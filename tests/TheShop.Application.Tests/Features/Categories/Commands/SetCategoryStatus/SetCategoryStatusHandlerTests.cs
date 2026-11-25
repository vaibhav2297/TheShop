using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.Commands.SetCategoryStatus;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.SetCategoryStatus;

/// <summary>
/// Tests for <see cref="SetCategoryStatusHandler"/> — applies the target status to every selected
/// category (covering both the inline single-row toggle, AC-16, and the bulk action, AC-28), while
/// counting only the categories that actually changed state (plan §6 Flow 4 step 3) and skipping a
/// category deleted concurrently by someone else rather than failing the whole batch.
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class SetCategoryStatusHandlerTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();

    private SetCategoryStatusHandler CreateSut() => new(_categories);

    private Category SetUpCategory(bool isActive)
    {
        var category = Category.Create($"Category {Guid.NewGuid()}", null, isActive);
        _categories.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
        return category;
    }

    private void SetUpSuccessfulUpdates() =>
        _categories.UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());

    // =========================================================================
    // Inline single-row toggle — activate is immediate, no confirmation (AC-16)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenActivatingASingleInactiveCategory_PersistsItAsActive()
    {
        var category = SetUpCategory(isActive: false);
        SetUpSuccessfulUpdates();

        await CreateSut().Handle(new SetCategoryStatusCommand([category.Id], true), CancellationToken.None);

        await _categories.Received(1).UpdateAsync(Arg.Is<Category>(c => c.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenActivatingASingleInactiveCategory_ReturnsChangedCountOfOne()
    {
        var category = SetUpCategory(isActive: false);
        SetUpSuccessfulUpdates();

        var result = await CreateSut().Handle(new SetCategoryStatusCommand([category.Id], true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChangedCount.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenDeactivatingASingleActiveCategory_PersistsItAsInactive()
    {
        var category = SetUpCategory(isActive: true);
        SetUpSuccessfulUpdates();

        await CreateSut().Handle(new SetCategoryStatusCommand([category.Id], false), CancellationToken.None);

        await _categories.Received(1).UpdateAsync(Arg.Is<Category>(c => !c.IsActive), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Already-in-target-status categories are no-ops, excluded from the count
    // (plan §6 Flow 4 step 3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenCategoryIsAlreadyInTheTargetStatus_DoesNotCountItAsChanged()
    {
        var category = SetUpCategory(isActive: true);
        SetUpSuccessfulUpdates();

        var result = await CreateSut().Handle(new SetCategoryStatusCommand([category.Id], true), CancellationToken.None);

        result.Value.ChangedCount.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithFiveSelectedOfWhichTwoAlreadyInactive_CountsOnlyTheThreeThatChanged()
    {
        // Deactivating a selection of 5 that already holds 2 inactive categories must report 3 —
        // the count the list visibly changes (plan §6 Flow 4 step 3).
        var active = new[] { SetUpCategory(isActive: true), SetUpCategory(isActive: true), SetUpCategory(isActive: true) };
        var alreadyInactive = new[] { SetUpCategory(isActive: false), SetUpCategory(isActive: false) };
        SetUpSuccessfulUpdates();

        var ids = active.Concat(alreadyInactive).Select(c => c.Id).ToList();
        var result = await CreateSut().Handle(new SetCategoryStatusCommand(ids, false), CancellationToken.None);

        result.Value.ChangedCount.Should().Be(3);
    }

    // =========================================================================
    // Bulk action applies to every selected category (AC-28)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithThreeSelectedCategories_UpdatesEachOfThem()
    {
        var categories = new[] { SetUpCategory(false), SetUpCategory(false), SetUpCategory(false) };
        SetUpSuccessfulUpdates();

        var result = await CreateSut().Handle(
            new SetCategoryStatusCommand(categories.Select(c => c.Id).ToList(), true), CancellationToken.None);

        result.Value.ChangedCount.Should().Be(3);
        foreach (var category in categories)
            await _categories.Received(1).UpdateAsync(Arg.Is<Category>(c => c.Id == category.Id && c.IsActive), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // A category deleted concurrently is skipped, not a failure (edge case)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenACategoryNoLongerExists_SkipsItWithoutFailingTheWholeBatch()
    {
        var missingId = Guid.NewGuid();
        _categories.GetByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((Category?)null);
        var existing = SetUpCategory(isActive: false);
        SetUpSuccessfulUpdates();

        var result = await CreateSut().Handle(
            new SetCategoryStatusCommand([missingId, existing.Id], true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChangedCount.Should().Be(1);
    }

    // =========================================================================
    // Persistence failure (edge case: "flipping a category's status fails")
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenUpdateAsyncFails_ReturnsTheFailureKey()
    {
        var category = SetUpCategory(isActive: false);
        _categories.UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(CategoryErrorKeys.StatusChangeFailed));

        var result = await CreateSut().Handle(new SetCategoryStatusCommand([category.Id], true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.StatusChangeFailed);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenUpdateAsyncThrowsUnexpectedly_ReturnsStatusChangeFailedFailure()
    {
        var category = SetUpCategory(isActive: false);
        _categories.UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        var result = await CreateSut().Handle(new SetCategoryStatusCommand([category.Id], true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.StatusChangeFailed);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-16: Handle_WhenActivatingASingleInactiveCategory_PersistsItAsActive,
//         Handle_WhenActivatingASingleInactiveCategory_ReturnsChangedCountOfOne,
//         Handle_WhenDeactivatingASingleActiveCategory_PersistsItAsInactive
// AC-28: Handle_WithThreeSelectedCategories_UpdatesEachOfThem,
//         Handle_WithFiveSelectedOfWhichTwoAlreadyInactive_CountsOnlyTheThreeThatChanged

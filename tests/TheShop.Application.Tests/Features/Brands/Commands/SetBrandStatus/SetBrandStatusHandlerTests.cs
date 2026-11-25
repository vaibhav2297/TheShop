using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands;
using TheShop.Application.Features.Brands.Commands.SetBrandStatus;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.SetBrandStatus;

/// <summary>
/// Tests for <see cref="SetBrandStatusHandler"/> — applies the target status to every selected
/// brand (covering both the inline single-row toggle, FR-18/AC-21, and the bulk action,
/// FR-20/AC-23), while counting only the brands that actually changed state (plan §6 Flow 3
/// step 3) and skipping a brand deleted concurrently by someone else rather than failing the
/// whole batch.
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class SetBrandStatusHandlerTests
{
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();

    private SetBrandStatusHandler CreateSut() => new(_brands);

    private Brand SetUpBrand(bool isActive)
    {
        var brand = Brand.Create($"Brand {Guid.NewGuid()}", null, isActive);
        _brands.GetByIdAsync(brand.Id, Arg.Any<CancellationToken>()).Returns(brand);
        return brand;
    }

    private void SetUpSuccessfulUpdates() =>
        _brands.UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());

    // =========================================================================
    // Inline single-row toggle — activate is immediate, no confirmation (AC-21)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenActivatingASingleInactiveBrand_PersistsItAsActive()
    {
        var brand = SetUpBrand(isActive: false);
        SetUpSuccessfulUpdates();

        await CreateSut().Handle(new SetBrandStatusCommand([brand.Id], true), CancellationToken.None);

        await _brands.Received(1).UpdateAsync(Arg.Is<Brand>(b => b.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenActivatingASingleInactiveBrand_ReturnsChangedCountOfOne()
    {
        var brand = SetUpBrand(isActive: false);
        SetUpSuccessfulUpdates();

        var result = await CreateSut().Handle(new SetBrandStatusCommand([brand.Id], true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChangedCount.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenDeactivatingASingleActiveBrand_PersistsItAsInactive()
    {
        var brand = SetUpBrand(isActive: true);
        SetUpSuccessfulUpdates();

        await CreateSut().Handle(new SetBrandStatusCommand([brand.Id], false), CancellationToken.None);

        await _brands.Received(1).UpdateAsync(Arg.Is<Brand>(b => !b.IsActive), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Already-in-target-status brands are no-ops, excluded from the count
    // (plan §6 Flow 3 step 3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenBrandIsAlreadyInTheTargetStatus_DoesNotCountItAsChanged()
    {
        var brand = SetUpBrand(isActive: true);
        SetUpSuccessfulUpdates();

        var result = await CreateSut().Handle(new SetBrandStatusCommand([brand.Id], true), CancellationToken.None);

        result.Value.ChangedCount.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithFiveSelectedOfWhichTwoAlreadyInactive_CountsOnlyTheThreeThatChanged()
    {
        // Deactivating a selection of 5 that already holds 2 inactive brands must report 3 — the
        // count the list visibly changes (plan §6 Flow 3 step 3).
        var active = new[] { SetUpBrand(isActive: true), SetUpBrand(isActive: true), SetUpBrand(isActive: true) };
        var alreadyInactive = new[] { SetUpBrand(isActive: false), SetUpBrand(isActive: false) };
        SetUpSuccessfulUpdates();

        var ids = active.Concat(alreadyInactive).Select(b => b.Id).ToList();
        var result = await CreateSut().Handle(new SetBrandStatusCommand(ids, false), CancellationToken.None);

        result.Value.ChangedCount.Should().Be(3);
    }

    // =========================================================================
    // Bulk action applies to every selected brand (AC-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithThreeSelectedBrands_UpdatesEachOfThem()
    {
        var brands = new[] { SetUpBrand(false), SetUpBrand(false), SetUpBrand(false) };
        SetUpSuccessfulUpdates();

        var result = await CreateSut().Handle(
            new SetBrandStatusCommand(brands.Select(b => b.Id).ToList(), true), CancellationToken.None);

        result.Value.ChangedCount.Should().Be(3);
        foreach (var brand in brands)
            await _brands.Received(1).UpdateAsync(Arg.Is<Brand>(b => b.Id == brand.Id && b.IsActive), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // A brand deleted concurrently is skipped, not a failure (edge case)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenABrandNoLongerExists_SkipsItWithoutFailingTheWholeBatch()
    {
        var missingId = Guid.NewGuid();
        _brands.GetByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((Brand?)null);
        var existing = SetUpBrand(isActive: false);
        SetUpSuccessfulUpdates();

        var result = await CreateSut().Handle(
            new SetBrandStatusCommand([missingId, existing.Id], true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChangedCount.Should().Be(1);
    }

    // =========================================================================
    // Persistence failure (edge case: "flipping a brand's status fails")
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenUpdateAsyncFails_ReturnsTheFailureKey()
    {
        var brand = SetUpBrand(isActive: false);
        _brands.UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(BrandErrorKeys.StatusChangeFailed));

        var result = await CreateSut().Handle(new SetBrandStatusCommand([brand.Id], true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.StatusChangeFailed);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenUpdateAsyncThrowsUnexpectedly_ReturnsStatusChangeFailedFailure()
    {
        var brand = SetUpBrand(isActive: false);
        _brands.UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        var result = await CreateSut().Handle(new SetBrandStatusCommand([brand.Id], true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.StatusChangeFailed);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-21: Handle_WhenActivatingASingleInactiveBrand_PersistsItAsActive,
//         Handle_WhenActivatingASingleInactiveBrand_ReturnsChangedCountOfOne,
//         Handle_WhenDeactivatingASingleActiveBrand_PersistsItAsInactive
// AC-23: Handle_WithThreeSelectedBrands_UpdatesEachOfThem,
//         Handle_WithFiveSelectedOfWhichTwoAlreadyInactive_CountsOnlyTheThreeThatChanged

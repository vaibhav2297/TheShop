using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.Commands.DeleteCategories;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Roles;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.DeleteCategories;

/// <summary>
/// Tests for <see cref="DeleteCategoriesHandler"/> — delegates the atomic partial-success delete
/// to <see cref="ICategoryRepository.DeleteManyAsync"/> (RULE-6/RULE-15; single-row AC-17, bulk
/// AC-29/AC-30), best-effort disposes the deleted categories' image objects afterwards (RULE-11),
/// and translates the RPC's access-denied exception into <see cref="RbacErrorKeys.AccessDenied"/>.
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class DeleteCategoriesHandlerTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private DeleteCategoriesHandler CreateSut() => new(_categories, _fileStorage);

    private void SetUpDeletionOutcome(CategoryDeletionOutcomeDto outcome, IReadOnlyList<string> deletedImagePaths) =>
        _categories.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
               .Returns((outcome, deletedImagePaths));

    // =========================================================================
    // Single category, no product references it (AC-17)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenTheCategoryIsDeletable_ReturnsSuccessWithDeletedCountOfOne()
    {
        var id = Guid.NewGuid();
        SetUpDeletionOutcome(new CategoryDeletionOutcomeDto(1, []), []);

        var result = await CreateSut().Handle(new DeleteCategoriesCommand([id]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeletedCount.Should().Be(1);
        result.Value.Blocked.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenTheDeletedCategoryHadAnImage_DeletesTheImageObject()
    {
        SetUpDeletionOutcome(new CategoryDeletionOutcomeDto(1, []), ["categories/abc/image.webp"]);

        await CreateSut().Handle(new DeleteCategoriesCommand([Guid.NewGuid()]), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.CategoryImages, "categories/abc/image.webp", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenTheDeletedCategoryHadNoImage_DoesNotAttemptToDeleteAnything()
    {
        SetUpDeletionOutcome(new CategoryDeletionOutcomeDto(1, []), []);

        await CreateSut().Handle(new DeleteCategoriesCommand([Guid.NewGuid()]), CancellationToken.None);

        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<StorageArea>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Single category, referenced by a product (AC-19, RULE-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenTheCategoryIsInUse_ReturnsZeroDeletedAndTheBlockedCategoryWithItsProductCount()
    {
        var id = Guid.NewGuid();
        SetUpDeletionOutcome(new CategoryDeletionOutcomeDto(0, [new BlockedCategoryDto(id, "Disposables", 3)]), []);

        var result = await CreateSut().Handle(new DeleteCategoriesCommand([id]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeletedCount.Should().Be(0);
        result.Value.Blocked.Should().ContainSingle(b => b.Id == id && b.Name == "Disposables" && b.ProductCount == 3);
    }

    // =========================================================================
    // Bulk deletion — mixed outcome (RULE-15, AC-29)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithAMixOfDeletableAndInUseCategories_ReturnsBothTheDeletedCountAndTheBlockedList()
    {
        var blocked = new[]
        {
            new BlockedCategoryDto(Guid.NewGuid(), "Disposables", 2),
            new BlockedCategoryDto(Guid.NewGuid(), "Pod Systems", 1),
        };
        SetUpDeletionOutcome(new CategoryDeletionOutcomeDto(3, blocked), ["a/1.webp", "a/2.webp", "a/3.webp"]);

        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var result = await CreateSut().Handle(new DeleteCategoriesCommand(ids), CancellationToken.None);

        result.Value.DeletedCount.Should().Be(3);
        result.Value.Blocked.Should().BeEquivalentTo(blocked);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithAMixOfDeletableAndInUseCategories_DeletesOnlyTheDeletedOnesImages()
    {
        SetUpDeletionOutcome(
            new CategoryDeletionOutcomeDto(2, [new BlockedCategoryDto(Guid.NewGuid(), "Disposables", 1)]),
            ["a/1.webp", "a/2.webp"]);

        await CreateSut().Handle(new DeleteCategoriesCommand([Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.CategoryImages, "a/1.webp", Arg.Any<CancellationToken>());
        await _fileStorage.Received(1).DeleteAsync(StorageArea.CategoryImages, "a/2.webp", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Bulk deletion — every selected category is in use (RULE-15, AC-30)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenEverySelectedCategoryIsInUse_ReturnsZeroDeletedAndEveryCategoryBlocked()
    {
        var blocked = new[]
        {
            new BlockedCategoryDto(Guid.NewGuid(), "Disposables", 2),
            new BlockedCategoryDto(Guid.NewGuid(), "Pod Systems", 5),
        };
        SetUpDeletionOutcome(new CategoryDeletionOutcomeDto(0, blocked), []);

        var result = await CreateSut().Handle(
            new DeleteCategoriesCommand(blocked.Select(b => b.Id).ToList()), CancellationToken.None);

        result.Value.DeletedCount.Should().Be(0);
        result.Value.Blocked.Should().HaveCount(2);
    }

    // =========================================================================
    // Best-effort image cleanup — a storage failure never fails the request (RULE-11 compensation)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenDeletingTheImageObjectThrows_StillReturnsSuccess()
    {
        SetUpDeletionOutcome(new CategoryDeletionOutcomeDto(1, []), ["a/1.webp"]);
        _fileStorage.DeleteAsync(StorageArea.CategoryImages, "a/1.webp", Arg.Any<CancellationToken>())
                    .Throws(new InvalidOperationException("storage unreachable"));

        var result = await CreateSut().Handle(new DeleteCategoriesCommand([Guid.NewGuid()]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // =========================================================================
    // Access-denied translation — the RPC re-checks categories.delete itself
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenTheRepositoryThrowsAnAccessDeniedException_ReturnsRbacAccessDeniedFailure()
    {
        _categories.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("access denied"));

        var result = await CreateSut().Handle(new DeleteCategoriesCommand([Guid.NewGuid()]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RbacErrorKeys.AccessDenied);
    }

    // =========================================================================
    // Unexpected technical failure (edge case: connection problem)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenTheRepositoryThrowsUnexpectedly_ReturnsDeleteFailedFailure()
    {
        _categories.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        var result = await CreateSut().Handle(new DeleteCategoriesCommand([Guid.NewGuid()]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.DeleteFailed);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-17: Handle_WhenTheCategoryIsDeletable_ReturnsSuccessWithDeletedCountOfOne,
//         Handle_WhenTheDeletedCategoryHadAnImage_DeletesTheImageObject
// AC-19: Handle_WhenTheCategoryIsInUse_ReturnsZeroDeletedAndTheBlockedCategoryWithItsProductCount
// AC-29: Handle_WithAMixOfDeletableAndInUseCategories_ReturnsBothTheDeletedCountAndTheBlockedList,
//         Handle_WithAMixOfDeletableAndInUseCategories_DeletesOnlyTheDeletedOnesImages
// AC-30: Handle_WhenEverySelectedCategoryIsInUse_ReturnsZeroDeletedAndEveryCategoryBlocked
// AC-31: Handle_WhenTheRepositoryThrowsAnAccessDeniedException_ReturnsRbacAccessDeniedFailure

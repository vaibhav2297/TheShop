using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.DeleteProducts;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Roles;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.DeleteProducts;

/// <summary>
/// Tests for <see cref="DeleteProductsHandler"/> — delegates the atomic partial-success delete to
/// <see cref="IProductRepository.DeleteManyAsync"/> (RULE-3/RULE-4; single-row AC-13, bulk
/// AC-16/AC-17), best-effort disposes the deleted products' exclusive image objects afterwards
/// (RULE-8), and translates the RPC's access-denied exception into
/// <see cref="RbacErrorKeys.AccessDenied"/>. No table references <c>products</c> today (plan §11
/// accepted risk), so every stubbed outcome here proves the handler's own branching rather than
/// the SQL reference count, which stays a <c>0</c>-literal until <c>order_items</c> ships.
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class DeleteProductsHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private DeleteProductsHandler CreateSut() => new(_products, _fileStorage);

    private void SetUpDeletionOutcome(ProductDeletionOutcomeDto outcome, IReadOnlyList<string> deletedImageKeys) =>
        _products.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns((outcome, deletedImageKeys));

    // =========================================================================
    // Single product, nothing references it (AC-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenTheProductIsDeletable_ReturnsSuccessWithDeletedCountOfOne()
    {
        var id = Guid.NewGuid();
        SetUpDeletionOutcome(new ProductDeletionOutcomeDto(1, []), []);

        var result = await CreateSut().Handle(new DeleteProductsCommand([id]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeletedCount.Should().Be(1);
        result.Value.Blocked.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenTheDeletedProductHadImages_DeletesEachImageObject()
    {
        SetUpDeletionOutcome(new ProductDeletionOutcomeDto(1, []), ["products/abc/1.webp", "products/abc/2.webp"]);

        await CreateSut().Handle(new DeleteProductsCommand([Guid.NewGuid()]), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.ProductImages, "products/abc/1.webp", Arg.Any<CancellationToken>());
        await _fileStorage.Received(1).DeleteAsync(StorageArea.ProductImages, "products/abc/2.webp", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenTheDeletedProductHadNoImages_DoesNotAttemptToDeleteAnything()
    {
        SetUpDeletionOutcome(new ProductDeletionOutcomeDto(1, []), []);

        await CreateSut().Handle(new DeleteProductsCommand([Guid.NewGuid()]), CancellationToken.None);

        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<StorageArea>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Single product, referenced by another record (FR-12, RULE-3) — stubbed outcome,
    // since no referencing table exists yet (plan §11 accepted risk)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenTheProductIsReferenced_ReturnsZeroDeletedAndTheBlockedProductWithItsReferenceCount()
    {
        var id = Guid.NewGuid();
        SetUpDeletionOutcome(new ProductDeletionOutcomeDto(0, [new ReferencedProductDto(id, "Elf Bar BC5000", 3)]), []);

        var result = await CreateSut().Handle(new DeleteProductsCommand([id]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeletedCount.Should().Be(0);
        result.Value.Blocked.Should().ContainSingle(p => p.Id == id && p.Name == "Elf Bar BC5000" && p.ReferenceCount == 3);
    }

    // =========================================================================
    // Bulk deletion — partial success (RULE-3/RULE-4, AC-16)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WithFiveSelectedOfWhichTwoAreReferenced_ReturnsThreeDeletedAndTheTwoBlocked()
    {
        var blocked = new[]
        {
            new ReferencedProductDto(Guid.NewGuid(), "Elf Bar BC5000", 2),
            new ReferencedProductDto(Guid.NewGuid(), "Lost Mary OS5000", 1),
        };
        SetUpDeletionOutcome(new ProductDeletionOutcomeDto(3, blocked), ["a/1.webp", "a/2.webp", "a/3.webp"]);

        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var result = await CreateSut().Handle(new DeleteProductsCommand(ids), CancellationToken.None);

        result.Value.DeletedCount.Should().Be(3);
        result.Value.Blocked.Should().BeEquivalentTo(blocked);
    }

    // =========================================================================
    // Bulk deletion — every selected product is referenced (RULE-3/RULE-4, AC-17)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenEverySelectedProductIsReferenced_ReturnsZeroDeletedAndEveryProductBlocked()
    {
        var blocked = new[]
        {
            new ReferencedProductDto(Guid.NewGuid(), "Elf Bar BC5000", 2),
            new ReferencedProductDto(Guid.NewGuid(), "Lost Mary OS5000", 5),
        };
        SetUpDeletionOutcome(new ProductDeletionOutcomeDto(0, blocked), []);

        var result = await CreateSut().Handle(
            new DeleteProductsCommand(blocked.Select(p => p.Id).ToList()), CancellationToken.None);

        result.Value.DeletedCount.Should().Be(0);
        result.Value.Blocked.Should().HaveCount(2);
    }

    // =========================================================================
    // Best-effort image cleanup — a storage failure never fails the request (RULE-8 compensation)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenDeletingAnImageObjectThrows_StillReturnsSuccess()
    {
        SetUpDeletionOutcome(new ProductDeletionOutcomeDto(1, []), ["a/1.webp"]);
        _fileStorage.DeleteAsync(StorageArea.ProductImages, "a/1.webp", Arg.Any<CancellationToken>())
                    .Throws(new InvalidOperationException("storage unreachable"));

        var result = await CreateSut().Handle(new DeleteProductsCommand([Guid.NewGuid()]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // =========================================================================
    // Access-denied translation — the RPC re-checks products.delete itself (RULE-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenTheRepositoryThrowsAnAccessDeniedException_ReturnsRbacAccessDeniedFailure()
    {
        _products.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Throws(new InvalidOperationException("access denied"));

        var result = await CreateSut().Handle(new DeleteProductsCommand([Guid.NewGuid()]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RbacErrorKeys.AccessDenied);
    }

    // =========================================================================
    // Unexpected technical failure (edge case: connection problem)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenTheRepositoryThrowsUnexpectedly_ReturnsDeleteFailedFailure()
    {
        _products.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
                 .Throws(new InvalidOperationException("connection lost"));

        var result = await CreateSut().Handle(new DeleteProductsCommand([Guid.NewGuid()]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ProductErrorKeys.DeleteFailed);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-13: Handle_WhenTheProductIsDeletable_ReturnsSuccessWithDeletedCountOfOne,
//         Handle_WhenTheDeletedProductHadImages_DeletesEachImageObject
// AC-15: Handle_WhenTheProductIsReferenced_ReturnsZeroDeletedAndTheBlockedProductWithItsReferenceCount
// AC-16: Handle_WithFiveSelectedOfWhichTwoAreReferenced_ReturnsThreeDeletedAndTheTwoBlocked
// AC-17: Handle_WhenEverySelectedProductIsReferenced_ReturnsZeroDeletedAndEveryProductBlocked
// AC-18: Handle_WhenTheRepositoryThrowsAnAccessDeniedException_ReturnsRbacAccessDeniedFailure

using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.UpdateProduct;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.UpdateProduct;

/// <summary>
/// Tests for <see cref="UpdateProductHandler"/>'s general save flow: the happy path across all
/// editable fields (AC-6, AC-7), a stale id (AC-34), name/SKU uniqueness excluding the product's
/// own current values (RULE-2, RULE-8), category/brand missing (RULE-3), the row-version
/// concurrency guard (Decision 11), gallery diffing that deletes removed images (AC-7, RULE-18),
/// and technical-failure handling (AC-32). Variant image pin resolution and the publish-refusal
/// detail are covered separately in <c>UpdateProductHandlerPinTests</c>.
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class UpdateProductHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Guid _brandId = Guid.NewGuid();
    private readonly Guid _storedImageId = Guid.NewGuid();

    public UpdateProductHandlerTests()
    {
        _categories.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Category.Rehydrate(_categoryId, "Disposables"));
        _brands.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Brand.Rehydrate(_brandId, "Elf Bar", "elf-bar"));
        _products.FindConflictsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ProductConflicts.None);
        _products.GetForEditAsync(_productId, Arg.Any<CancellationToken>())
            .Returns((StoredProduct(), "row-version-1"));
    }

    private Product StoredProduct() =>
        Product.Rehydrate(
            _productId, "Elf Bar BC5000", "A long-lasting disposable vape.", null,
            Sku.Create("ELF-BC5000"), ProductPricing.Create(Money.Create(24.99m)), false,
            Category.Rehydrate(_categoryId, "Disposables"), Brand.Rehydrate(_brandId, "Elf Bar", "elf-bar"),
            DateTimeOffset.UtcNow,
            images: [ProductImage.Rehydrate(_storedImageId, "products/stored.png", 0, true)]);

    private UpdateProductHandler CreateSut() => new(_products, _categories, _brands, _fileStorage);

    private UpdateProductCommand Command(
        IReadOnlyList<ProductGalleryEntry>? gallery = null,
        string rowVersion = "row-version-1",
        Guid? id = null,
        string name = "Elf Bar BC5000",
        decimal? originalPrice = 24.99m) =>
        new(id ?? _productId, rowVersion, name, "A long-lasting disposable vape.", "ELF-BC5000",
            _categoryId, _brandId, originalPrice, null, false, gallery ?? [], [], []);

    // =========================================================================
    // Happy path (AC-6, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithValidChanges_PersistsAndReturnsTheUpdatedDto()
    {
        _products.UpdateAsync(Arg.Any<Product>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok("row-version-2"));

        var result = await CreateSut().Handle(Command(name: "Elf Bar Renamed"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Elf Bar Renamed");
        result.Value.RowVersion.Should().Be("row-version-2");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithNoChangesAtAll_Succeeds()
    {
        _products.UpdateAsync(Arg.Any<Product>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok("row-version-2"));

        var result = await CreateSut().Handle(
            Command(gallery: [new ProductGalleryEntry(_storedImageId, null, 0, true, _storedImageId)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("saving an edit form untouched must not be refused as a conflict with itself");
    }

    // =========================================================================
    // Stale edit link (AC-34)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenTheProductNoLongerExists_FailsWithNotFound()
    {
        _products.GetForEditAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(((Product Product, string RowVersion)?)null);

        var result = await CreateSut().Handle(Command(id: Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.NotFound);
    }

    // =========================================================================
    // Name / SKU uniqueness, excluding the product's own current values (RULE-2, RULE-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenRenamedToAnotherProductsName_FailsWithNameAlreadyExists()
    {
        _products.FindConflictsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new ProductConflicts(NameTaken: true, SkusTaken: []));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.NameAlreadyExists);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_ChecksConflictsExcludingItsOwnProductId()
    {
        _products.UpdateAsync(Arg.Any<Product>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok("row-version-2"));

        await CreateSut().Handle(Command(), CancellationToken.None);

        await _products.Received(1).FindConflictsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), _productId, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Category / brand missing (RULE-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenTheCategoryDoesNotExist_FailsWithCategoryRequired()
    {
        _categories.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Category?)null);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.CategoryRequired);
    }

    // =========================================================================
    // Concurrent edit (Decision 11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenAnotherStaffMemberSavedSinceTheFormLoaded_FailsWithModifiedElsewhere()
    {
        _products.UpdateAsync(Arg.Any<Product>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<string>(ProductErrorKeys.ModifiedElsewhere));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.ModifiedElsewhere,
            "a concurrent save must not be silently overwritten");
    }

    // =========================================================================
    // Gallery diffing — new upload, removed image discarded (AC-7, RULE-18)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenAnImageIsRemovedFromTheGallery_DeletesItAfterSaving()
    {
        _products.UpdateAsync(Arg.Any<Product>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok("row-version-2"));

        var result = await CreateSut().Handle(Command(gallery: []), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _fileStorage.Received(1).DeleteAsync(StorageArea.ProductImages, "products/stored.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenANewImageIsAdded_UploadsItAndKeepsTheStoredOne()
    {
        _fileStorage.UploadAsync(
            StorageArea.ProductImages, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("products/new.png");
        _products.UpdateAsync(Arg.Any<Product>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok("row-version-2"));

        var result = await CreateSut().Handle(
            Command(gallery: [
                new ProductGalleryEntry(_storedImageId, null, 0, true, _storedImageId),
                new ProductGalleryEntry(null, new ProductImageUpload([1, 2, 3], "new.png", "image/png"), 1, false, Guid.NewGuid()),
            ]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Images.Should().HaveCount(2);
        await _fileStorage.DidNotReceive().DeleteAsync(StorageArea.ProductImages, "products/stored.png", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Technical failure (AC-32)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenAnUnexpectedExceptionIsThrown_FailsWithUpdateFailed()
    {
        _products.UpdateAsync(Arg.Any<Product>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("connection problem"));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.UpdateFailed);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6 (edit form saves exactly what changed): Handle_WithValidChanges_PersistsAndReturnsTheUpdatedDto,
//        Handle_WithNoChangesAtAll_Succeeds
// AC-7 (gallery diffing on save; removed image no longer stored): Handle_WhenAnImageIsRemovedFromTheGallery_DeletesItAfterSaving,
//        Handle_WhenANewImageIsAdded_UploadsItAndKeepsTheStoredOne
// AC-22 (duplicate product name refused): Handle_WhenRenamedToAnotherProductsName_FailsWithNameAlreadyExists
// AC-23 (category/brand required): Handle_WhenTheCategoryDoesNotExist_FailsWithCategoryRequired
// AC-27 (a product's own SKU/name raise no conflict on a no-op edit): Handle_ChecksConflictsExcludingItsOwnProductId,
//        Handle_WithNoChangesAtAll_Succeeds
// AC-32 (a failed save preserves everything, never partly saves): Handle_WhenAnUnexpectedExceptionIsThrown_FailsWithUpdateFailed
// AC-34 (stale edit link): Handle_WhenTheProductNoLongerExists_FailsWithNotFound
// RULE-18 (an unused image is discarded): Handle_WhenAnImageIsRemovedFromTheGallery_DeletesItAfterSaving
// Edge case (changed by another staff member): Handle_WhenAnotherStaffMemberSavedSinceTheFormLoaded_FailsWithModifiedElsewhere

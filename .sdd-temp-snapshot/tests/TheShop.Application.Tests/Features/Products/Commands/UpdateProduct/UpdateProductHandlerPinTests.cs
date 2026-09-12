using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.Commands.UpdateProduct;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.UpdateProduct;

/// <summary>
/// Tests for <see cref="UpdateProductHandler"/>'s variant image pin resolution (FR-16, RULE-13)
/// when the gallery mixes images already stored with images added in this edit. A kept image is
/// its own correlation key, a new one gets a client-minted key the handler resolves to the
/// identifier the aggregate mints, and both have to work in the same save.
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class UpdateProductHandlerPinTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Guid _brandId = Guid.NewGuid();
    private readonly Guid _storedImageId = Guid.NewGuid();

    private Product? _persisted;

    public UpdateProductHandlerPinTests()
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

        _fileStorage.UploadAsync(
            StorageArea.ProductImages, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("products/new-image.png");

        _products.UpdateAsync(Arg.Any<Product>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _persisted = call.Arg<Product>();
                return Result.Ok("row-version-2");
            });
    }

    /// <summary>The product as it stands before the edit: one stored gallery image, no variants.</summary>
    private Product StoredProduct() =>
        Product.Rehydrate(
            _productId,
            "Elf Bar BC5000",
            ProductDescription.Rehydrate("A long-lasting disposable vape."),
            null,
            Sku.Create("ELF-BC5000"),
            null,
            false,
            Category.Rehydrate(_categoryId, "Disposables"),
            Brand.Rehydrate(_brandId, "Elf Bar", "elf-bar"),
            DateTimeOffset.UtcNow,
            images: [ProductImage.Rehydrate(_storedImageId, "products/stored-image.png", 0, true)]);

    private UpdateProductHandler CreateSut() => new(_products, _categories, _brands, _fileStorage);

    private UpdateProductCommand Command(
        IReadOnlyList<ProductGalleryEntry> gallery,
        IReadOnlyList<OptionTypeInput> optionTypes,
        IReadOnlyList<VariantInput> variants) =>
        new(_productId, "row-version-1", "Elf Bar BC5000", "A long-lasting disposable vape.", "ELF-BC5000",
            _categoryId, _brandId, null, null, false, gallery, optionTypes, [], variants);

    private static (IReadOnlyList<OptionTypeInput> OptionTypes, Guid MangoValueId, Guid MintValueId) FlavourOptions()
    {
        var mango = Guid.NewGuid();
        var mint = Guid.NewGuid();
        return ([new OptionTypeInput(Guid.NewGuid(), "Flavour", [new OptionValueInput(mango, "Mango"), new OptionValueInput(mint, "Mint")])], mango, mint);
    }

    private static VariantInput Variant(Guid optionValueId, string sku, Guid? pinnedImageId) =>
        new(Guid.NewGuid(), [optionValueId], sku, 24.99m, null, true, pinnedImageId);

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_PinningAKeptImage_ResolvesToTheStoredIdentifier()
    {
        var (optionTypes, mangoValueId, _) = FlavourOptions();

        var result = await CreateSut().Handle(
            Command(
                [new ProductGalleryEntry(_storedImageId, null, 0, true, _storedImageId)],
                optionTypes,
                [Variant(mangoValueId, "ELF-BC5000-MANGO", _storedImageId)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _persisted!.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId))
            .PinnedImageId.Should().Be(_storedImageId);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_PinningAnImageAddedInThisEdit_ResolvesToTheNewlyMintedIdentifier()
    {
        var newImageClientId = Guid.NewGuid();
        var (optionTypes, mangoValueId, _) = FlavourOptions();

        var result = await CreateSut().Handle(
            Command(
                [
                    new ProductGalleryEntry(_storedImageId, null, 0, true, _storedImageId),
                    new ProductGalleryEntry(null, new ProductImageUpload([1, 2, 3], "new.png", "image/png"), 1, false, newImageClientId),
                ],
                optionTypes,
                [Variant(mangoValueId, "ELF-BC5000-MANGO", newImageClientId)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var newImageId = _persisted!.Images[1].Id;
        newImageId.Should().NotBe(newImageClientId, "the aggregate mints the identifier, not the client");
        _persisted.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId))
            .PinnedImageId.Should().Be(newImageId);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithAKeptAndANewImagePinnedByDifferentVariants_ResolvesBoth()
    {
        var newImageClientId = Guid.NewGuid();
        var (optionTypes, mangoValueId, mintValueId) = FlavourOptions();

        await CreateSut().Handle(
            Command(
                [
                    new ProductGalleryEntry(_storedImageId, null, 0, true, _storedImageId),
                    new ProductGalleryEntry(null, new ProductImageUpload([1, 2, 3], "new.png", "image/png"), 1, false, newImageClientId),
                ],
                optionTypes,
                [
                    Variant(mangoValueId, "ELF-BC5000-MANGO", _storedImageId),
                    Variant(mintValueId, "ELF-BC5000-MINT", newImageClientId),
                ]),
            CancellationToken.None);

        _persisted!.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId))
            .PinnedImageId.Should().Be(_persisted.Images[0].Id);
        _persisted.Variants.Single(v => v.OptionValueIds.Contains(mintValueId))
            .PinnedImageId.Should().Be(_persisted.Images[1].Id);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_PinningAnImageDroppedFromTheGallery_LeavesTheVariantUnpinned()
    {
        var newImageClientId = Guid.NewGuid();
        var (optionTypes, mangoValueId, _) = FlavourOptions();

        // The stored image is gone from the submitted gallery, so the pin naming it resolves to
        // nothing — RULE-13's "removing a pinned image leaves those variants unpinned".
        await CreateSut().Handle(
            Command(
                [new ProductGalleryEntry(null, new ProductImageUpload([1, 2, 3], "new.png", "image/png"), 0, true, newImageClientId)],
                optionTypes,
                [Variant(mangoValueId, "ELF-BC5000-MANGO", _storedImageId)]),
            CancellationToken.None);

        _persisted!.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId))
            .PinnedImageId.Should().BeNull();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-10a (pin a gallery image to a variant, Edit mode): Handle_PinningAKeptImage_ResolvesToTheStoredIdentifier,
//         Handle_PinningAnImageAddedInThisEdit_ResolvesToTheNewlyMintedIdentifier,
//         Handle_WithAKeptAndANewImagePinnedByDifferentVariants_ResolvesBoth
// RULE-13 (removing a pinned image unpins its variants): Handle_PinningAnImageDroppedFromTheGallery_LeavesTheVariantUnpinned

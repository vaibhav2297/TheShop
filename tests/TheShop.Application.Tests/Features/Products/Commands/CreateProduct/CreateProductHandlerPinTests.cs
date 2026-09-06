using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.Commands.CreateProduct;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.CreateProduct;

/// <summary>
/// Tests for <see cref="CreateProductHandler"/>'s variant image pin resolution (FR-16, RULE-13)
/// and its publish refusal (RULE-14, AC-20). A staff member can pin an image they picked moments
/// earlier, before it has been uploaded and given an identifier, so the command refers to gallery
/// images by the client's own <c>ClientId</c> and the handler resolves those to the identifiers the
/// aggregate mints. A pin naming an image outside the submitted gallery resolves to nothing and is
/// dropped, which is the ownership guard RULE-13 asks for.
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class CreateProductHandlerPinTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Guid _brandId = Guid.NewGuid();

    private Product? _persisted;

    public CreateProductHandlerPinTests()
    {
        _categories.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Category.Rehydrate(_categoryId, "Disposables"));
        _brands.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Brand.Rehydrate(_brandId, "Elf Bar", "elf-bar"));
        _products.FindConflictsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ProductConflicts.None);

        // Each upload gets its own object key, so the gallery never collapses two images into one.
        var uploadCount = 0;
        _fileStorage.UploadAsync(
            StorageArea.ProductImages, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => $"products/image-{Interlocked.Increment(ref uploadCount)}.png");

        _products.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _persisted = call.Arg<Product>();
                return Result.Ok("row-version-1");
            });
    }

    private CreateProductHandler CreateSut() => new(_products, _categories, _brands, _fileStorage);

    private static ProductGalleryEntry NewImage(Guid clientId, int position) =>
        new(null, new ProductImageUpload([1, 2, 3], $"{position}.png", "image/png"), position, position == 0, clientId);

    private CreateProductCommand Command(
        IReadOnlyList<ProductGalleryEntry> gallery,
        IReadOnlyList<OptionTypeInput> optionTypes,
        IReadOnlyList<VariantInput> variants,
        bool isPublished = false) =>
        new("Elf Bar BC5000", "A long-lasting disposable vape.", "ELF-BC5000",
            _categoryId, _brandId, null, null, isPublished, gallery, optionTypes, variants);

    /// <summary>
    /// One option type with two values, and the two variants they generate — the smallest shape
    /// that can carry a pin.
    /// </summary>
    private static (IReadOnlyList<OptionTypeInput> OptionTypes, Guid MangoValueId, Guid MintValueId) FlavourOptions()
    {
        var mango = Guid.NewGuid();
        var mint = Guid.NewGuid();
        return ([new OptionTypeInput(Guid.NewGuid(), "Flavour", [new OptionValueInput(mango, "Mango"), new OptionValueInput(mint, "Mint")])], mango, mint);
    }

    private static VariantInput Variant(Guid optionValueId, string sku, decimal? price, Guid? pinnedImageId = null) =>
        new(Guid.NewGuid(), [optionValueId], sku, price, null, true, pinnedImageId);

    // =========================================================================
    // Pin resolution (FR-16, AC-10a)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithAPinOnAnImageUploadedInTheSameSave_PinsTheVariantToThatImage()
    {
        var mangoImageId = Guid.NewGuid();
        var (optionTypes, mangoValueId, mintValueId) = FlavourOptions();

        var result = await CreateSut().Handle(
            Command(
                [NewImage(mangoImageId, 0), NewImage(Guid.NewGuid(), 1)],
                optionTypes,
                [Variant(mangoValueId, "ELF-BC5000-MANGO", 24.99m, mangoImageId), Variant(mintValueId, "ELF-BC5000-MINT", 24.99m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _persisted.Should().NotBeNull();

        var mangoVariant = _persisted!.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId));
        mangoVariant.PinnedImageId.Should().Be(
            _persisted.Images[0].Id,
            "the pin named the first gallery image by its client id, which the handler resolves to the identifier the aggregate minted");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithAPinOnAnImageUploadedInTheSameSave_DoesNotPersistTheClientsOwnIdentifier()
    {
        var mangoImageId = Guid.NewGuid();
        var (optionTypes, mangoValueId, _) = FlavourOptions();

        await CreateSut().Handle(
            Command(
                [NewImage(mangoImageId, 0)],
                optionTypes,
                [Variant(mangoValueId, "ELF-BC5000-MANGO", 24.99m, mangoImageId)]),
            CancellationToken.None);

        _persisted.Should().NotBeNull();
        _persisted!.Images.Should().OnlyContain(image => image.Id != mangoImageId,
            "an image's identity is minted server-side; the client's value is only a correlation key");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithEachVariantPinnedToADifferentImage_KeepsThePinsDistinct()
    {
        var firstImageId = Guid.NewGuid();
        var secondImageId = Guid.NewGuid();
        var (optionTypes, mangoValueId, mintValueId) = FlavourOptions();

        await CreateSut().Handle(
            Command(
                [NewImage(firstImageId, 0), NewImage(secondImageId, 1)],
                optionTypes,
                [
                    Variant(mangoValueId, "ELF-BC5000-MANGO", 24.99m, firstImageId),
                    Variant(mintValueId, "ELF-BC5000-MINT", 24.99m, secondImageId),
                ]),
            CancellationToken.None);

        _persisted.Should().NotBeNull();
        var mango = _persisted!.Variants.Single(v => v.OptionValueIds.Contains(mangoValueId));
        var mint = _persisted.Variants.Single(v => v.OptionValueIds.Contains(mintValueId));

        mango.PinnedImageId.Should().Be(_persisted.Images[0].Id);
        mint.PinnedImageId.Should().Be(_persisted.Images[1].Id);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithAPinNamingAnImageOutsideTheGallery_LeavesTheVariantUnpinned()
    {
        var (optionTypes, mangoValueId, _) = FlavourOptions();

        var result = await CreateSut().Handle(
            Command(
                [NewImage(Guid.NewGuid(), 0)],
                optionTypes,
                [Variant(mangoValueId, "ELF-BC5000-MANGO", 24.99m, Guid.NewGuid())]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _persisted.Should().NotBeNull();
        _persisted!.Variants.Should().OnlyContain(v => v.PinnedImageId == null,
            "a variant may only be pinned to one of the product's own images (RULE-13)");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WithNoGalleryAtAll_LeavesAPinnedVariantUnpinned()
    {
        var (optionTypes, mangoValueId, _) = FlavourOptions();

        var result = await CreateSut().Handle(
            Command([], optionTypes, [Variant(mangoValueId, "ELF-BC5000-MANGO", 24.99m, Guid.NewGuid())]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _persisted!.Variants.Should().OnlyContain(v => v.PinnedImageId == null);
    }

    // =========================================================================
    // Publish refusal detail (RULE-14, AC-20)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_PublishingWithAnUnpricedVariant_FailsWithTheNotPublishableKey()
    {
        var (optionTypes, mangoValueId, mintValueId) = FlavourOptions();

        var result = await CreateSut().Handle(
            Command(
                [NewImage(Guid.NewGuid(), 0)],
                optionTypes,
                [Variant(mangoValueId, "ELF-BC5000-MANGO", 24.99m), Variant(mintValueId, "ELF-BC5000-MINT", null)],
                isPublished: true),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductNotPublishableException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_PublishingWithAnUnpricedVariant_NamesThatVariantInTheErrorArgs()
    {
        var (optionTypes, mangoValueId, mintValueId) = FlavourOptions();
        var unpriced = Variant(mintValueId, "ELF-BC5000-MINT", null);

        var result = await CreateSut().Handle(
            Command(
                [NewImage(Guid.NewGuid(), 0)],
                optionTypes,
                [Variant(mangoValueId, "ELF-BC5000-MANGO", 24.99m), unpriced],
                isPublished: true),
            CancellationToken.None);

        result.ErrorArgs.Should().ContainSingle()
            .Which.Should().Be($"{ProductNotPublishableException.VariantPriceTokenPrefix}{unpriced.Id}",
                "the form flags the exact row from this token rather than only saying something is missing");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_SavingAnUnpricedVariantAsADraft_Succeeds()
    {
        var (optionTypes, mangoValueId, mintValueId) = FlavourOptions();

        var result = await CreateSut().Handle(
            Command(
                [NewImage(Guid.NewGuid(), 0)],
                optionTypes,
                [Variant(mangoValueId, "ELF-BC5000-MANGO", 24.99m), Variant(mintValueId, "ELF-BC5000-MINT", null)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("an unpublished product may be saved at any level of completeness (RULE-15)");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-10a (pin a gallery image to a variant): Handle_WithAPinOnAnImageUploadedInTheSameSave_PinsTheVariantToThatImage,
//         Handle_WithEachVariantPinnedToADifferentImage_KeepsThePinsDistinct,
//         Handle_WithAPinOnAnImageUploadedInTheSameSave_DoesNotPersistTheClientsOwnIdentifier
// RULE-13 (a pin resolves only to the product's own gallery): Handle_WithAPinNamingAnImageOutsideTheGallery_LeavesTheVariantUnpinned,
//         Handle_WithNoGalleryAtAll_LeavesAPinnedVariantUnpinned
// AC-20 (publish refused, everything missing listed and flagged per row): Handle_PublishingWithAnUnpricedVariant_FailsWithTheNotPublishableKey,
//         Handle_PublishingWithAnUnpricedVariant_NamesThatVariantInTheErrorArgs
// RULE-15 (a draft may be incomplete): Handle_SavingAnUnpricedVariantAsADraft_Succeeds

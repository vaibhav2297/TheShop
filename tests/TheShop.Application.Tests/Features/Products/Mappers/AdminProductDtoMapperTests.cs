using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Mappers;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Mappers;

/// <summary>
/// Tests for <see cref="AdminProductDtoMapper.ToListItemDto"/> — the <see cref="Product"/> →
/// <see cref="ProductListItemDto"/> projection behind the admin product list's price column
/// (FR-2, AC-1). The projection carries raw amounts; rendering them in the shop's currency is
/// the Web layer's job, so these tests assert numbers, never formatted text.
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class AdminProductDtoMapperTests
{
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private static Category ExampleCategory() =>
        Category.Rehydrate(Guid.NewGuid(), "Disposables");

    private static Brand ExampleBrand() =>
        Brand.Rehydrate(Guid.NewGuid(), "Elf Bar", "elf-bar");

    /// <summary>
    /// Builds a product the way the admin list loads it: the read-optimized rehydration that
    /// carries the DB's precomputed variant read-model columns instead of embedding variant rows.
    /// </summary>
    private static Product BuildProduct(
        ProductPricing? pricing = null,
        bool hasVariants = false,
        Money? minVariantPrice = null) =>
        Product.Rehydrate(
            Guid.NewGuid(),
            "Elf Bar BC5000",
            "A long-lasting disposable vape.",
            "https://example.com/photo.webp",
            Sku.Create("ELF-BAR-BC5000"),
            pricing,
            true,
            ExampleCategory(),
            ExampleBrand(),
            DateTimeOffset.UtcNow,
            hasVariants: hasVariants,
            minVariantPrice: minVariantPrice);

    // =========================================================================
    // Price projection — product prices itself (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToListItemDto_WithoutDiscount_MapsEffectivePriceAsOriginalPrice()
    {
        var product = BuildProduct(ProductPricing.Create(Money.Create(24.99m)));

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.EffectivePrice.Should().Be(24.99m);
        dto.HasVariants.Should().BeFalse();
        dto.MinVariantPrice.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToListItemDto_WithDiscount_MapsEffectivePriceAsSalePrice()
    {
        var product = BuildProduct(ProductPricing.Create(Money.Create(24.99m), Money.Create(19.99m)));

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.EffectivePrice.Should().Be(19.99m);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToListItemDto_WhenProductIsUnpriced_MapsEffectivePriceNull()
    {
        var product = BuildProduct(pricing: null);

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.EffectivePrice.Should().BeNull();
        dto.MinVariantPrice.Should().BeNull();
    }

    // =========================================================================
    // Price projection — product prices through its variants (RULE-19, AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToListItemDto_WithVariants_MapsMinVariantPriceAndLeavesEffectivePriceNull()
    {
        var product = BuildProduct(hasVariants: true, minVariantPrice: Money.Create(12.50m));

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.HasVariants.Should().BeTrue();
        dto.MinVariantPrice.Should().Be(12.50m);
        dto.EffectivePrice.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToListItemDto_WithUnpricedVariants_MapsBothPricesNull()
    {
        var product = BuildProduct(hasVariants: true, minVariantPrice: null);

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.HasVariants.Should().BeTrue();
        dto.MinVariantPrice.Should().BeNull();
        dto.EffectivePrice.Should().BeNull();
    }

    // =========================================================================
    // Currency — taken from whichever price the row actually shows (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToListItemDto_WhenProductPricesItself_MapsCurrencyFromItsOwnPrice()
    {
        var product = BuildProduct(ProductPricing.Create(Money.Create(24.99m, "CAD")));

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.Currency.Should().Be("CAD");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToListItemDto_WhenProductPricesThroughVariants_MapsCurrencyFromTheMinVariantPrice()
    {
        var product = BuildProduct(hasVariants: true, minVariantPrice: Money.Create(12.50m, "USD"));

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.Currency.Should().Be("USD");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToListItemDto_WhenProductIsUnpriced_FallsBackToTheStorefrontCurrency()
    {
        var product = BuildProduct(pricing: null);

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.Currency.Should().Be(Money.DefaultCurrency);
    }

    // =========================================================================
    // ToAdminDto — the full edit payload (AC-6)
    // =========================================================================

    private static Product BuildFullProduct()
    {
        var product = Product.Create(
            "Elf Bar BC5000", "A long-lasting disposable vape.", null,
            Sku.Create("ELF-BC5000"), null, false, ExampleCategory(), ExampleBrand());

        product.SetGallery([new GalleryImageInput(null, "products/photo.png", true)]);
        product.SetOptionTypes([
            new ProductOptionTypeInput(null, "Flavour", [
                new ProductOptionValueInput(null, "Mango"),
                new ProductOptionValueInput(null, "Mint"),
            ]),
        ]);
        product.ApplyVariantConfiguration([]);
        return product;
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToAdminDto_MapsDetailsCategoryAndBrandNames()
    {
        var product = BuildFullProduct();

        var dto = AdminProductDtoMapper.ToAdminDto(product, _fileStorage, "row-version-1");

        dto.Id.Should().Be(product.Id);
        dto.Name.Should().Be("Elf Bar BC5000");
        dto.Sku.Should().Be("ELF-BC5000");
        dto.CategoryName.Should().Be("Disposables");
        dto.BrandName.Should().Be("Elf Bar");
        dto.RowVersion.Should().Be("row-version-1");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToAdminDto_MapsTheGalleryInSavedOrderWithThePrimaryMarked()
    {
        var product = BuildFullProduct();

        var dto = AdminProductDtoMapper.ToAdminDto(product, _fileStorage, "row-version-1");

        dto.Images.Should().ContainSingle();
        dto.Images[0].IsPrimary.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToAdminDto_MapsEachOptionTypeAndItsValues()
    {
        var product = BuildFullProduct();

        var dto = AdminProductDtoMapper.ToAdminDto(product, _fileStorage, "row-version-1");

        dto.OptionTypes.Should().ContainSingle().Which.Name.Should().Be("Flavour");
        dto.OptionTypes[0].Values.Select(v => v.Value).Should().BeEquivalentTo(["Mango", "Mint"]);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToAdminDto_MapsEveryGeneratedVariantWithItsLabel()
    {
        var product = BuildFullProduct();

        var dto = AdminProductDtoMapper.ToAdminDto(product, _fileStorage, "row-version-1");

        dto.Variants.Should().HaveCount(2);
        dto.Variants.Select(v => v.Label).Should().BeEquivalentTo(["Mango", "Mint"]);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void ToAdminDto_WhenAProductHasNoVariants_MapsAnEmptyVariantList()
    {
        var product = BuildProduct(ProductPricing.Create(Money.Create(24.99m)));

        var dto = AdminProductDtoMapper.ToAdminDto(product, _fileStorage, "row-version-1");

        dto.Variants.Should().BeEmpty();
        dto.OptionTypes.Should().BeEmpty();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1 (price column): ToListItemDto_WithoutDiscount_MapsEffectivePriceAsOriginalPrice,
//        ToListItemDto_WithDiscount_MapsEffectivePriceAsSalePrice,
//        ToListItemDto_WhenProductIsUnpriced_MapsEffectivePriceNull,
//        ToListItemDto_WithVariants_MapsMinVariantPriceAndLeavesEffectivePriceNull,
//        ToListItemDto_WithUnpricedVariants_MapsBothPricesNull
// AC-6 (edit form shows everything exactly as saved): ToAdminDto_MapsDetailsCategoryAndBrandNames,
//        ToAdminDto_MapsTheGalleryInSavedOrderWithThePrimaryMarked, ToAdminDto_MapsEachOptionTypeAndItsValues,
//        ToAdminDto_MapsEveryGeneratedVariantWithItsLabel, ToAdminDto_WhenAProductHasNoVariants_MapsAnEmptyVariantList

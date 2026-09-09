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
/// <see cref="ProductListItemDto"/> projection behind the admin manage-products list's price
/// range and variant count (FR-2, FR-17, plan §5 Decision 13). The projection carries raw
/// amounts; rendering them in the shop's currency is the Web layer's job, so these tests assert
/// numbers, never formatted text.
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class AdminProductDtoMapperTests
{
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private static Category ExampleCategory() =>
        Category.Rehydrate(Guid.NewGuid(), "Disposables");

    private static Brand ExampleBrand() =>
        Brand.Rehydrate(Guid.NewGuid(), "Elf Bar", "elf-bar");

    private static Product BuildProduct(ProductPricing? pricing = null) =>
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
            DateTimeOffset.UtcNow);

    /// <summary>
    /// Builds a product carrying one variant per supplied price (<see langword="null"/> for an
    /// unpriced variant), via the same option-type/variant-configuration path the admin edit form
    /// uses — so <see cref="Product.Variants"/> is genuinely populated, matching what the admin
    /// list's aggregate load carries.
    /// </summary>
    private static Product BuildProductWithVariants(params decimal?[] variantPrices)
    {
        var product = Product.Create(
            "Elf Bar BC5000", "A long-lasting disposable vape.", null,
            Sku.Create("ELF-BC5000"), null, false, ExampleCategory(), ExampleBrand());

        var values = variantPrices.Select((_, i) => new ProductOptionValueInput(null, $"V{i}")).ToList();
        product.SetOptionTypes([new ProductOptionTypeInput(null, "Variant", values)]);
        product.ApplyVariantConfiguration([]);

        var priced = product.Variants
            .Select((v, i) => new ProductVariantInput(
                v.Id,
                v.OptionValueIds,
                v.Sku,
                variantPrices[i] is decimal amount ? ProductPricing.Create(Money.Create(amount)) : null,
                true,
                null))
            .ToList();
        product.ApplyVariantConfiguration(priced);

        return product;
    }

    // =========================================================================
    // Price range — product with no variants prices itself (plan §5 Decision 13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToListItemDto_WithNoVariantsAndNoDiscount_MapsBothEndsToTheOriginalPrice()
    {
        var product = BuildProduct(ProductPricing.Create(Money.Create(24.99m)));

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.MinPrice.Should().Be(24.99m);
        dto.MaxPrice.Should().Be(24.99m);
        dto.VariantCount.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToListItemDto_WithNoVariantsAndADiscount_MapsBothEndsToTheSalePrice()
    {
        var product = BuildProduct(ProductPricing.Create(Money.Create(24.99m), Money.Create(19.99m)));

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.MinPrice.Should().Be(19.99m);
        dto.MaxPrice.Should().Be(19.99m);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToListItemDto_WithNoVariantsAndUnpriced_MapsBothEndsNull()
    {
        var product = BuildProduct(pricing: null);

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.MinPrice.Should().BeNull();
        dto.MaxPrice.Should().BeNull();
        dto.VariantCount.Should().Be(0);
    }

    // =========================================================================
    // Price range — product prices through its variants (FR-17, RULE-19)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToListItemDto_WithVariants_MapsTheLowestAndHighestVariantPrice()
    {
        var product = BuildProductWithVariants(10m, 30m);

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.MinPrice.Should().Be(10m);
        dto.MaxPrice.Should().Be(30m);
        dto.VariantCount.Should().Be(2);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToListItemDto_WithEquallyPricedVariants_CollapsesBothEndsToTheOneAmount()
    {
        var product = BuildProductWithVariants(15m, 15m);

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.MinPrice.Should().Be(15m);
        dto.MaxPrice.Should().Be(15m);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToListItemDto_WithUnpricedVariants_MapsBothPricesNullButKeepsTheVariantCount()
    {
        var product = BuildProductWithVariants(null, null);

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.MinPrice.Should().BeNull();
        dto.MaxPrice.Should().BeNull();
        dto.VariantCount.Should().Be(2);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToListItemDto_WithOneUnpricedVariant_IgnoresItWhenComputingTheRange()
    {
        var product = BuildProductWithVariants(10m, null);

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.MinPrice.Should().Be(10m);
        dto.MaxPrice.Should().Be(10m);
        dto.VariantCount.Should().Be(2);
    }

    // =========================================================================
    // Currency — taken from whichever price the row actually shows (AC-24)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToListItemDto_WhenProductPricesItself_MapsCurrencyFromItsOwnPrice()
    {
        var product = BuildProduct(ProductPricing.Create(Money.Create(24.99m, "CAD")));

        var dto = AdminProductDtoMapper.ToListItemDto(product, _fileStorage);

        dto.Currency.Should().Be("CAD");
    }

    [Fact]
    [Trait("Feature", "manage-product")]
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
// AC-24 (variants 10 and 30 with sale to 25 -> CAD 10-25 range, currency): ToListItemDto_WithVariants_MapsTheLowestAndHighestVariantPrice,
//        ToListItemDto_WhenProductPricesItself_MapsCurrencyFromItsOwnPrice
// AC-25 (equal prices collapse to one amount): ToListItemDto_WithEquallyPricedVariants_CollapsesBothEndsToTheOneAmount
// AC-29 (variant count includes unpriced/unavailable variants): ToListItemDto_WithUnpricedVariants_MapsBothPricesNullButKeepsTheVariantCount,
//        ToListItemDto_WithOneUnpricedVariant_IgnoresItWhenComputingTheRange
// Decision 13 (zero-variant product prices itself, both ends collapse): ToListItemDto_WithNoVariantsAndNoDiscount_MapsBothEndsToTheOriginalPrice,
//        ToListItemDto_WithNoVariantsAndADiscount_MapsBothEndsToTheSalePrice, ToListItemDto_WithNoVariantsAndUnpriced_MapsBothEndsNull,
//        ToListItemDto_WhenProductIsUnpriced_FallsBackToTheStorefrontCurrency
// AC-6 (edit form shows everything exactly as saved): ToAdminDto_MapsDetailsCategoryAndBrandNames,
//        ToAdminDto_MapsTheGalleryInSavedOrderWithThePrimaryMarked, ToAdminDto_MapsEachOptionTypeAndItsValues,
//        ToAdminDto_MapsEveryGeneratedVariantWithItsLabel, ToAdminDto_WhenAProductHasNoVariants_MapsAnEmptyVariantList

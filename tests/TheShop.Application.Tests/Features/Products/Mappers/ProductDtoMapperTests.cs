using FluentAssertions;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Mappers;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Mappers;

/// <summary>
/// Tests for <see cref="ProductDtoMapper"/> — the <see cref="Product"/> → <see cref="ProductSummaryDto"/>
/// projection that feeds the product card grid (FR-1, FR-2, FR-3, AC-1, AC-2, AC-11).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class ProductDtoMapperTests
{
    private static Category ExampleCategory() =>
        Category.Create(Guid.NewGuid(), "Disposables", "disposables");

    private static Brand ExampleBrand() =>
        Brand.Create(Guid.NewGuid(), "Elf Bar", "elf-bar");

    private static Product BuildProduct(
        ProductPricing? pricing = null,
        int stockQuantity = 10,
        string? imageUrl = "https://example.com/photo.webp") =>
        Product.Create(
            "Elf Bar BC5000",
            "A long-lasting disposable vape.",
            imageUrl,
            pricing ?? ProductPricing.Create(Money.Create(24.99m)),
            stockQuantity,
            true,
            ExampleCategory(),
            ExampleBrand(),
            "Blue Razz Ice",
            50);

    // =========================================================================
    // Field mapping — happy path (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToSummaryDto_MapsIdNameImageUrlBrandFlavourAndNicotineStrength()
    {
        var product = BuildProduct();

        var dto = ProductDtoMapper.ToSummaryDto(product);

        dto.Id.Should().Be(product.Id);
        dto.Name.Should().Be("Elf Bar BC5000");
        dto.ImageUrl.Should().Be("https://example.com/photo.webp");
        dto.BrandName.Should().Be("Elf Bar");
        dto.Flavour.Should().Be("Blue Razz Ice");
        dto.NicotineStrengthMg.Should().Be(50);
    }

    // =========================================================================
    // Discount projection (AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToSummaryDto_WithDiscountedProduct_MapsSalePriceAndIsDiscountedTrue()
    {
        var pricing = ProductPricing.Create(Money.Create(24.99m), Money.Create(19.99m));
        var product = BuildProduct(pricing);

        var dto = ProductDtoMapper.ToSummaryDto(product);

        dto.IsDiscounted.Should().BeTrue();
        dto.OriginalPrice.Should().Be(24.99m);
        dto.SalePrice.Should().Be(19.99m);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToSummaryDto_WithoutDiscount_MapsSalePriceNullAndIsDiscountedFalse()
    {
        var pricing = ProductPricing.Create(Money.Create(24.99m));
        var product = BuildProduct(pricing);

        var dto = ProductDtoMapper.ToSummaryDto(product);

        dto.IsDiscounted.Should().BeFalse();
        dto.SalePrice.Should().BeNull();
        dto.OriginalPrice.Should().Be(24.99m);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToSummaryDto_MapsCurrencyFromOriginalPrice()
    {
        var pricing = ProductPricing.Create(Money.Create(24.99m, "CAD"));
        var product = BuildProduct(pricing);

        var dto = ProductDtoMapper.ToSummaryDto(product);

        dto.Currency.Should().Be("CAD");
    }

    // =========================================================================
    // Stock availability (AC-11 out-of-stock edge case)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToSummaryDto_WhenOutOfStock_MapsIsInStockFalse()
    {
        var product = BuildProduct(stockQuantity: 0);

        var dto = ProductDtoMapper.ToSummaryDto(product);

        dto.IsInStock.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToSummaryDto_WhenInStock_MapsIsInStockTrue()
    {
        var product = BuildProduct(stockQuantity: 5);

        var dto = ProductDtoMapper.ToSummaryDto(product);

        dto.IsInStock.Should().BeTrue();
    }

    // =========================================================================
    // Missing image (AC-12 placeholder edge case)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToSummaryDto_WhenProductHasNoImage_MapsImageUrlAsNull()
    {
        var product = BuildProduct(imageUrl: null);

        var dto = ProductDtoMapper.ToSummaryDto(product);

        dto.ImageUrl.Should().BeNull();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: ToSummaryDto_MapsIdNameImageUrlBrandFlavourAndNicotineStrength
// AC-2: ToSummaryDto_WithDiscountedProduct_MapsSalePriceAndIsDiscountedTrue,
//        ToSummaryDto_WithoutDiscount_MapsSalePriceNullAndIsDiscountedFalse
// AC-11: ToSummaryDto_WhenOutOfStock_MapsIsInStockFalse, ToSummaryDto_WhenInStock_MapsIsInStockTrue
// AC-12: ToSummaryDto_WhenProductHasNoImage_MapsImageUrlAsNull

using FluentAssertions;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for the <see cref="ProductPricing"/> value object — the discount rule behind
/// spec constraint "sale price is always prominent, original price (MRP) struck through;
/// no discount → single price only" (FR-3, AC-2).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class ProductPricingTests
{
    private static Money Cad(decimal amount) => Money.Create(amount, "CAD");

    // =========================================================================
    // Create — no discount (AC-2: single price only)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithoutSalePrice_IsDiscountedIsFalse()
    {
        var pricing = ProductPricing.Create(Cad(24.99m));

        pricing.IsDiscounted.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithoutSalePrice_EffectiveIsOriginalPrice()
    {
        var pricing = ProductPricing.Create(Cad(24.99m));

        pricing.Effective.Should().Be(Cad(24.99m));
    }

    // =========================================================================
    // Create — discounted (AC-2: sale price prominent, original struck through)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithSalePriceBelowOriginal_IsDiscountedIsTrue()
    {
        var pricing = ProductPricing.Create(Cad(24.99m), Cad(19.99m));

        pricing.IsDiscounted.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithSalePriceBelowOriginal_EffectiveIsSalePrice()
    {
        var pricing = ProductPricing.Create(Cad(24.99m), Cad(19.99m));

        pricing.Effective.Should().Be(Cad(19.99m));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithSalePriceBelowOriginal_OriginalPriceIsRetainedForStrikethrough()
    {
        var pricing = ProductPricing.Create(Cad(24.99m), Cad(19.99m));

        pricing.OriginalPrice.Should().Be(Cad(24.99m));
    }

    // =========================================================================
    // Create — boundary: sale price equal to original
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithSalePriceEqualToOriginal_DoesNotThrow()
    {
        var act = () => ProductPricing.Create(Cad(24.99m), Cad(24.99m));

        act.Should().NotThrow();
    }

    // =========================================================================
    // Create — invariant: sale price must not exceed original
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithSalePriceAboveOriginal_ThrowsDomainException()
    {
        var act = () => ProductPricing.Create(Cad(19.99m), Cad(24.99m));

        act.Should().Throw<DomainException>()
           .Which.MessageKey.Should().Be("Product_Pricing_SaleAboveOriginal");
    }

    // =========================================================================
    // Equality — value semantics
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Equals_WithSameOriginalAndSalePrice_ReturnsTrue()
    {
        var a = ProductPricing.Create(Cad(24.99m), Cad(19.99m));
        var b = ProductPricing.Create(Cad(24.99m), Cad(19.99m));

        a.Should().Be(b);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Equals_OneWithSalePriceOneWithout_ReturnsFalse()
    {
        var a = ProductPricing.Create(Cad(24.99m), Cad(19.99m));
        var b = ProductPricing.Create(Cad(24.99m));

        a.Should().NotBe(b);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-2: Create_WithSalePriceBelowOriginal_IsDiscountedIsTrue,
//        Create_WithSalePriceBelowOriginal_EffectiveIsSalePrice,
//        Create_WithSalePriceBelowOriginal_OriginalPriceIsRetainedForStrikethrough,
//        Create_WithoutSalePrice_IsDiscountedIsFalse, Create_WithoutSalePrice_EffectiveIsOriginalPrice

using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="Product"/> aggregate — discount projection (<see cref="Product.IsDiscounted"/>,
/// <see cref="Product.EffectivePrice"/>) and stock availability (<see cref="Product.IsInStock"/>),
/// which back FR-3 (AC-2) and the out-of-stock edge case (AC-11).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class ProductTests
{
    private static Category ExampleCategory() =>
        Category.Rehydrate(Guid.NewGuid(), "Disposables");

    private static Brand ExampleBrand() =>
        Brand.Rehydrate(Guid.NewGuid(), "Elf Bar", "elf-bar");

    private static Product BuildProduct(
        ProductPricing? pricing = null,
        int stockQuantity = 10,
        bool isPublished = true) =>
        Product.Create(
            "Elf Bar BC5000",
            "A long-lasting disposable vape.",
            "https://example.com/photo.webp",
            pricing ?? ProductPricing.Create(Money.Create(24.99m)),
            stockQuantity,
            isPublished,
            ExampleCategory(),
            ExampleBrand(),
            "Blue Razz Ice",
            50);

    // =========================================================================
    // Create — happy path (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithValidData_ReturnsProductWithSuppliedProperties()
    {
        var product = BuildProduct();

        product.Name.Should().Be("Elf Bar BC5000");
        product.ImageUrl.Should().Be("https://example.com/photo.webp");
        product.Flavour.Should().Be("Blue Razz Ice");
        product.NicotineStrengthMg.Should().Be(50);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_CalledTwice_AssignsDifferentIds()
    {
        var first = BuildProduct();
        var second = BuildProduct();

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithoutExplicitCreatedAt_AssignsUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var product = BuildProduct();
        var after = DateTimeOffset.UtcNow;

        product.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithExplicitCreatedAt_UsesSuppliedTimestamp()
    {
        var fixedTime = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var product = Product.Create(
            "Elf Bar BC5000", "desc", null,
            ProductPricing.Create(Money.Create(24.99m)),
            10, true, ExampleCategory(), ExampleBrand(), null, null, fixedTime);

        product.CreatedAt.Should().Be(fixedTime);
    }

    // =========================================================================
    // IsDiscounted / EffectivePrice — projected from ProductPricing (AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void IsDiscounted_WhenPricingHasSalePrice_ReturnsTrue()
    {
        var pricing = ProductPricing.Create(Money.Create(24.99m), Money.Create(19.99m));
        var product = BuildProduct(pricing);

        product.IsDiscounted.Should().BeTrue();
        product.EffectivePrice.Should().Be(Money.Create(19.99m));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void IsDiscounted_WhenPricingHasNoSalePrice_ReturnsFalse()
    {
        var pricing = ProductPricing.Create(Money.Create(24.99m));
        var product = BuildProduct(pricing);

        product.IsDiscounted.Should().BeFalse();
        product.EffectivePrice.Should().Be(Money.Create(24.99m));
    }

    // =========================================================================
    // IsInStock — stock availability (AC-11 out-of-stock edge case)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void IsInStock_WhenStockQuantityIsPositive_ReturnsTrue()
    {
        var product = BuildProduct(stockQuantity: 1);

        product.IsInStock.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void IsInStock_WhenStockQuantityIsZero_ReturnsFalse()
    {
        // Boundary: zero stock is the out-of-stock case (AC-11).
        var product = BuildProduct(stockQuantity: 0);

        product.IsInStock.Should().BeFalse();
    }

    // =========================================================================
    // Rehydrate — reconstruction from persisted data without re-running invariants
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Rehydrate_PreservesSuppliedId()
    {
        var id = Guid.NewGuid();

        var product = Product.Rehydrate(
            id, "Elf Bar BC5000", "desc", null,
            ProductPricing.Create(Money.Create(24.99m)),
            10, true, ExampleCategory(), ExampleBrand(), null, null,
            DateTimeOffset.UtcNow);

        product.Id.Should().Be(id);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Rehydrate_WithZeroStock_IsInStockIsFalse()
    {
        var product = Product.Rehydrate(
            Guid.NewGuid(), "SMOK Nord 5", "desc", null,
            ProductPricing.Create(Money.Create(39.99m)),
            0, true, ExampleCategory(), ExampleBrand(), null, null,
            DateTimeOffset.UtcNow);

        product.IsInStock.Should().BeFalse();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Create_WithValidData_ReturnsProductWithSuppliedProperties
// AC-2: IsDiscounted_WhenPricingHasSalePrice_ReturnsTrue, IsDiscounted_WhenPricingHasNoSalePrice_ReturnsFalse
// AC-11: IsInStock_WhenStockQuantityIsZero_ReturnsFalse, IsInStock_WhenStockQuantityIsPositive_ReturnsTrue,
//         Rehydrate_WithZeroStock_IsInStockIsFalse

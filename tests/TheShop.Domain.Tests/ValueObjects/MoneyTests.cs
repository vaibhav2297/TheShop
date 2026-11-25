using FluentAssertions;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for the <see cref="Money"/> value object. Backs the catalogue's price display
/// (spec constraint: "Prices are shown in Canadian dollars (CAD)").
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class MoneyTests
{
    // =========================================================================
    // Create — happy path
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithNonNegativeAmount_ReturnsMoneyWithSuppliedAmountAndCurrency()
    {
        var money = Money.Create(24.99m, "CAD");

        money.Amount.Should().Be(24.99m);
        money.Currency.Should().Be("CAD");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithoutCurrency_DefaultsToCad()
    {
        var money = Money.Create(10m);

        money.Currency.Should().Be(Money.DefaultCurrency);
        money.Currency.Should().Be("CAD");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithZeroAmount_Succeeds()
    {
        // Boundary: zero is the smallest valid amount.
        var act = () => Money.Create(0m);

        act.Should().NotThrow();
    }

    // =========================================================================
    // Create — validation
    // =========================================================================

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithNegativeAmount_ThrowsDomainException(decimal amount)
    {
        var act = () => Money.Create(amount);

        act.Should().Throw<DomainException>()
           .Which.MessageKey.Should().Be("Money_Negative");
    }

    // =========================================================================
    // Equality — value semantics
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Equals_WithSameAmountAndCurrency_ReturnsTrue()
    {
        var a = Money.Create(19.99m, "CAD");
        var b = Money.Create(19.99m, "CAD");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Equals_WithDifferentCurrencyCase_ReturnsTrue()
    {
        // Currency equality is case-insensitive.
        var a = Money.Create(19.99m, "CAD");
        var b = Money.Create(19.99m, "cad");

        a.Should().Be(b);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Equals_WithDifferentAmount_ReturnsFalse()
    {
        var a = Money.Create(19.99m);
        var b = Money.Create(24.99m);

        a.Should().NotBe(b);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Equals_WithDifferentCurrency_ReturnsFalse()
    {
        var a = Money.Create(19.99m, "CAD");
        var b = Money.Create(19.99m, "USD");

        a.Should().NotBe(b);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-2 (supporting): Money is the amount type ProductPricing composes; its non-negative
//        invariant and CAD default back every price shown on the catalogue grid.

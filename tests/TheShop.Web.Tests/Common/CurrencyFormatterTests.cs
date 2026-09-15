using System.Globalization;
using FluentAssertions;
using TheShop.Web.Common;
using Xunit;

namespace TheShop.Web.Tests.Common;

/// <summary>
/// Tests for <see cref="CurrencyFormatter"/> and the storefront's fixed English Canadian
/// currency format.
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class CurrencyFormatterTests
{
    private static readonly CultureInfo OriginalUiCulture = CultureInfo.CurrentUICulture;

    private static void UseCulture(string cultureName) =>
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

    private static void RestoreCulture() => CultureInfo.CurrentUICulture = OriginalUiCulture;

    [Theory]
    [InlineData("en-CA")]
    [InlineData("fr-CA")]
    [InlineData("de-DE")]
    [Trait("Feature", "remove-french")]
    public void Format_UnderAnyUiCulture_UsesEnglishCanadianFormat(string cultureName)
    {
        UseCulture(cultureName);
        try
        {
            CurrencyFormatter.Format(24.99m).Should().Be("$24.99");
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Format_WithTheDefaultCurrency_RendersEnglishCanadianSymbol()
    {
        CurrencyFormatter.Format(24.99m, "CAD").Should().Be(CurrencyFormatter.Format(24.99m));
    }

    [Theory]
    [InlineData("cad")]
    [InlineData("  CAD  ")]
    [InlineData("")]
    [Trait("Feature", "create-product")]
    public void Format_WithADefaultCurrencyVariant_IsTreatedAsTheDefault(string currency)
    {
        CurrencyFormatter.Format(24.99m, currency).Should().Be(CurrencyFormatter.Format(24.99m));
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Format_WithANonDefaultCurrency_UsesEnglishCanadianLayout()
    {
        var formatted = CurrencyFormatter.Format(24.99m, "USD");

        formatted.Should().StartWith("USD").And.Contain("24.99").And.NotContain("$");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Format_WithAnUnknownCurrency_DoesNotThrow()
    {
        var format = () => CurrencyFormatter.Format(24.99m, "XYZ");

        format.Should().NotThrow();
    }

    [Theory]
    [InlineData("en-CA")]
    [InlineData("fr-CA")]
    [Trait("Feature", "remove-french")]
    public void SymbolLeadsAmount_UnderAnyUiCulture_IsTrue(string cultureName)
    {
        UseCulture(cultureName);
        try
        {
            CurrencyFormatter.SymbolLeadsAmount.Should().BeTrue();
            CurrencyFormatter.Format(24.99m).Should().StartWith(CurrencyFormatter.Symbol);
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Theory]
    [InlineData("en-CA")]
    [InlineData("fr-CA")]
    [InlineData("de-DE")]
    [Trait("Feature", "remove-french")]
    public void Culture_UnderAnyUiCulture_IsEnglishCanadian(string cultureName)
    {
        UseCulture(cultureName);
        try
        {
            CurrencyFormatter.Culture.Name.Should().Be("en-CA");
            CurrencyFormatter.Culture.NumberFormat.NumberDecimalSeparator.Should().Be(".");
        }
        finally
        {
            RestoreCulture();
        }
    }
}

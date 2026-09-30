using System.Globalization;
using FluentAssertions;
using TheShop.Web.Common;
using Xunit;

namespace TheShop.Web.Tests.Common;

/// <summary>
/// Tests Canadian English price formatting, regardless of browser culture.
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class CurrencyFormatterTests
{
    private static readonly CultureInfo OriginalUiCulture = CultureInfo.CurrentUICulture;

    private static void UseCulture(string cultureName) =>
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

    private static void RestoreCulture() => CultureInfo.CurrentUICulture = OriginalUiCulture;

    // =========================================================================
    // English (en-CA)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Format_UnderEnglishCulture_RendersDollarSignPrefixed()
    {
        UseCulture("en-CA");
        try
        {
            var formatted = CurrencyFormatter.Format(24.99m);

            formatted.Should().StartWith("$");
            formatted.Should().Contain("24.99");
        }
        finally
        {
            RestoreCulture();
        }
    }

    // =========================================================================
    // Other UI cultures use Canadian English (en-CA)
    // =========================================================================

    [Theory]
    [InlineData("fr-CA")]
    [InlineData("de-DE")]
    [Trait("Feature", "product-catalogue")]
    public void Format_UnderAnotherCulture_UsesEnglishFormat(string cultureName)
    {
        UseCulture(cultureName);
        try
        {
            var formatted = CurrencyFormatter.Format(24.99m);

            formatted.Should().StartWith("$");
        }
        finally
        {
            RestoreCulture();
        }
    }

    // =========================================================================
    // Currency selects the symbol; Canadian English owns the layout
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Format_WithTheDefaultCurrency_RendersTheCultureNativeSymbol()
    {
        UseCulture("en-CA");
        try
        {
            CurrencyFormatter.Format(24.99m, "CAD").Should().Be(CurrencyFormatter.Format(24.99m));
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Theory]
    [InlineData("cad")]
    [InlineData("  CAD  ")]
    [InlineData("")]
    [Trait("Feature", "create-product")]
    public void Format_WithADefaultCurrencyVariant_IsTreatedAsTheDefault(string currency)
    {
        UseCulture("en-CA");
        try
        {
            CurrencyFormatter.Format(24.99m, currency).Should().Be(CurrencyFormatter.Format(24.99m));
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Format_WithANonDefaultCurrency_RendersTheCodeRatherThanBorrowingTheDollarSign()
    {
        UseCulture("en-CA");
        try
        {
            var formatted = CurrencyFormatter.Format(24.99m, "USD");

            formatted.Should().Contain("USD");
            formatted.Should().NotContain("$", "a USD amount must never be shown as if it were CAD");
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Format_WithAnUnknownCurrency_DoesNotThrow()
    {
        UseCulture("en-CA");
        try
        {
            // Currency codes arrive from the database; an unexpected one must degrade to a
            // readable string rather than take down the render.
            var format = () => CurrencyFormatter.Format(24.99m, "XYZ");

            format.Should().NotThrow();
        }
        finally
        {
            RestoreCulture();
        }
    }

    // =========================================================================
    // Adorning an editable money input — the amount is a bare decimal, so the
    // symbol and its side are resolved separately (create-product AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void SymbolLeadsAmount_UnderEnglishCulture_IsTrue()
    {
        UseCulture("en-CA");
        try
        {
            CurrencyFormatter.SymbolLeadsAmount.Should().BeTrue("en-CA writes $12.99");
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SymbolLeadsAmount_AgreesWithWhereFormatActuallyPutsTheSymbol()
    {
        foreach (var culture in new[] { "en-CA", "de-DE" })
        {
            UseCulture(culture);
            try
            {
                var leads = CurrencyFormatter.SymbolLeadsAmount;
                var formatted = CurrencyFormatter.Format(24.99m);

                formatted.StartsWith(CurrencyFormatter.Symbol, StringComparison.Ordinal)
                    .Should().Be(leads, $"the adornment side must match {culture} rendered output");
            }
            finally
            {
                RestoreCulture();
            }
        }
    }

}

using System.Globalization;
using FluentAssertions;
using TheShop.Web.Common;
using Xunit;

namespace TheShop.Web.Tests.Common;

/// <summary>
/// Tests for <see cref="CurrencyFormatter"/> — culture-aware CAD price formatting behind spec
/// constraint "Prices are shown in Canadian dollars (CAD) using the site's currency format, in
/// both languages" (FR-3, AC-2, AC-13).
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
    // French (fr-CA) — AC-13: catalogue text follows the active site language
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Format_UnderFrenchCulture_RendersDollarSignSuffixed()
    {
        UseCulture("fr-CA");
        try
        {
            var formatted = CurrencyFormatter.Format(24.99m);

            formatted.Should().NotStartWith("$");
            formatted.Should().Contain("$");
            formatted.Should().Contain("24,99"); // fr-CA uses a comma decimal separator
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Format_SameAmountUnderEnglishAndFrenchCultures_ProducesDifferentText()
    {
        UseCulture("en-CA");
        var en = CurrencyFormatter.Format(24.99m);
        UseCulture("fr-CA");
        var fr = CurrencyFormatter.Format(24.99m);
        RestoreCulture();

        en.Should().NotBe(fr);
    }

    // =========================================================================
    // Culture other than English/French defaults to English (en-CA)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Format_UnderAnUnsupportedCulture_FallsBackToEnglishFormat()
    {
        UseCulture("de-DE");
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
    // Currency selects the symbol; culture still owns the layout
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
    public void Format_WithANonDefaultCurrency_KeepsTheActiveCultureLayout()
    {
        UseCulture("fr-CA");
        try
        {
            var formatted = CurrencyFormatter.Format(24.99m, "USD");

            formatted.Should().Contain("24,99", "the page's decimal separator still applies");
            formatted.Should().NotStartWith("USD", "fr-CA writes the symbol after the amount");
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
    public void SymbolLeadsAmount_UnderFrenchCulture_IsFalse()
    {
        UseCulture("fr-CA");
        try
        {
            CurrencyFormatter.SymbolLeadsAmount.Should().BeFalse("fr-CA writes 12,99 $");
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
        foreach (var culture in new[] { "en-CA", "fr-CA" })
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

    [Fact]
    [Trait("Feature", "create-product")]
    public void Culture_FollowsTheActiveUiLanguage()
    {
        UseCulture("fr-CA");
        try
        {
            // The editable field parses and renders through this culture, so its decimal
            // separator must be the page's, not the browser's.
            CurrencyFormatter.Culture.NumberFormat.NumberDecimalSeparator.Should().Be(",");
        }
        finally
        {
            RestoreCulture();
        }
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-13 (price-formatting subset only): Format_UnderFrenchCulture_RendersDollarSignSuffixed,
//        Format_SameAmountUnderEnglishAndFrenchCultures_ProducesDifferentText
// Note: full AC-13 (every label/button/filter/sort string in both languages) is a
// resx-completeness concern verified by /theshop.review's French-localization gate, not by
// unit/component tests — see the coverage summary.

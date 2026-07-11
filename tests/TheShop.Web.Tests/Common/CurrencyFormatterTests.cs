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
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-13 (price-formatting subset only): Format_UnderFrenchCulture_RendersDollarSignSuffixed,
//        Format_SameAmountUnderEnglishAndFrenchCultures_ProducesDifferentText
// Note: full AC-13 (every label/button/filter/sort string in both languages) is a
// resx-completeness concern verified by /theshop.review's French-localization gate, not by
// unit/component tests — see the coverage summary.

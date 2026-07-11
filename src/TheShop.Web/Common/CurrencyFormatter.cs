using System.Globalization;

namespace TheShop.Web.Common;

/// <summary>
/// Formats CAD amounts for display, honouring the active UI culture so English pages
/// render <c>$12.99</c> and French pages render <c>12,99 $</c> without any format
/// literals appearing in markup.
/// </summary>
public static class CurrencyFormatter
{
    private static readonly CultureInfo EnCa = CultureInfo.GetCultureInfo("en-CA");
    private static readonly CultureInfo FrCa = CultureInfo.GetCultureInfo("fr-CA");

    /// <summary>
    /// Formats <paramref name="amount"/> as a currency string using the Canadian
    /// English or French culture that matches <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public static string Format(decimal amount) =>
        amount.ToString("C", ResolveCulture());

    private static CultureInfo ResolveCulture() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fr", StringComparison.OrdinalIgnoreCase)
            ? FrCa
            : EnCa;
}

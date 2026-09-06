using System.Globalization;
using TheShop.Domain.ValueObjects;

namespace TheShop.Web.Common;

/// <summary>
/// Formats monetary amounts for display, honouring the active UI culture so English pages
/// render <c>$12.99</c> and French pages render <c>12,99 $</c> without any format
/// literals appearing in markup.
/// </summary>
/// <remarks>
/// Culture and currency answer different halves of the question and are resolved separately:
/// the culture decides the layout — decimal separator, grouping, and which side the symbol sits on
/// — while the currency decides only which symbol appears.
/// </remarks>
public static class CurrencyFormatter
{
    private static readonly CultureInfo EnCa = CultureInfo.GetCultureInfo("en-CA");
    private static readonly CultureInfo FrCa = CultureInfo.GetCultureInfo("fr-CA");

    /// <summary>
    /// The Canadian culture matching the active UI language. Hand this to any component that
    /// parses or renders a money value itself — an editable numeric field, for instance — so its
    /// decimal separator matches the rest of the page rather than the browser's culture.
    /// </summary>
    public static CultureInfo Culture => ResolveCulture();

    /// <summary>
    /// The currency symbol for the active UI culture, for adorning an editable money input whose
    /// bound value is a plain <see cref="decimal"/> and so cannot carry its own formatting.
    /// </summary>
    public static string Symbol => ResolveCulture().NumberFormat.CurrencySymbol;

    /// <summary>
    /// <c>true</c> when the active culture writes the symbol before the amount (en-CA <c>$12.99</c>),
    /// <c>false</c> when it writes it after (fr-CA <c>12,99 $</c>). Callers that adorn an input use
    /// this to place the symbol on the correct side; the underlying
    /// <see cref="NumberFormatInfo.CurrencyPositivePattern"/> values are
    /// <c>0 = $n</c>, <c>1 = n$</c>, <c>2 = $ n</c>, <c>3 = n $</c>.
    /// </summary>
    public static bool SymbolLeadsAmount =>
        ResolveCulture().NumberFormat.CurrencyPositivePattern is 0 or 2;

    /// <summary>
    /// Formats <paramref name="amount"/> as a currency string, laid out by the Canadian English or
    /// French culture that matches <see cref="CultureInfo.CurrentUICulture"/> and carrying the
    /// symbol for <paramref name="currency"/>.
    /// </summary>
    /// <param name="amount">The monetary amount.</param>
    /// <param name="currency">
    /// The ISO currency code the amount is denominated in; defaults to
    /// <see cref="Money.DefaultCurrency"/>. A code the storefront has no symbol for is rendered as
    /// the code itself (<c>USD 12.99</c>) rather than borrowing the Canadian <c>$</c>, so an amount
    /// is never shown in a currency it is not actually in.
    /// </param>
    public static string Format(decimal amount, string currency = Money.DefaultCurrency)
    {
        var culture = ResolveCulture();

        if (IsDefaultCurrency(currency))
            return amount.ToString("C", culture);

        // Keep the culture's layout and swap only the symbol, so a non-CAD amount still reads
        // with the page's separators and symbol placement.
        var format = (NumberFormatInfo)culture.NumberFormat.Clone();
        format.CurrencySymbol = currency.Trim().ToUpperInvariant();

        return amount.ToString("C", format);
    }

    private static bool IsDefaultCurrency(string currency) =>
        string.IsNullOrWhiteSpace(currency) ||
        currency.Trim().Equals(Money.DefaultCurrency, StringComparison.OrdinalIgnoreCase);

    private static CultureInfo ResolveCulture() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fr", StringComparison.OrdinalIgnoreCase)
            ? FrCa
            : EnCa;
}

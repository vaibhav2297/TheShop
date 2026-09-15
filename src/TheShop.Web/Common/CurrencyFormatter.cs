using System.Globalization;
using TheShop.Domain.ValueObjects;

namespace TheShop.Web.Common;

/// <summary>
/// Formats monetary amounts for display using the storefront's English Canadian format without
/// format literals appearing in markup.
/// </summary>
/// <remarks>
/// Culture defines the layout — decimal separator, grouping, and symbol placement — while the
/// currency defines only which symbol appears.
/// </remarks>
public static class CurrencyFormatter
{
    private static readonly CultureInfo EnCa = CultureInfo.GetCultureInfo("en-CA");
    /// <summary>
    /// The storefront culture. Hand this to a component that parses or renders a money value
    /// itself so its decimal separator matches the rest of the page rather than browser culture.
    /// </summary>
    public static CultureInfo Culture => EnCa;

    /// <summary>
    /// The currency symbol for adorning an editable money input whose
    /// bound value is a plain <see cref="decimal"/> and so cannot carry its own formatting.
    /// </summary>
    public static string Symbol => EnCa.NumberFormat.CurrencySymbol;

    /// <summary>
    /// <c>true</c> because English Canadian writes the symbol before the amount (for example,
    /// <c>$12.99</c>). Callers that adorn an input use this to place the symbol on the correct
    /// side; the underlying
    /// <see cref="NumberFormatInfo.CurrencyPositivePattern"/> values are
    /// <c>0 = $n</c>, <c>1 = n$</c>, <c>2 = $ n</c>, <c>3 = n $</c>.
    /// </summary>
    public static bool SymbolLeadsAmount =>
        EnCa.NumberFormat.CurrencyPositivePattern is 0 or 2;

    /// <summary>
    /// Formats <paramref name="amount"/> as an English Canadian currency string carrying the
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
        if (IsDefaultCurrency(currency))
            return amount.ToString("C", EnCa);

        // Keep storefront layout and swap only symbol, so a non-CAD amount never reads as CAD.
        var format = (NumberFormatInfo)EnCa.NumberFormat.Clone();
        format.CurrencySymbol = currency.Trim().ToUpperInvariant();

        return amount.ToString("C", format);
    }

    private static bool IsDefaultCurrency(string currency) =>
        string.IsNullOrWhiteSpace(currency) ||
        currency.Trim().Equals(Money.DefaultCurrency, StringComparison.OrdinalIgnoreCase);
}

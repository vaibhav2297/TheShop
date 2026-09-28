using System.Globalization;
using TheShop.Domain.ValueObjects;

namespace TheShop.Web.Common;

/// <summary>
/// Formats monetary amounts for display using Canadian English conventions.
/// </summary>
/// <remarks>
/// Canadian English determines the layout; the currency determines the symbol.
/// </remarks>
public static class CurrencyFormatter
{
    private static readonly CultureInfo EnCa = CultureInfo.GetCultureInfo("en-CA");

    /// <summary>
    /// Canadian English culture for money inputs and display, independent of browser culture.
    /// </summary>
    public static CultureInfo Culture => EnCa;

    /// <summary>
    /// The Canadian currency symbol, for adorning an editable money input whose
    /// bound value is a plain <see cref="decimal"/> and so cannot carry its own formatting.
    /// </summary>
    public static string Symbol => EnCa.NumberFormat.CurrencySymbol;

    /// <summary>
    /// Whether Canadian English places the currency symbol before the amount.
    /// </summary>
    public static bool SymbolLeadsAmount =>
        EnCa.NumberFormat.CurrencyPositivePattern is 0 or 2;

    /// <summary>
    /// Formats <paramref name="amount"/> using Canadian English layout and the symbol for
    /// <paramref name="currency"/>.
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
        var culture = EnCa;

        if (IsDefaultCurrency(currency))
            return amount.ToString("C", culture);

        // Keep Canadian English layout and swap only the symbol.
        var format = (NumberFormatInfo)culture.NumberFormat.Clone();
        format.CurrencySymbol = currency.Trim().ToUpperInvariant();

        return amount.ToString("C", format);
    }

    private static bool IsDefaultCurrency(string currency) =>
        string.IsNullOrWhiteSpace(currency) ||
        currency.Trim().Equals(Money.DefaultCurrency, StringComparison.OrdinalIgnoreCase);

}

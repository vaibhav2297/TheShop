using Microsoft.AspNetCore.Components;
using MudBlazor;
using TheShop.Web.Common;

namespace TheShop.Web.Components.Common;

/// <summary>
/// An editable monetary amount. Wraps <see cref="MudNumericField{T}"/> rather than extending it so
/// the pieces that make a field "money" — the two-decimal format, the culture that drives the
/// decimal separator, and the currency symbol adorning the correct side of the input — are settled
/// here and cannot be overridden per call site.
/// </summary>
/// <remarks>
/// The bound value is a plain <see cref="decimal"/>, so the symbol cannot travel with it the way it
/// does in read-only display (<see cref="CurrencyFormatter.Format(decimal)"/>); it is rendered as an
/// adornment instead, placed by <see cref="CurrencyFormatter.SymbolLeadsAmount"/>.
/// A <c>Min</c> of zero mirrors the domain rule that <c>Money</c> refuses a negative amount,
/// catching it in the field rather than as an exception at save time.
/// </remarks>
public partial class ShopMoneyField : MudComponentBase
{
    /// <summary>
    /// Two decimal places, matching how the amount reads back in
    /// <see cref="CurrencyFormatter.Format(decimal)"/> once saved.
    /// </summary>
    private const string MoneyFormat = "N2";

    /// <summary>The bound amount. <c>null</c> means the field has not been filled in yet.</summary>
    [Parameter]
    public decimal? Value { get; set; }

    /// <summary>Raised when the user changes the amount.</summary>
    [Parameter]
    public EventCallback<decimal?> ValueChanged { get; set; }

    /// <summary>The label shown in the field outline.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Marks the field required for its enclosing <see cref="MudForm"/>.</summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>The validation message shown when a <see cref="Required"/> field is left empty.</summary>
    [Parameter]
    public string? RequiredError { get; set; }

    /// <summary>
    /// Puts the field in its error state independently of <see cref="MudForm"/> validation, for a
    /// problem the form only learns about elsewhere — a rule spanning several fields, or a failure
    /// the server reported. <see cref="ErrorText"/> supplies the message.
    /// </summary>
    [Parameter]
    public bool Error { get; set; }

    /// <summary>The message shown while <see cref="Error"/> is set.</summary>
    [Parameter]
    public string? ErrorText { get; set; }

    /// <summary>Disables input, typically while the surrounding form is saving.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>The typography of the entered amount. Defaults to <see cref="MudBlazor.Typo.body1"/>.</summary>
    [Parameter]
    public Typo Typo { get; set; } = Typo.body1;

    /// <summary>
    /// The field's vertical density — <see cref="MudBlazor.Margin.Dense"/> for a field sitting in a
    /// table row.
    /// </summary>
    [Parameter]
    public Margin Margin { get; set; } = Margin.None;

    /// <summary>Whether the field fills its container. Defaults to <c>true</c>.</summary>
    [Parameter]
    public bool FullWidth { get; set; } = true;

    /// <summary>
    /// Which side of the input carries the currency symbol, following the active culture.
    /// </summary>
    private static Adornment SymbolPlacement =>
        CurrencyFormatter.SymbolLeadsAmount ? Adornment.Start : Adornment.End;
}

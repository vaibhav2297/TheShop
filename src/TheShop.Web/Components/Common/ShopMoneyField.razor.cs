using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using TheShop.Web.Common;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>A nonnegative, nullable CAD amount using the shared numeric editor. Attributes target its input.</summary>
public partial class ShopMoneyField : ComponentBase
{
    /// <summary>The committed amount; null means no price is entered.</summary>
    [Parameter] public decimal? Value { get; set; }

    /// <summary>Raised only when a valid edit is committed.</summary>
    [Parameter] public EventCallback<decimal?> ValueChanged { get; set; }

    /// <summary>Identifies the owning model field for binding and form validation.</summary>
    [Parameter, EditorRequired] public Expression<Func<decimal?>> ValueExpression { get; set; } = default!;

    /// <summary>Visible floating label; otherwise supply an accessible name through attributes.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Optional localized description associated with the input.</summary>
    [Parameter] public string? HelperText { get; set; }

    /// <summary>Rejects an empty amount when the owning form requires a price.</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Localized message for a missing required amount.</summary>
    [Parameter] public string? RequiredError { get; set; }

    /// <summary>Displays the owning form's external validation error.</summary>
    [Parameter] public bool Error { get; set; }

    /// <summary>Localized external validation message.</summary>
    [Parameter] public string? ErrorText { get; set; }

    /// <summary>Prevents editing and value callbacks.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Optional row-owned draft retained across virtualization.</summary>
    [Parameter] public ShopNumericDraft DraftState { get; set; } = new();

    /// <summary>Native attributes, including lowercase class/style, target the actual input.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ShopNumericField _field = default!;

    private static string FormatMoney(decimal value) => CurrencyFormatter.Format(value);

    /// <summary>Commits pending edits and awaits their callbacks before the owning form validates and saves.</summary>
    public Task<bool> ValidateAsync() => _field.ValidateAsync();
}

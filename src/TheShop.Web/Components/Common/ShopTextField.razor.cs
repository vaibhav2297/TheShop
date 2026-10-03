using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>An outlined, label-only text field with immediate binding and EditContext validation. Native attributes and CSS classes target its input.</summary>
public partial class ShopTextField : InputBase<string?>
{
    private readonly string _generatedId = $"shop-field-{Guid.NewGuid():N}";

    /// <summary>Required resource-backed floating label and accessible name; overrides unmatched ARIA naming attributes.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;

    /// <summary>Optional trusted decorative SVG fragment from ShopIcons, displayed before the input.</summary>
    [Parameter] public string? StartIcon { get; set; }

    /// <summary>Optional resource-backed hint, displayed below the field and associated with its input.</summary>
    [Parameter] public string? HelperText { get; set; }

    /// <summary>Prevents native editing and input-event updates. Supply a BusyFor value when an operation disables the field.</summary>
    [Parameter] public bool Disabled { get; set; }

    private string InputId => string.IsNullOrWhiteSpace(AttributeText("id")) ? _generatedId : AttributeText("id")!;
    private string HelperId => $"{InputId}-hint";
    private string ErrorId => $"{InputId}-error";
    private string InputClass => ShopCssClass.Join("shop-field-input", CssClass);
    private string? InvalidAttribute => EditContext?.GetValidationMessages(FieldIdentifier).Any() == true
        ? "true" : AttributeText("aria-invalid");

    private IReadOnlyDictionary<string, object> InputAttributes
    {
        get
        {
            var attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (AdditionalAttributes is not null)
                foreach (var attribute in AdditionalAttributes)
                    attributes[attribute.Key] = attribute.Value;
            attributes.Remove("aria-label");
            attributes.Remove("aria-labelledby");
            string?[] descriptions =
            [
                AttributeText("aria-describedby"),
                string.IsNullOrWhiteSpace(HelperText) ? null : HelperId,
                EditContext is null ? null : ErrorId
            ];
            var ids = string.Join(' ', descriptions).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal);
            var describedBy = string.Join(' ', ids);
            if (describedBy.Length > 0)
                attributes["aria-describedby"] = describedBy;
            else
                attributes.Remove("aria-describedby");
            return attributes;
        }
    }

    private string? AttributeText(string name) => AdditionalAttributes?
        .FirstOrDefault(attribute => string.Equals(attribute.Key, name, StringComparison.OrdinalIgnoreCase)).Value?.ToString();

    /// <summary>Rejects a blank label rather than rendering an unnamed field.</summary>
    /// <exception cref="ArgumentException">Label is blank.</exception>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
    }

    private void SetValue(string? value)
    {
        if (!Disabled)
            CurrentValueAsString = value;
    }

    /// <inheritdoc />
    protected override bool TryParseValueFromString(string? value, out string? result,
        [NotNullWhen(false)] out string? validationErrorMessage)
    {
        result = value;
        validationErrorMessage = null;
        return true;
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>A labelled two-state checkbox with Figma sizing and built-in Blazor form binding. Native attributes and CSS classes target the input.</summary>
public partial class ShopCheckbox : InputCheckbox
{
    private readonly string _generatedId = $"shop-checkbox-{Guid.NewGuid():N}";

    /// <summary>Required resource-backed or caller-resolved visible label and accessible name.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;

    /// <summary>Selects the Figma icon and target dimensions; label typography remains unchanged.</summary>
    [Parameter] public ShopSize Size { get; set; } = ShopSize.Medium;

    /// <summary>Prevents native activation and synthetic changes. Supply the owning operation's BusyFor value when applicable.</summary>
    [Parameter] public bool Disabled { get; set; }

    private string InputId => string.IsNullOrWhiteSpace(AttributeText("id")) ? _generatedId : AttributeText("id")!;
    private string ErrorId => $"{InputId}-error";
    private string RootClass => ShopCssClass.Join("shop-native", "shop-checkbox", ShopCssClass.Modifier("shop-checkbox", Size));
    private string InputClass => ShopCssClass.Join("shop-checkbox-input", CssClass);
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
            attributes.Remove("aria-checked");
            attributes.Remove("role");
            if (EditContext is not null)
            {
                var ids = $"{AttributeText("aria-describedby")} {ErrorId}"
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal);
                attributes["aria-describedby"] = string.Join(' ', ids);
            }
            return attributes;
        }
    }

    private string? AttributeText(string name) => AdditionalAttributes?
        .FirstOrDefault(attribute => string.Equals(attribute.Key, name, StringComparison.OrdinalIgnoreCase)).Value?.ToString();

    /// <summary>Rejects a blank label rather than rendering an unnamed checkbox.</summary>
    /// <exception cref="ArgumentException">Label is blank.</exception>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
    }

    private void SetValue(bool value)
    {
        if (!Disabled)
            CurrentValue = value;
    }
}

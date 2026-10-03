using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>An icon-only action composing ShopButton with size- and variant-specific geometry.</summary>
public partial class ShopIconButton : ShopComponentBase
{
    /// <summary>Trusted SVG fragment from ShopIcons; never pass user or remote markup.</summary>
    [Parameter, EditorRequired] public string Icon { get; set; } = string.Empty;

    /// <summary>Resource-backed accessible name; takes precedence over supplied ARIA naming attributes.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;

    /// <summary>Shared color role; defaults to primary.</summary>
    [Parameter] public ShopColor Color { get; set; } = ShopColor.Primary;

    /// <summary>Filled, outlined, or text treatment; defaults to filled.</summary>
    [Parameter] public ShopVariant Variant { get; set; } = ShopVariant.Filled;

    /// <summary>Relative icon-button size; defaults to medium. Geometry depends on the selected variant.</summary>
    [Parameter] public ShopSize Size { get; set; } = ShopSize.Medium;

    /// <summary>Prevents browser activation and callback dispatch through the shared button.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Replaces the visible icon with the shared loading spinner while preserving size and Label. Supply the value from BusyFor.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Native button, submit, or reset behavior; unknown values safely default to button.</summary>
    [Parameter] public string Type { get; set; } = "button";

    /// <summary>Raised for an enabled button activation.</summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    private string ClassName => ShopCssClass.Join("shop-icon-button", Class);

    private IReadOnlyDictionary<string, object> ButtonAttributes
    {
        get
        {
            var attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (AdditionalAttributes is not null)
                foreach (var attribute in AdditionalAttributes)
                    attributes[attribute.Key] = attribute.Value;
            attributes.Remove("aria-labelledby");
            attributes["aria-label"] = Label;
            return attributes;
        }
    }

    /// <summary>Rejects missing icon content or accessible names rather than rendering an unnamed action.</summary>
    /// <exception cref="ArgumentException">Icon or Label is blank.</exception>
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Icon);
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
    }
}

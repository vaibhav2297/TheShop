using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>A styled action button or native navigation link; icon-only controls require an accessible name.</summary>
public partial class ShopButton : ShopComponentBase
{
    /// <summary>Visible button content; may contain a decorative icon for an icon-only action.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Visual treatment, independent of native button behavior.</summary>
    [Parameter] public ShopVariant Variant { get; set; } = ShopVariant.Filled;

    /// <summary>Color role, independent of treatment and behavior; defaults to primary.</summary>
    [Parameter] public ShopColor Color { get; set; } = ShopColor.Primary;

    /// <summary>Figma size variant; defaults to medium.</summary>
    [Parameter] public ShopSize Size { get; set; } = ShopSize.Medium;

    /// <summary>Prevents browser activation and callback dispatch.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Shows only a spinner, preserves the name and dimensions, and prevents activation. Supply the value from BusyFor.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Navigation destination. Non-null values render an anchor; disabled or loading links omit the destination and leave the tab order.</summary>
    [Parameter] public string? Href { get; set; }

    /// <summary>Native button, submit, or reset behavior when Href is null. Unknown values safely default to button.</summary>
    [Parameter] public string Type { get; set; } = "button";

    /// <summary>Trusted SVG fragment from ShopIcons displayed before the content.</summary>
    [Parameter] public string? StartIcon { get; set; }

    /// <summary>Trusted SVG fragment from ShopIcons displayed after the content.</summary>
    [Parameter] public string? EndIcon { get; set; }

    /// <summary>Raised for enabled activation; link navigation retains the browser's default behavior.</summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    private string ButtonType => Type?.ToLowerInvariant() switch
    {
        "submit" => "submit",
        "reset" => "reset",
        _ => "button"
    };

    private string ClassName => ShopCssClass.Join(
        "shop-button",
        Loading ? "shop-button-loading" : null,
        ShopCssClass.Modifier("shop-button", Variant),
        ShopCssClass.Modifier("shop-button", Color),
        ShopCssClass.Modifier("shop-button", Size),
        Class);

    private bool IsDisabled => Disabled || Loading;

    private object? LinkTabIndex => IsDisabled ? -1 : AdditionalAttributes?
        .FirstOrDefault(attribute => string.Equals(attribute.Key, "tabindex", StringComparison.OrdinalIgnoreCase)).Value;

    private Task OnClickAsync(MouseEventArgs args) => IsDisabled ? Task.CompletedTask : OnClick.InvokeAsync(args);
}

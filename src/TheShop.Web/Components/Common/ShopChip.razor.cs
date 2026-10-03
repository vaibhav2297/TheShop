using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>Outlined label or controlled toggle chip with the Figma default and selected treatments.</summary>
public partial class ShopChip : ShopComponentBase
{
    /// <summary>Localized non-interactive label content.</summary>
    [Parameter, EditorRequired] public RenderFragment? ChildContent { get; set; }

    /// <summary>Relative chip geometry; defaults to Medium.</summary>
    [Parameter] public ShopSize Size { get; set; } = ShopSize.Medium;

    /// <summary>Whether the chip has the selected border treatment.</summary>
    [Parameter] public bool Selected { get; set; }

    /// <summary>When supplied, renders a native toggle button; the caller owns the committed selection.</summary>
    [Parameter] public EventCallback<bool> SelectedChanged { get; set; }

    /// <summary>Disables a selectable chip and prevents selection callbacks.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Optional decorative SVG fragment from the trusted ShopIcons registry.</summary>
    [Parameter] public string? StartIcon { get; set; }

    /// <summary>Optional decorative trailing SVG; not a separate dismiss action.</summary>
    [Parameter] public string? EndIcon { get; set; }

    private string ClassName => ShopCssClass.Join("shop-native", "shop-chip",
        ShopCssClass.Modifier("shop-chip", Size, nameof(Size)),
        Selected ? "shop-chip-selected" : null, Class);

    private Task ToggleAsync() => Disabled ? Task.CompletedTask : SelectedChanged.InvokeAsync(!Selected);
}


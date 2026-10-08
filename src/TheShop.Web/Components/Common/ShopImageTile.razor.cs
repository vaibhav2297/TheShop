using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TheShop.Web.Common.UI;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>One controlled image presentation with sibling selection and removal actions.
/// Owns no files, URLs, selection order, or grid sizing. Root attributes target its outer frame.</summary>
public partial class ShopImageTile : ShopComponentBase
{
    /// <summary>Borrowed image URL; the owner manages its lifetime.</summary>
    [Parameter] public string? Src { get; set; }
    /// <summary>Image description and accessible selection name.</summary>
    [Parameter, EditorRequired] public string Alt { get; set; } = string.Empty;
    /// <summary>Image fitting inside the square tile; defaults to whole-image display.</summary>
    [Parameter] public ShopImagePreset Preset { get; set; } = ShopImagePreset.SquareContain;
    /// <summary>Shows the filled badge; grid placement remains caller-owned.</summary>
    [Parameter] public bool Primary { get; set; }
    /// <summary>Resource-backed badge text.</summary>
    [Parameter] public string PrimaryLabel { get; set; } = Strings.ImageUpload_Primary;
    /// <summary>Nullable selection state; null means this is not a toggle selector.</summary>
    [Parameter] public bool? Selected { get; set; }
    /// <summary>Shows a loader over dimmed imagery. Supply processing state from BusyFor.</summary>
    [Parameter] public bool Loading { get; set; }
    /// <summary>Replaces imagery with an error message; removal remains available.</summary>
    [Parameter] public string? ErrorText { get; set; }
    /// <summary>Offers removal; false hides the action in every state.</summary>
    [Parameter] public bool Removable { get; set; } = true;
    /// <summary>Accessible name of the removal action.</summary>
    [Parameter] public string RemoveLabel { get; set; } = Strings.ImageUpload_Remove;
    /// <summary>Prevents all tile actions.</summary>
    [Parameter] public bool Disabled { get; set; }
    /// <summary>Enables native dragging of the surface; the parent handles reordering.</summary>
    [Parameter] public bool Draggable { get; set; }
    /// <summary>Optional instruction IDs for the surface button.</summary>
    [Parameter] public string? DescribedBy { get; set; }
    /// <summary>Requests selection or activation without changing state internally.</summary>
    [Parameter] public EventCallback OnClick { get; set; }
    /// <summary>Reports keyboard focus on the surface.</summary>
    [Parameter] public EventCallback OnFocus { get; set; }
    /// <summary>Requests removal without affecting selection.</summary>
    [Parameter] public EventCallback OnRemove { get; set; }
    /// <summary>Forwards enabled surface key presses for parent-owned ordering shortcuts.</summary>
    [Parameter] public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }

    private bool EffectiveDisabled => Disabled || Loading;
    private string? Pressed => Selected?.ToString().ToLowerInvariant();
    private string ClassName => ShopCssClass.Join("shop-image-tile", Selected == true ? "shop-image-tile-selected" : null,
        Loading ? "shop-image-tile-loading" : null, ErrorText is not null ? "shop-image-tile-error" : null, Class);
    private Task ActivateAsync() => EffectiveDisabled ? Task.CompletedTask : OnClick.InvokeAsync();
    private Task FocusAsync() => EffectiveDisabled ? Task.CompletedTask : OnFocus.InvokeAsync();
    private Task RemoveAsync() => EffectiveDisabled || !Removable ? Task.CompletedTask : OnRemove.InvokeAsync();
    private Task KeyDownAsync(KeyboardEventArgs args) => EffectiveDisabled ? Task.CompletedTask : OnKeyDown.InvokeAsync(args);
}

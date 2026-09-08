using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Reusable dialog chrome — title with an optional close button, a content slot, and an actions
/// slot — composed by every dialog in the admin surface (<see cref="ShopConfirmDialog"/>,
/// <c>VariantImageDialog</c>) instead of each redeclaring the same <see cref="MudDialog"/>
/// header/content/actions structure. All text is supplied by the caller as already-localized
/// strings (Rule 11 Pattern 1 at the call site) — the dialog itself resolves no resource keys.
/// </summary>
public partial class ShopDialog : MudComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>The dialog's title.</summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>The dialog's body content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>The dialog's action buttons.</summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <summary>Whether the header shows a close (X) button. Defaults to <c>true</c>.</summary>
    [Parameter]
    public bool ShowCloseButton { get; set; } = true;

    /// <summary>Localized ARIA label for the close button.</summary>
    [Parameter]
    public string? CloseLabel { get; set; }

    protected string Classname => new CssBuilder("shop-dialog").AddClass(Class).Build();
    protected string Stylename => new StyleBuilder().AddStyle(Style).Build();

    private void Close() => MudDialog.Cancel();
}

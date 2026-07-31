using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Full-bleed bar pinned to the bottom of the viewport while a list has a live multi-selection.
/// Shows the selected-item count on the left and the caller's <see cref="Actions"/> on the right.
/// The actions stay with the calling page rather than being parameters here, so each one keeps
/// its own <c>AuthorizeView</c> gate and the bar itself stays feature-agnostic. Fixed positioning
/// comes from <c>Styles/components/_bulkactionbar.scss</c>; the surface reuses the same
/// <c>mud-theme-dark</c> token as <see cref="ShopAnnouncementBar"/>.
/// </summary>
public partial class ShopBulkActionBar : MudComponentBase
{
    /// <summary>
    /// Whether the bar is shown. Callers pass their own "has a selection" predicate; the bar
    /// renders nothing at all while this is <c>false</c>.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; }

    /// <summary>The number of currently selected items, reported in the bar's label.</summary>
    [Parameter, EditorRequired]
    public int SelectedCount { get; set; }

    /// <summary>
    /// The action controls rendered at the right of the bar, supplied by the calling page so
    /// that permission gating stays with the feature that owns the actions.
    /// </summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <summary>
    /// Invoked when the user dismisses the bar with its close button. The bar does not hide itself
    /// — <see cref="Visible"/> stays caller-owned — so the page clears the selection that put the
    /// bar up, which is what actually takes it down.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private string Classname => new CssBuilder("shop-bulk-action-bar")
        .AddClass("mud-theme-dark")
        .AddClass(Class)
        .Build();
}

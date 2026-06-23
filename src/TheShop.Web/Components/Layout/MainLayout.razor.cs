using Microsoft.AspNetCore.Components;
using TheShop.Web.Components.Common;
using TheShop.Web.State;
using TheShop.Web.Theme;

namespace TheShop.Web.Components.Layout;

/// <summary>
/// Root layout for all main shop pages. Lays out the optional <see cref="ShopAnnouncementBar"/>,
/// the <see cref="ShopAppBar"/>, and the scrollable page content as a vertical flex column,
/// so the header re-flows automatically when an announcement appears or is cleared.
/// Auth pages use <see cref="AuthLayout"/> instead.
/// </summary>
public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject] private ShopTheme ShopTheme { get; set; } = default!;

    [Inject] private AnnouncementState Announcement { get; set; } = default!;

    protected override void OnInitialized() => Announcement.OnChange += StateHasChanged;

    public void Dispose() => Announcement.OnChange -= StateHasChanged;
}

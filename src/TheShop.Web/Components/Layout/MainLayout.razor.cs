using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using TheShop.Web.Components.Common;
using TheShop.Web.State;
using TheShop.Web.Theme;

namespace TheShop.Web.Components.Layout;

/// <summary>
/// Root layout for all main shop pages. Lays out the optional <see cref="ShopAnnouncementBar"/>,
/// the <see cref="ShopAppBar"/>, the breadcrumb slot (when a trail is active), the scrollable
/// page content, and the <see cref="ShopFooter"/> slot as a vertical flex column. Clears the
/// breadcrumb trail and restores footer visibility on every navigation so the next page starts
/// from a clean chrome state. Auth pages use <see cref="AuthLayout"/> instead.
/// </summary>
public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject] private ShopTheme ShopTheme { get; set; } = default!;

    [Inject] private AnnouncementState Announcement { get; set; } = default!;

    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    [Inject] private FooterState Footer { get; set; } = default!;

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Announcement.OnChange += StateHasChanged;
        Breadcrumbs.OnChange += StateHasChanged;
        Footer.OnChange += StateHasChanged;
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        Breadcrumbs.Clear();
        Footer.Show();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Announcement.OnChange -= StateHasChanged;
        Breadcrumbs.OnChange -= StateHasChanged;
        Footer.OnChange -= StateHasChanged;
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}

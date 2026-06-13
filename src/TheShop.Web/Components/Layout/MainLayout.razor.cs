using Microsoft.AspNetCore.Components;
using TheShop.Web.Components.Common;
using TheShop.Web.Theme;

namespace TheShop.Web.Components.Layout;

/// <summary>
/// Root layout for all main shop pages. Renders the announcement bar, <see cref="ShopAppBar"/>,
/// and the page content area. Auth pages use <see cref="AuthLayout"/> instead.
/// </summary>
public partial class MainLayout : LayoutComponentBase
{
    [Inject] private ShopTheme ShopTheme { get; set; } = default!;
}

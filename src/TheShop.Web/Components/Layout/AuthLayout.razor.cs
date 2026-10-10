using Microsoft.AspNetCore.Components;
using TheShop.Web.Theme;

namespace TheShop.Web.Components.Layout;

/// <summary>
/// Native document container for authentication pages with the shared UI host.
/// Theme/popover providers temporarily support the remaining signup and verification controls.
/// </summary>
public partial class AuthLayout : LayoutComponentBase
{
    [Inject] private ShopTheme ShopTheme { get; set; } = default!;
}

using Microsoft.AspNetCore.Components;
using TheShop.Web.Theme;

namespace TheShop.Web.Components.Layout;

/// <summary>
/// Bare layout for authentication pages. Renders only the MudBlazor providers and
/// <see cref="ShopLoadingOverlay"/> — no AppBar or navigation shell.
/// </summary>
public partial class AuthLayout : LayoutComponentBase
{
    [Inject] private ShopTheme ShopTheme { get; set; } = default!;
}

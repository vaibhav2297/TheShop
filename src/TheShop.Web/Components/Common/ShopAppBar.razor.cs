using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Layout;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Top navigation bar rendered in <see cref="MainLayout"/>. Displays the brand logo,
/// primary nav links, and auth-aware action icons. The layout owns the account drawer state.
/// </summary>
public partial class ShopAppBar : ShopComponentBase
{
    /// <summary>Raised when the authenticated account action is activated.</summary>
    [Parameter] public EventCallback OnAccountClick { get; set; }

    /// <summary>Whether the layout's account drawer is open; exposed to assistive technology.</summary>
    [Parameter] public bool AccountOpen { get; set; }

    private string ClassName => ShopCssClass.Join("shop-native", "shop-appbar", Class);
}

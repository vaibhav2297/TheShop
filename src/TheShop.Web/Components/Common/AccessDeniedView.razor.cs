using TheShop.Web.Common;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Shared in-place access-denied content block (Figma node <c>2470:2220</c>), rendered inside the
/// normal page chrome at the attempted URL rather than as a separate error page. Used by
/// <c>App.razor</c>'s <c>AuthorizeRouteView.NotAuthorized</c> for an authenticated-but-unauthorized
/// visitor and by admin pages that additionally gate on a specific permission (e.g.
/// <see cref="TheShop.Web.Pages.Admin.ManageProducts"/> on <c>products.view</c>). The single back
/// button always returns the visitor to the store home.
/// </summary>
public partial class AccessDeniedView : ShopComponentBase
{
    private const string BackHref = Routes.Home;

    private string ClassName => ShopCssClass.Join("shop-native", "shop-access-denied", Class);
}

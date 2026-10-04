using TheShop.Web.Common.UI;
using TheShop.Web.Components.Layout;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Top navigation bar rendered in <see cref="MainLayout"/>. Displays the brand logo,
/// primary nav links, and auth-aware action icons. The authenticated account dropdown and
/// sign-out flow are owned by <see cref="ProfileMenu"/>.
/// </summary>
public partial class ShopAppBar : ShopComponentBase
{
    private string ClassName => ShopCssClass.Join("shop-native", "shop-appbar", Class);
}

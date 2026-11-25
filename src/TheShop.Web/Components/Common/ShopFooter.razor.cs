using TheShop.Web.Common.UI;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>Responsive site footer with shared link content, brand information and copyright.</summary>
public partial class ShopFooter : ShopComponentBase
{
    private string ClassName => ShopCssClass.Join("shop-native", "shop-footer", Class);
    private static string Copyright => string.Format(Strings.Footer_Copyright, DateTime.UtcNow.Year);
}

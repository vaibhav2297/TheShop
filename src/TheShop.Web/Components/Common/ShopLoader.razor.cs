using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Decorative indeterminate loader matching Figma 2996:28796. The owner controls visibility
/// and loading announcements. Class, Style and additional attributes target the 48px root;
/// caller SCSS may resize it and set --shop-loader-color for contextual presentation.
/// </summary>
public partial class ShopLoader : ShopComponentBase
{
    private string ClassName => ShopCssClass.Join("shop-loader", Class);
}

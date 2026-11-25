using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Static, decorative loading placeholder from Figma set 3010:17894. It is always hidden from
/// assistive technology; the busy container owns any loading announcement. Size comes from the
/// caller's SCSS class, never from parameters.
/// </summary>
public partial class ShopSkeleton : ShopComponentBase
{
    /// <summary>
    /// Rectangle takes its block size from the caller; Text spans one line of the inherited text style.
    /// </summary>
    [Parameter] public ShopSkeletonShape Shape { get; set; } = ShopSkeletonShape.Rectangle;

    // No shop-native root class: it would reset the inherited text style that sizes the Text shape.
    private string ClassName => ShopCssClass.Join(
        "shop-skeleton",
        ShopCssClass.Modifier("shop-skeleton", Shape),
        Class);
}

using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Products;

/// <summary>
/// Loading placeholder for <see cref="ProductCard"/>. It reuses the card's own media, content and
/// text classes, so its square image, line heights and gaps follow the real card. Decorative only.
/// </summary>
public partial class ProductCardSkeleton : ShopComponentBase
{
    private string ClassName => ShopCssClass.Join("shop-native", "shop-product-card", "shop-product-card-skeleton", Class);
}

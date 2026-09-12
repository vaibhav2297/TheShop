namespace TheShop.Web.Components.Common;

/// <summary>
/// Defines stable aspect-ratio and fit presets for images shown throughout the storefront and administration UI.
/// </summary>
public enum ShopImageTreatment
{
    /// <summary>
    /// Square product media that keeps the whole product visible.
    /// </summary>
    Product,

    /// <summary>
    /// Square compact media that keeps the whole image visible.
    /// </summary>
    Thumbnail,

    /// <summary>
    /// Square category photography that fills and may crop at the frame edges.
    /// </summary>
    CategoryTile,

    /// <summary>
    /// Wide 16:5 category photography that fills and may crop at the frame edges.
    /// </summary>
    CategoryBanner,

    /// <summary>
    /// Desktop 16:9 hero photography that fills and may crop at the frame edges.
    /// </summary>
    HeroDesktop,

    /// <summary>
    /// Mobile 4:5 hero photography that fills and may crop at the frame edges.
    /// </summary>
    HeroMobile,

    /// <summary>
    /// Editorial 4:3 photography that fills and may crop at the frame edges.
    /// </summary>
    Editorial,

    /// <summary>
    /// Logo media that retains its intrinsic ratio and remains fully visible.
    /// </summary>
    BrandLogo,

    /// <summary>
    /// Social-sharing media prepared for a 40:21 frame without additional cropping.
    /// </summary>
    SocialSharing
}

namespace TheShop.Web.Components.Common;

/// <summary>
/// Named image treatments rendered by <see cref="ShopImage"/>. Each preset fixes the displayed frame
/// ratio and whether the image is shown whole (contain) or fills the frame with a centered crop (cover).
/// </summary>
public enum ShopImagePreset
{
    /// <summary>
    /// Catalogue product tile: 1:1 frame, whole image visible.
    /// </summary>
    ProductCard,

    /// <summary>
    /// Product detail gallery: 1:1 frame, whole image visible.
    /// </summary>
    ProductDetail,

    /// <summary>
    /// Cart, order, and admin thumbnails: 1:1 frame, whole image visible.
    /// </summary>
    Thumbnail,

    /// <summary>
    /// Category tile photography: 1:1 frame, centered crop.
    /// </summary>
    CategoryTile,

    /// <summary>
    /// Category banner: 16:5 frame on desktop and 4:5 on mobile, centered crop. Uses
    /// <see cref="ShopImage.MobileSrc"/> on mobile when supplied.
    /// </summary>
    CategoryBanner,

    /// <summary>
    /// Hero: 16:9 frame on desktop and 4:5 on mobile, centered crop. Uses
    /// <see cref="ShopImage.MobileSrc"/> on mobile when supplied.
    /// </summary>
    Hero,

    /// <summary>
    /// Mobile-only banner composition: 4:5 frame, centered crop.
    /// </summary>
    MobileBanner,

    /// <summary>
    /// Editorial or lifestyle card: 4:3 frame, centered crop.
    /// </summary>
    Editorial,

    /// <summary>
    /// Brand logo: no imposed ratio. The caller reserves width and height; the whole logo is
    /// contained within that space in its original proportions.
    /// </summary>
    BrandLogo,

    /// <summary>
    /// Prepared social sharing composition: 40:21 frame, whole image visible.
    /// </summary>
    SocialSharing,
}

namespace TheShop.Web.Components.Common;

/// <summary>
/// Named image treatments rendered by <see cref="ShopImage"/>. Each preset fixes the displayed frame
/// ratio and whether the image is shown whole (contain) or fills the frame with a centered crop (cover).
/// </summary>
public enum ShopImagePreset
{
    /// <summary>
    /// Product cards, galleries, and cart, order, or admin thumbnails: 1:1 frame, whole image visible.
    /// </summary>
    SquareContain,

    /// <summary>
    /// Category tiles and square photography: 1:1 frame, centered crop.
    /// </summary>
    SquareCover,

    /// <summary>
    /// Category and promotional banners: 16:5 frame on desktop and 4:5 on mobile, centered crop. Uses
    /// <see cref="ShopImage.MobileSrc"/> on mobile when supplied.
    /// </summary>
    Banner,

    /// <summary>
    /// Hero: 16:9 frame on desktop and 4:5 on mobile, centered crop. Uses
    /// <see cref="ShopImage.MobileSrc"/> on mobile when supplied.
    /// </summary>
    Hero,

    /// <summary>
    /// Portrait artwork and promotional cards: 4:5 frame, centered crop.
    /// </summary>
    PortraitCover,

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

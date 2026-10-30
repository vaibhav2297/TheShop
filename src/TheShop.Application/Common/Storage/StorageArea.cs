namespace TheShop.Application.Common.Storage;

/// <summary>
/// A logical storage namespace. Infrastructure maps each area to a bucket and key prefix —
/// Application never names a Supabase bucket directly (Rule 3). Adding a future storage need is
/// one enum value plus one Infrastructure registry entry.
/// </summary>
public enum StorageArea
{
    /// <summary>
    /// Public product images, namespaced by product ID. Each product can have multiple images.
    /// </summary>
    ProductImages,

    /// <summary>
    /// Public brand logos, namespaced by brand ID. Each brand can have one logo.
    /// </summary>
    BrandLogos,

    /// <summary>
    /// Public category images, namespaced by category ID. Each category can have one image.
    /// </summary>
    CategoryImages,

    /// <summary>
    /// Private homepage artwork with authenticated uploads and signed previews.
    /// </summary>
    HeroBannerImages,
}

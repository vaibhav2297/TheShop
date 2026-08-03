namespace TheShop.Application.Common.Storage;

/// <summary>
/// A logical storage namespace. Infrastructure maps each area to a bucket and key prefix —
/// Application never names a Supabase bucket directly (Rule 3). Adding a future storage need is
/// one enum value plus one Infrastructure registry entry.
/// </summary>
public enum StorageArea
{
    ProductImages,
    BrandLogos,
    CategoryImages,
}

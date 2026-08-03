using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using FileOptions = Supabase.Storage.FileOptions;

namespace TheShop.Infrastructure.Storage;

/// <summary>
/// Supabase Storage-backed implementation of <see cref="IFileStorage"/>, generic across every
/// <see cref="StorageArea"/> via an internal area → (bucket, key prefix) registry. Storage RLS
/// restricts writes per area to the permission that area's bucket policies require.
/// </summary>
public sealed class SupabaseFileStorage(Supabase.Client client) : IFileStorage
{
    private static readonly IReadOnlyDictionary<StorageArea, (string Bucket, string KeyPrefix)> Areas =
        new Dictionary<StorageArea, (string Bucket, string KeyPrefix)>
        {
            [StorageArea.ProductImages] = ("product-images", "products"),
            [StorageArea.BrandLogos] = ("brand-logos", "brands"),
            [StorageArea.CategoryImages] = ("category-images", "categories"),
        };

    /// <inheritdoc/>
    public async Task<string> UploadAsync(
        StorageArea area, Guid ownerId, Stream content, string fileName, string contentType, CancellationToken ct)
    {
        var (bucket, keyPrefix) = Areas[area];

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        var key = BuildObjectKey(keyPrefix, ownerId, fileName);
        var options = new FileOptions { ContentType = contentType, Upsert = false };

        await client.Storage.From(bucket).Upload(buffer.ToArray(), key, options);
        return key;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(StorageArea area, string objectKey, CancellationToken ct)
    {
        var (bucket, _) = Areas[area];
        await client.Storage.From(bucket).Remove([objectKey]);
    }

    /// <inheritdoc/>
    public string GetPublicUrl(StorageArea area, string objectKey)
    {
        var (bucket, _) = Areas[area];
        return client.Storage.From(bucket).GetPublicUrl(objectKey);
    }

    private static string BuildObjectKey(string keyPrefix, Guid ownerId, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return $"{keyPrefix}/{ownerId}/{Guid.NewGuid():N}{ext}";
    }
}

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
    private static readonly IReadOnlyDictionary<StorageArea, (string Bucket, string KeyPrefix, bool IsPublic)> Areas =
        new Dictionary<StorageArea, (string Bucket, string KeyPrefix, bool IsPublic)>
        {
            [StorageArea.ProductImages] = ("product-images", "products", true),
            [StorageArea.BrandLogos] = ("brand-logos", "brands", true),
            [StorageArea.CategoryImages] = ("category-images", "categories", true),
            [StorageArea.HeroBannerImages] = ("hero-banner-images", "hero", false),
        };

    /// <inheritdoc/>
    public async Task<string> UploadAsync(
        StorageArea area, Guid ownerId, Stream content, string fileName, string contentType, CancellationToken ct)
    {
        var (bucket, keyPrefix, _) = Areas[area];

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        var key = BuildObjectKey(keyPrefix, ownerId, fileName);
        var options = new FileOptions { ContentType = contentType, Upsert = false };

        ct.ThrowIfCancellationRequested();
        await client.Storage.From(bucket).Upload(buffer.ToArray(), key, options);
        return key;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(StorageArea area, string objectKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (bucket, _, _) = Areas[area];
        await client.Storage.From(bucket).Remove([objectKey]);
    }

    /// <inheritdoc/>
    public async Task<string> CreateSignedUrlAsync(
        StorageArea area, string objectKey, int expiresInSeconds, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expiresInSeconds);
        ct.ThrowIfCancellationRequested();
        var (bucket, _, _) = Areas[area];
        return await client.Storage.From(bucket).CreateSignedUrl(objectKey, expiresInSeconds);
    }

    /// <inheritdoc/>
    public string GetPublicUrl(StorageArea area, string objectKey)
    {
        var (bucket, _, isPublic) = Areas[area];
        if (!isPublic)
            throw new InvalidOperationException("Public URLs are unavailable for private storage areas.");
        return client.Storage.From(bucket).GetPublicUrl(objectKey);
    }

    private static string BuildObjectKey(string keyPrefix, Guid ownerId, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return $"{keyPrefix}/{ownerId}/{Guid.NewGuid():N}{ext}";
    }
}

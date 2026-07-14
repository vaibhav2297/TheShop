using TheShop.Application.Common.Interfaces;
using FileOptions = Supabase.Storage.FileOptions;

namespace TheShop.Infrastructure.Storage;

/// <summary>
/// Supabase Storage-backed implementation of <see cref="IProductImageStorage"/>. Reads and writes
/// the public <c>product-images</c> bucket (migration 0004). Storage RLS restricts writes to the
/// admin role, so uploads/deletes only succeed for an admin-authenticated Supabase client.
/// </summary>
public sealed class SupabaseProductImageStorage(Supabase.Client client) : IProductImageStorage
{
    // Same bucket the catalogue read side resolves image URLs from
    // (see SupabaseProductRepository.ProductImagesBucket).
    private const string Bucket = "product-images";

    /// <inheritdoc/>
    public async Task<string> UploadAsync(
        Guid productId, Stream content, string fileName, string contentType, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        var key = ProductImageKey.For(productId, fileName);
        var options = new FileOptions { ContentType = contentType, Upsert = false };

        await client.Storage.From(Bucket).Upload(buffer.ToArray(), key, options);
        return key;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string imagePath, CancellationToken ct)
    {
        await client.Storage.From(Bucket).Remove([imagePath]);
    }
}

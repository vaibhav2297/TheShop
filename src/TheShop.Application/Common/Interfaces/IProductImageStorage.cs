namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Write-side contract for product image storage. Implementations live in the Infrastructure
/// layer (backed by the Supabase Storage <c>product-images</c> bucket). The admin product-image
/// upload feature depends on this abstraction rather than on the Supabase SDK directly.
/// </summary>
/// <remarks>
/// The returned/accepted <c>imagePath</c> is the storage OBJECT KEY (e.g.
/// <c>products/{productId}/{guid}.webp</c>), which is what gets persisted to
/// <c>products.image_path</c>. The catalogue read side turns that key into a public URL.
/// </remarks>
public interface IProductImageStorage
{
    /// <summary>
    /// Uploads an image for the given product and returns the stored object key to persist on
    /// the product. Each upload gets a unique key, so callers can replace an image by uploading a
    /// new one and deleting the old key.
    /// </summary>
    /// <param name="productId">The product the image belongs to; namespaces the object key.</param>
    /// <param name="content">The image bytes.</param>
    /// <param name="fileName">The original file name; its extension is preserved on the key.</param>
    /// <param name="contentType">The image MIME type (e.g. <c>image/webp</c>).</param>
    Task<string> UploadAsync(
        Guid productId, Stream content, string fileName, string contentType, CancellationToken ct);

    /// <summary>
    /// Removes a previously uploaded image by its object key. No-op if the object does not exist.
    /// </summary>
    Task DeleteAsync(string imagePath, CancellationToken ct);
}

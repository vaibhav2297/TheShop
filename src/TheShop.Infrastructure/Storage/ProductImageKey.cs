namespace TheShop.Infrastructure.Storage;

/// <summary>
/// Builds Supabase Storage object keys for product images. The key namespaces each object under
/// its product and gives it a unique, collision-free name while preserving the original file
/// extension: <c>products/{productId}/{guid}{ext}</c>.
/// </summary>
internal static class ProductImageKey
{
    public static string For(Guid productId, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return $"products/{productId}/{Guid.NewGuid():N}{ext}";
    }
}

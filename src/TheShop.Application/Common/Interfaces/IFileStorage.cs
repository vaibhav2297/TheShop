using TheShop.Application.Common.Storage;

namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Provider-neutral file-storage contract, generic across every <see cref="StorageArea"/>.
/// Implementations live in the Infrastructure layer (backed by Supabase Storage buckets).
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Uploads a file into the given <paramref name="area"/>, namespaced under
    /// <paramref name="ownerId"/>, and returns the stored object key to persist on the owning
    /// entity. Each upload gets a unique key.
    /// </summary>
    Task<string> UploadAsync(
        StorageArea area, Guid ownerId, Stream content, string fileName, string contentType, CancellationToken ct);

    /// <summary>
    /// Removes a previously uploaded object by its key. No-op if the object does not exist.
    /// </summary>
    Task DeleteAsync(StorageArea area, string objectKey, CancellationToken ct);

    /// <summary>
    /// Resolves a stored object key to its public URL.
    /// </summary>
    string GetPublicUrl(StorageArea area, string objectKey);
}

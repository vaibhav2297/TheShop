namespace TheShop.Web.Components.Common;

/// <summary>
/// A single image held by <see cref="ShopImageUpload"/>. Covers both a file the user just picked
/// (raw <see cref="Bytes"/> plus a <c>data:</c> <see cref="PreviewUrl"/>) and an image that is
/// already stored server-side (<see cref="ExistingId"/> plus its stored URL), so a consumer can
/// present, reorder, and submit one ordered selection that mixes the two.
/// </summary>
/// <param name="Bytes">The full contents of the file, read once at selection time. Empty for an already-stored image.</param>
/// <param name="FileName">The original client file name. Empty for an already-stored image.</param>
/// <param name="ContentType">The MIME type reported by the browser (e.g. <c>image/png</c>). Empty for an already-stored image.</param>
/// <param name="PreviewUrl">The preview source — a <c>data:</c> URL of <paramref name="Bytes"/> for a newly picked file, the stored URL otherwise.</param>
public sealed record ShopUploadedImage(
    byte[] Bytes,
    string FileName,
    string ContentType,
    string PreviewUrl)
{
    /// <summary>
    /// A stable identity for this entry, minted at selection time and carried unchanged for as long
    /// as the entry stays in the selection. For an already-stored image it is the stored
    /// <see cref="ExistingId"/>; for a newly picked file it is a fresh correlation id that lets a
    /// consumer refer to the image before it has been uploaded and given a real identifier
    /// server-side. Also makes two picks of the same file distinguishable, which record value
    /// equality alone would not.
    /// </summary>
    public Guid ClientId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Identifier of the already-stored image this entry stands for, or <c>null</c> when the entry
    /// is a newly picked file that has not been uploaded yet.
    /// </summary>
    public Guid? ExistingId { get; init; }

    /// <summary>
    /// Whether this entry refers to an already-stored image rather than a newly picked file.
    /// </summary>
    public bool IsExisting => ExistingId is not null;

    /// <summary>
    /// A localized validation message (wrong type or too large) when this entry failed
    /// <see cref="ShopImageUpload"/>'s guard at selection time, or <c>null</c> when it is valid.
    /// An invalid entry still occupies a row — with its thumbnail, name, and this message — so the
    /// selection stays visible until the user removes it; a consumer must exclude it before saving.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates an entry for an image that is already stored server-side so it can sit in the same
    /// ordered selection as newly picked files.
    /// </summary>
    /// <param name="id">The stored image's identifier, which also becomes its <see cref="ClientId"/>.</param>
    /// <param name="url">The stored image's URL, used as the preview source.</param>
    /// <returns>An entry carrying no bytes, flagged as existing.</returns>
    public static ShopUploadedImage Existing(Guid id, string url) =>
        new([], string.Empty, string.Empty, url) { ClientId = id, ExistingId = id };
}

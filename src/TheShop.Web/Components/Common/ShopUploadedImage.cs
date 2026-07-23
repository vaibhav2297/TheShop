namespace TheShop.Web.Components.Common;

/// <summary>
/// A single image selected in <see cref="ShopImageUpload"/>. Bundles the raw file bytes with
/// the metadata needed both to render an in-browser preview (<see cref="PreviewUrl"/>) and to
/// forward the image to a command (<see cref="FileName"/>, <see cref="ContentType"/>).
/// </summary>
/// <param name="Bytes">The full contents of the file, read once at selection time.</param>
/// <param name="FileName">The original client file name.</param>
/// <param name="ContentType">The MIME type reported by the browser (e.g. <c>image/png</c>).</param>
/// <param name="PreviewUrl">A <c>data:</c> URL of the bytes, bound directly to an image preview.</param>
public sealed record ShopUploadedImage(
    byte[] Bytes,
    string FileName,
    string ContentType,
    string PreviewUrl);

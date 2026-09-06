namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// The transport for a newly uploaded gallery image inside a <see cref="ProductGalleryEntry"/>.
/// Keeps <c>IBrowserFile</c>/streams out of the Application layer (Rule 7).
/// </summary>
public sealed record ProductImageUpload(byte[] Content, string FileName, string ContentType);

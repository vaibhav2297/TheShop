namespace TheShop.Application.Features.Brands.DTOs;

/// <summary>
/// The transport for an optional logo upload inside <c>CreateBrandCommand</c>. Keeps
/// <c>IBrowserFile</c>/streams out of the Application layer (Rule 7).
/// </summary>
public sealed record BrandLogoUpload(byte[] Content, string FileName, string ContentType);

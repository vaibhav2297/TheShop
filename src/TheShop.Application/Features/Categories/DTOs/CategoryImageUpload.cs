namespace TheShop.Application.Features.Categories.DTOs;

/// <summary>
/// The transport for an optional image upload inside <c>CreateCategoryCommand</c>. Keeps
/// <c>IBrowserFile</c>/streams out of the Application layer (Rule 7).
/// </summary>
public sealed record CategoryImageUpload(byte[] Content, string FileName, string ContentType);

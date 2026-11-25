namespace TheShop.Application.Features.Categories.DTOs;

/// <summary>
/// A category as surfaced to the Web layer. <see cref="ImageUrl"/> is the resolved public bucket
/// URL, not the stored object key.
/// </summary>
public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    string? ImageUrl,
    bool IsActive);

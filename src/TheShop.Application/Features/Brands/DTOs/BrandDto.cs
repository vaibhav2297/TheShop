namespace TheShop.Application.Features.Brands.DTOs;

/// <summary>
/// A brand as surfaced to the Web layer. <see cref="LogoUrl"/> is the resolved public bucket
/// URL, not the stored object key.
/// </summary>
public sealed record BrandDto(
    Guid Id,
    string Name,
    string? Description,
    string? LogoUrl,
    bool IsActive);

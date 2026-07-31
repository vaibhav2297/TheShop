namespace TheShop.Application.Features.Brands.DTOs;

/// <summary>
/// A brand as surfaced in the manage-brands admin list. <see cref="ProductCount"/> is the
/// authoritative count from <c>brand_product_counts</c> (counts every referencing product,
/// published or not — RULE-6) and drives the FR-13 refusal message. <see cref="LogoUrl"/> is
/// always a displayable URL — a placeholder when the brand has no uploaded logo — since this DTO
/// only ever feeds a display cell, never a "does this brand have a logo" check.
/// </summary>
public sealed record BrandListItemDto(
    Guid Id,
    string Name,
    string? Description,
    string LogoUrl,
    bool IsActive,
    int ProductCount);

namespace TheShop.Application.Features.Categories.DTOs;

/// <summary>
/// A category as surfaced in the manage-categories admin list. <see cref="ProductCount"/> is the
/// authoritative count from <c>category_product_counts</c> (counts every referencing product,
/// published or not — RULE-6) and drives the FR-15 refusal message. <see cref="ImageUrl"/> is
/// always a displayable URL — a placeholder when the category has no uploaded image — since this
/// DTO only ever feeds a display cell, never a "does this category have an image" check.
/// </summary>
public sealed record CategoryListItemDto(
    Guid Id,
    string Name,
    string? Description,
    string ImageUrl,
    bool IsActive,
    int ProductCount);

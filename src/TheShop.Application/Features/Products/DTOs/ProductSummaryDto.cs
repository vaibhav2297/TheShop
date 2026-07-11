namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// A single product tile on the catalogue grid.
/// </summary>
public sealed record ProductSummaryDto(
    Guid Id,
    string Name,
    string? ImageUrl,
    decimal OriginalPrice,
    decimal? SalePrice,
    bool IsDiscounted,
    string Currency,
    bool IsInStock,
    string BrandName,
    string? Flavour,
    int? NicotineStrengthMg);

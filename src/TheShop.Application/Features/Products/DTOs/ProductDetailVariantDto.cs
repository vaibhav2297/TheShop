namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// A public option combination with its saved slug, SKU, price, and explicitly ordered images.
/// </summary>
public sealed record ProductDetailVariantDto(
    Guid Id,
    string Label,
    decimal? OriginalPrice,
    decimal? SalePrice,
    decimal? CurrentPrice,
    bool IsAvailable,
    IReadOnlyList<Guid> ImageIds,
    IReadOnlyList<Guid> OptionValueIds,
    string Slug,
    string Sku);

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// A public option combination and its price; target image already includes the primary-image fallback.
/// </summary>
public sealed record ProductDetailVariantDto(
    Guid Id,
    string Label,
    decimal? OriginalPrice,
    decimal? SalePrice,
    decimal? CurrentPrice,
    bool IsAvailable,
    Guid? TargetImageId,
    IReadOnlyList<Guid> OptionValueIds);

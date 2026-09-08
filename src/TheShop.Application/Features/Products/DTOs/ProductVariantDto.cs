namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One generated variant row, as surfaced to the admin edit form. <see cref="Label"/> is the
/// variant's option values joined for display (e.g. "Mango / 50mg").
/// </summary>
public sealed record ProductVariantDto(
    Guid Id,
    string Sku,
    decimal? OriginalPrice,
    decimal? SalePrice,
    bool IsAvailable,
    Guid? PinnedImageId,
    IReadOnlyList<Guid> OptionValueIds,
    string Label);

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// A single product tile on the catalogue grid. <see cref="OriginalPrice"/>/<see cref="SalePrice"/>
/// reflect the product's own pricing for a no-variant product, or its lowest variant price
/// (undiscounted) when it has variants — see <see cref="MinVariantPrice"/>. A <c>null</c>
/// <see cref="OriginalPrice"/> means the product carries no price at all, which a published product
/// cannot (RULE-14) but a draft read through an admin surface can.
/// </summary>
public sealed record ProductSummaryDto(
    Guid Id,
    string Name,
    string? ImageUrl,
    decimal? OriginalPrice,
    decimal? SalePrice,
    bool IsDiscounted,
    string Currency,
    bool IsInStock,
    string BrandName,
    decimal? MinVariantPrice,
    bool HasSellableVariant);

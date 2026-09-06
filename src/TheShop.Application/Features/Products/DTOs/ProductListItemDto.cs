namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One row of the admin product list (FR-2). Prices are carried as raw amounts, not a rendered
/// label — the Web layer owns currency formatting so the row honours the active UI culture.
/// </summary>
/// <param name="EffectivePrice">
/// The price to show for a product that prices itself: its sale price when discounted, otherwise
/// its original price. <c>null</c> when the product is unpriced or prices through its variants.
/// </param>
/// <param name="MinVariantPrice">
/// The lowest variant price, shown as a "from" price. Set only when <paramref name="HasVariants"/>
/// is <c>true</c>; <c>null</c> when no variant carries a price.
/// </param>
/// <param name="HasVariants">
/// <c>true</c> when the product prices through its variants (RULE-19), which makes
/// <paramref name="MinVariantPrice"/> rather than <paramref name="EffectivePrice"/> the price to render.
/// </param>
/// <param name="Currency">
/// The ISO code the row's prices are denominated in, so the Web layer renders the right symbol
/// rather than assuming the storefront default.
/// </param>
public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string Sku,
    string? PrimaryImageUrl,
    string BrandName,
    string CategoryName,
    decimal? EffectivePrice,
    decimal? MinVariantPrice,
    bool HasVariants,
    string Currency,
    bool IsPublished);

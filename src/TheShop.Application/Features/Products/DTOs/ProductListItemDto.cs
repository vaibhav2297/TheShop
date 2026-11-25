namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One row of the admin manage-products list (FR-2, FR-17). Prices are carried as raw amounts,
/// not a rendered label — the Web layer owns currency formatting so the row honours the active
/// UI culture.
/// </summary>
/// <param name="MinPrice">
/// The lowest price the product can be bought at: the lowest priced variant when it has
/// variants, or its own effective price when it has none (plan §5 Decision 13). <c>null</c> when
/// nothing is priced.
/// </param>
/// <param name="MaxPrice">
/// The highest price the product can be bought at, mirroring <paramref name="MinPrice"/>. Equal
/// to <paramref name="MinPrice"/> when the product has one price or no variants; the Web layer
/// collapses the pair into a single displayed amount in that case.
/// </param>
/// <param name="VariantCount">
/// The number of variants the product carries. Rendered as a caption beneath the product name,
/// not a separate column (plan §5 Decision 11).
/// </param>
/// <param name="Currency">
/// The ISO code <paramref name="MinPrice"/>/<paramref name="MaxPrice"/> are denominated in, so
/// the Web layer renders the right symbol rather than assuming the storefront default.
/// </param>
public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string Sku,
    string? PrimaryImageUrl,
    string BrandName,
    string CategoryName,
    decimal? MinPrice,
    decimal? MaxPrice,
    int VariantCount,
    string Currency,
    bool IsPublished);

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// The full admin edit payload for a product — details, gallery, option types, specification
/// rows, and generated variants. <see cref="RowVersion"/> is an opaque optimistic-concurrency
/// token the client never parses, round-tripped verbatim on <c>UpdateProductCommand</c>
/// (Decision 11).
/// </summary>
public sealed record AdminProductDto(
    Guid Id,
    string Name,
    string? Description,
    string Sku,
    Guid CategoryId,
    string CategoryName,
    Guid BrandId,
    string BrandName,
    decimal? OriginalPrice,
    decimal? SalePrice,
    bool IsPublished,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductOptionTypeDto> OptionTypes,
    IReadOnlyList<ProductSpecificationDto> Specifications,
    IReadOnlyList<ProductVariantDto> Variants,
    string RowVersion);

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One published product as shown on the customer product-details page. <see cref="Images"/>
/// lists the shared gallery primary image first. <see cref="OriginalPrice"/>/<see cref="SalePrice"/>
/// carry the product's own pricing only when it has no variants; a variant product prices
/// through <see cref="Variants"/>. <see cref="DescriptionHtml"/> is empty when the persisted
/// markup no longer satisfies the description grammar — <see cref="DescriptionText"/> then
/// carries the plain-text fallback.
/// </summary>
public sealed record ProductDetailsDto(
    Guid Id,
    string Name,
    string? BrandName,
    string DescriptionHtml,
    string DescriptionText,
    decimal? OriginalPrice,
    decimal? SalePrice,
    string Currency,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductOptionTypeDto> OptionTypes,
    IReadOnlyList<ProductSpecificationDto> Specifications,
    IReadOnlyList<ProductDetailsVariantDto> Variants);

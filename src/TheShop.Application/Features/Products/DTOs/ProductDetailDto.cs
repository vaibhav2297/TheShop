namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// Public product detail, with ordered shared images and variant prices; excludes administrative identifiers and concurrency data.
/// </summary>
public sealed record ProductDetailDto(
    Guid Id,
    string Name,
    string? BrandName,
    bool HasVariants,
    string Currency,
    decimal? OriginalPrice,
    decimal? SalePrice,
    decimal? CurrentPrice,
    decimal? MinVariantPrice,
    decimal? MaxVariantPrice,
    string? DescriptionHtml,
    Guid? PrimaryImageId,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductOptionTypeDto> OptionTypes,
    IReadOnlyList<ProductSpecificationDto> Specifications,
    IReadOnlyList<ProductDetailVariantDto> Variants);

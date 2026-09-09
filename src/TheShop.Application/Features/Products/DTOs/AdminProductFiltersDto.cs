using TheShop.Application.Common.Filtering;

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// The filter options offered on the admin manage-products page: every brand and category that
/// actually owns a product (Active or not, plan §5 Decision 7), plus the variant-aware price
/// bounds the price-range control needs.
/// </summary>
public sealed record AdminProductFiltersDto(
    IReadOnlyList<FilterOptionDto> Brands,
    IReadOnlyList<FilterOptionDto> Categories,
    RangeFilterDto PriceRange);

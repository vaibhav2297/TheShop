using TheShop.Domain.Enums;

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One backend-driven filter group for the catalogue's filter sidebar. The UI renders one
/// control per group by <see cref="Kind"/> — it hard-codes no filter set.
/// </summary>
/// <param name="Key">One of <see cref="ProductFilterKeys"/> — echoed back on the page query.</param>
/// <param name="LabelKey">A resource key naming the group (e.g. <c>Filter_Category</c>).</param>
/// <param name="Options">Populated when <paramref name="Kind"/> is <see cref="FilterKind.MultiSelect"/>.</param>
/// <param name="Range">Populated when <paramref name="Kind"/> is <see cref="FilterKind.Range"/>.</param>
public sealed record FilterGroupDto(
    string Key,
    string LabelKey,
    FilterKind Kind,
    IReadOnlyList<FilterOptionDto> Options,
    PriceRangeDto? Range);

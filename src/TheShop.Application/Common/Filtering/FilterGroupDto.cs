using TheShop.Domain.Enums;

namespace TheShop.Application.Common.Filtering;

/// <summary>
/// One backend-driven filter group for a filter sidebar. The UI renders one control per group by
/// <see cref="Kind"/> — it hard-codes no filter set.
/// </summary>
/// <param name="Key">A feature-defined key — echoed back on the page query.</param>
/// <param name="LabelKey">A resource key naming the group (e.g. <c>Filter_Category</c>).</param>
/// <param name="Options">Populated when <paramref name="Kind"/> is <see cref="FilterKind.MultiSelect"/> or <see cref="FilterKind.SingleSelect"/>.</param>
/// <param name="Range">Populated when <paramref name="Kind"/> is <see cref="FilterKind.Range"/>.</param>
public sealed record FilterGroupDto(
    string Key,
    string LabelKey,
    FilterKind Kind,
    IReadOnlyList<FilterOptionDto> Options,
    RangeFilterDto? Range);

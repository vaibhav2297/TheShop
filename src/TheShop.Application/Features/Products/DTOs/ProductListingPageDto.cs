using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>A listing page with scoped variant choices and reconciled selections.</summary>
public sealed record ProductListingPageDto<TItem>(
    PagedResult<TItem> Page,
    IReadOnlyList<FilterGroupDto> VariantGroups,
    IReadOnlyList<AppliedFilterDto> EffectiveVariantFilters,
    bool RemovedVariantSelections);

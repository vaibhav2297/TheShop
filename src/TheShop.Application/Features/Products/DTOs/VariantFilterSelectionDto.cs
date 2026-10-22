using TheShop.Application.Common.Filtering;

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>Valid variant selections and whether any requested selection was unavailable.</summary>
public sealed record VariantFilterSelectionDto(
    IReadOnlyList<AppliedFilterDto> Filters,
    bool RemovedSelections);

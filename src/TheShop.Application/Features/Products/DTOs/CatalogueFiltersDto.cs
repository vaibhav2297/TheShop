namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// The complete set of backend-driven filter groups for the catalogue's filter sidebar.
/// </summary>
public sealed record CatalogueFiltersDto(IReadOnlyList<FilterGroupDto> Groups);

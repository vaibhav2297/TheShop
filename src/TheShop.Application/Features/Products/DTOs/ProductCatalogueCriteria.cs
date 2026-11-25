using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;
using TheShop.Domain.Enums;

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// Normalized input to <c>IProductRepository.GetPageAsync</c> — the Application-layer
/// projection of <c>GetProductCataloguePageQuery</c> after validation and pagination clamping.
/// </summary>
public sealed record ProductCatalogueCriteria(
    IReadOnlyList<AppliedFilterDto> SelectedFilters,
    decimal? PriceMin,
    decimal? PriceMax,
    ProductSortOption Sort,
    PaginationRequest Pagination);

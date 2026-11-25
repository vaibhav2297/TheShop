using MediatR;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Enums;

namespace TheShop.Application.Features.Products.Queries.GetProductCataloguePage;

/// <summary>
/// Requests one page of the product catalogue grid, narrowed by <see cref="SelectedFilters"/>
/// and the price range, ordered by <see cref="Sort"/>.
/// </summary>
public sealed record GetProductCataloguePageQuery(
    IReadOnlyList<AppliedFilterDto> SelectedFilters,
    decimal? PriceMin,
    decimal? PriceMax,
    ProductSortOption Sort,
    PaginationRequest Pagination) : IRequest<Result<PagedResult<ProductSummaryDto>>>;

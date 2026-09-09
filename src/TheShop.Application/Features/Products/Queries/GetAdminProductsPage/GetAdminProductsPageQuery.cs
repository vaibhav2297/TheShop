using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Enums;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductsPage;

/// <summary>
/// Requests one page of the admin product list, narrowed by <paramref name="Search"/>,
/// <paramref name="Status"/>, <paramref name="BrandIds"/>, <paramref name="CategoryIds"/>, and
/// <paramref name="PriceMin"/>/<paramref name="PriceMax"/>, ordered by <paramref name="Sort"/>.
/// Every narrowing criterion is optional: a <see langword="null"/> value applies no narrowing on
/// that criterion (AC-1..AC-5).
/// </summary>
[RequiresPermission("products.view")]
public sealed record GetAdminProductsPageQuery(
    string? Search,
    ProductStatusFilter? Status,
    IReadOnlyList<Guid>? BrandIds,
    IReadOnlyList<Guid>? CategoryIds,
    decimal? PriceMin,
    decimal? PriceMax,
    AdminProductSortOption Sort,
    PaginationRequest Pagination) : IRequest<Result<PagedResult<ProductListItemDto>>>;

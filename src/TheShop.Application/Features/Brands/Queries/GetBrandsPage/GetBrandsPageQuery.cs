using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Domain.Enums;

namespace TheShop.Application.Features.Brands.Queries.GetBrandsPage;

/// <summary>
/// Requests one page of the manage-brands admin list, narrowed by <paramref name="Search"/> and
/// <paramref name="Status"/>, ordered by <paramref name="Sort"/>. Both narrowing criteria are
/// optional: a <see langword="null"/> <paramref name="Status"/> returns brands of every status.
/// </summary>
[RequiresPermission("brands.view")]
public sealed record GetBrandsPageQuery(
    string? Search,
    BrandStatusFilter? Status,
    BrandSortOption Sort,
    PaginationRequest Pagination) : IRequest<Result<PagedResult<BrandListItemDto>>>;

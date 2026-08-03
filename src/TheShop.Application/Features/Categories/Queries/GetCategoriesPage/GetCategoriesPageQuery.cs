using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Domain.Enums;

namespace TheShop.Application.Features.Categories.Queries.GetCategoriesPage;

/// <summary>
/// Requests one page of the manage-categories admin list, narrowed by
/// <paramref name="Search"/> and <paramref name="Status"/>, ordered by <paramref name="Sort"/>.
/// Both narrowing criteria are optional: a <see langword="null"/> <paramref name="Status"/>
/// returns categories of every status.
/// </summary>
[RequiresPermission("categories.view")]
public sealed record GetCategoriesPageQuery(
    string? Search,
    CategoryStatusFilter? Status,
    CategorySortOption Sort,
    PaginationRequest Pagination) : IRequest<Result<PagedResult<CategoryListItemDto>>>;

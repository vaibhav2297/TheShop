using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductsPage;

/// <summary>
/// Requests one page of the admin product list — published and unpublished alike, newest
/// first, with no search/filter/sort (AC-1, AC-2).
/// </summary>
[RequiresPermission("products.view")]
public sealed record GetAdminProductsPageQuery(PaginationRequest Pagination)
    : IRequest<Result<PagedResult<ProductListItemDto>>>;

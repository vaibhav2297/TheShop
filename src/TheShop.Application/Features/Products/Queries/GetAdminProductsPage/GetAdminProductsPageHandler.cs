using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductsPage;

/// <summary>
/// Handles <see cref="GetAdminProductsPageQuery"/>. Normalizes pagination to the fixed page size,
/// blank-collapses <c>Search</c>, builds the immutable <see cref="AdminProductCriteria"/>, and
/// delegates the read to <see cref="IProductRepository"/>.
/// </summary>
public sealed class GetAdminProductsPageHandler(IProductRepository products)
    : IRequestHandler<GetAdminProductsPageQuery, Result<PagedResult<ProductListItemDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<PagedResult<ProductListItemDto>>> Handle(
        GetAdminProductsPageQuery request,
        CancellationToken cancellationToken)
    {
        var pagination = request.Pagination.Normalized(GetAdminProductsPageQueryValidator.PageSize);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        var criteria = new AdminProductCriteria(
            search,
            request.Status,
            request.BrandIds,
            request.CategoryIds,
            request.PriceMin,
            request.PriceMax,
            request.Sort,
            pagination);

        var page = await products.GetAdminPageAsync(criteria, cancellationToken);

        return Result.Ok(page);
    }
}

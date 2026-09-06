using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductsPage;

/// <summary>
/// Handles <see cref="GetAdminProductsPageQuery"/>. Normalizes pagination to the fixed page
/// size and delegates the read to <see cref="IProductRepository"/>.
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

        var page = await products.GetAdminPageAsync(pagination, cancellationToken);

        return Result.Ok(page);
    }
}

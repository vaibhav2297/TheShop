using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Mappers;

namespace TheShop.Application.Features.Products.Queries.GetProductCataloguePage;

/// <summary>
/// Handles <see cref="GetProductCataloguePageQuery"/>. Normalizes pagination, delegates the
/// filtered/sorted/paged read to <see cref="IProductRepository"/>, and maps the result to
/// <see cref="ProductSummaryDto"/>.
/// </summary>
public sealed class GetProductCataloguePageHandler(IProductRepository products)
    : IRequestHandler<GetProductCataloguePageQuery, Result<PagedResult<ProductSummaryDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<PagedResult<ProductSummaryDto>>> Handle(
        GetProductCataloguePageQuery request,
        CancellationToken cancellationToken)
    {
        var pagination = request.Pagination.Normalized(GetProductCataloguePageQueryValidator.MaxPageSize);

        var criteria = new ProductCatalogueCriteria(
            request.SelectedFilters,
            request.PriceMin,
            request.PriceMax,
            request.Sort,
            pagination);

        var page = await products.GetPageAsync(criteria, cancellationToken);

        return Result.Ok(page.MapItems(ProductDtoMapper.ToSummaryDto));
    }
}

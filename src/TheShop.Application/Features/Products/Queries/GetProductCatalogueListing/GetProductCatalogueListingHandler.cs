using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Filtering;

namespace TheShop.Application.Features.Products.Queries.GetProductCatalogueListing;

/// <summary>Reconciles public selections before dispatching the existing paged catalogue read.</summary>
public sealed class GetProductCatalogueListingHandler(IProductRepository products, ISender sender)
    : IRequestHandler<GetProductCatalogueListingQuery, Result<ProductListingPageDto<ProductSummaryDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<ProductListingPageDto<ProductSummaryDto>>> Handle(
        GetProductCatalogueListingQuery request, CancellationToken cancellationToken)
    {
        var groups = await products.GetFilterGroupsAsync(cancellationToken);
        var variantGroups = groups.Where(group => ProductFilterKeys.IsVariantKey(group.Key)).ToList();
        var selection = VariantFilterSelection.Reconcile(request.PageQuery.SelectedFilters, variantGroups);
        var query = request.PageQuery with
        {
            SelectedFilters = [.. request.PageQuery.SelectedFilters.Where(filter => !ProductFilterKeys.IsVariantKey(filter.Key)),
                .. selection.Filters],
        };
        var result = await sender.Send(query, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result.IsSuccess
            ? Result.Ok(new ProductListingPageDto<ProductSummaryDto>(result.Value, variantGroups, selection.Filters, selection.RemovedSelections))
            : Result.Fail<ProductListingPageDto<ProductSummaryDto>>(result.Error!, result.ErrorArgs, result.ValidationErrors);
    }
}

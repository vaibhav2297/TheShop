using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Filtering;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductListing;

/// <summary>Reconciles management selections and delegates paging without narrowing price rollups.</summary>
public sealed class GetAdminProductListingHandler(IProductRepository products, ISender sender)
    : IRequestHandler<GetAdminProductListingQuery, Result<ProductListingPageDto<ProductListItemDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<ProductListingPageDto<ProductListItemDto>>> Handle(
        GetAdminProductListingQuery request, CancellationToken cancellationToken)
    {
        var metadata = await products.GetAdminFiltersAsync(cancellationToken);
        var selection = VariantFilterSelection.Reconcile(request.PageQuery.VariantFilters ?? [], metadata.VariantGroups);
        var result = await sender.Send(request.PageQuery with { VariantFilters = selection.Filters }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result.IsSuccess
            ? Result.Ok(new ProductListingPageDto<ProductListItemDto>(result.Value, metadata.VariantGroups, selection.Filters, selection.RemovedSelections))
            : Result.Fail<ProductListingPageDto<ProductListItemDto>>(result.Error!, result.ErrorArgs, result.ValidationErrors);
    }
}

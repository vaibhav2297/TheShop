using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.GetCatalogueFilters;

/// <summary>
/// Handles <see cref="GetCatalogueFiltersQuery"/> by delegating to
/// <see cref="IProductRepository.GetFilterGroupsAsync"/>.
/// </summary>
public sealed class GetCatalogueFiltersHandler(IProductRepository products)
    : IRequestHandler<GetCatalogueFiltersQuery, Result<CatalogueFiltersDto>>
{
    public async Task<Result<CatalogueFiltersDto>> Handle(
        GetCatalogueFiltersQuery request,
        CancellationToken cancellationToken)
    {
        var groups = await products.GetFilterGroupsAsync(cancellationToken);
        return Result.Ok(new CatalogueFiltersDto(groups));
    }
}

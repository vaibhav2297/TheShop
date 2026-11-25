using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductFilters;

/// <summary>
/// Handles <see cref="GetAdminProductFiltersQuery"/>. Delegates directly to
/// <see cref="IProductRepository"/> — the filter set has no request-side narrowing to apply.
/// </summary>
public sealed class GetAdminProductFiltersHandler(IProductRepository products)
    : IRequestHandler<GetAdminProductFiltersQuery, Result<AdminProductFiltersDto>>
{
    /// <inheritdoc/>
    public async Task<Result<AdminProductFiltersDto>> Handle(
        GetAdminProductFiltersQuery request, CancellationToken cancellationToken)
    {
        var filters = await products.GetAdminFiltersAsync(cancellationToken);

        return Result.Ok(filters);
    }
}

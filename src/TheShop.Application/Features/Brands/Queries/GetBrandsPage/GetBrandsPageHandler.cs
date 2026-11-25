using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;

namespace TheShop.Application.Features.Brands.Queries.GetBrandsPage;

/// <summary>
/// Handles <see cref="GetBrandsPageQuery"/>. Normalizes pagination to the fixed page size and
/// delegates the filtered/sorted/paged read — including per-brand product counts — to
/// <see cref="IBrandRepository"/>.
/// </summary>
public sealed class GetBrandsPageHandler(IBrandRepository brands)
    : IRequestHandler<GetBrandsPageQuery, Result<PagedResult<BrandListItemDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<PagedResult<BrandListItemDto>>> Handle(
        GetBrandsPageQuery request,
        CancellationToken cancellationToken)
    {
        var pagination = request.Pagination.Normalized(GetBrandsPageQueryValidator.PageSize);

        var page = await brands.GetPageAsync(
            request.Search, request.Status, request.Sort, pagination, cancellationToken);

        return Result.Ok(page);
    }
}

using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;

namespace TheShop.Application.Features.Brands.Queries.GetActiveBrands;

/// <summary>
/// Handles <see cref="GetActiveBrandsQuery"/> by delegating to
/// <see cref="IBrandRepository.GetActiveLookupAsync"/>.
/// </summary>
public sealed class GetActiveBrandsHandler(IBrandRepository brands)
    : IRequestHandler<GetActiveBrandsQuery, Result<IReadOnlyList<BrandLookupDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<BrandLookupDto>>> Handle(
        GetActiveBrandsQuery request, CancellationToken cancellationToken) =>
        Result.Ok(await brands.GetActiveLookupAsync(cancellationToken));
}

using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Mappers;

namespace TheShop.Application.Features.Brands.Queries.GetBrandById;

/// <summary>
/// Handles <see cref="GetBrandByIdQuery"/>.
/// </summary>
public sealed class GetBrandByIdHandler(IBrandRepository brands, IFileStorage fileStorage)
    : IRequestHandler<GetBrandByIdQuery, Result<BrandDto>>
{
    /// <inheritdoc/>
    public async Task<Result<BrandDto>> Handle(GetBrandByIdQuery request, CancellationToken cancellationToken)
    {
        var brand = await brands.GetByIdAsync(request.Id, cancellationToken);
        if (brand is null)
            return Result.Fail<BrandDto>(BrandErrorKeys.NotFound);

        return Result.Ok(BrandDtoMapper.ToDto(brand, fileStorage));
    }
}

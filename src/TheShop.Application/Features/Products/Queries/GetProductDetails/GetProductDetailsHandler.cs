using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Mappers;

namespace TheShop.Application.Features.Products.Queries.GetProductDetails;

/// <summary>
/// Handles <see cref="GetProductDetailsQuery"/>. Only published products are visible, whatever
/// the caller's permissions; a missing or unpublished product returns
/// <see cref="ProductErrorKeys.NotFound"/> rather than throwing.
/// </summary>
public sealed class GetProductDetailsHandler(IProductRepository products, IFileStorage fileStorage)
    : IRequestHandler<GetProductDetailsQuery, Result<ProductDetailsDto>>
{
    /// <inheritdoc/>
    public async Task<Result<ProductDetailsDto>> Handle(GetProductDetailsQuery request, CancellationToken cancellationToken)
    {
        var product = await products.GetPublishedByIdAsync(request.Id, cancellationToken);
        if (product is null || !product.IsPublished)
            return Result.Fail<ProductDetailsDto>(ProductErrorKeys.NotFound);

        return Result.Ok(ProductDetailsDtoMapper.ToDetailsDto(product, fileStorage));
    }
}

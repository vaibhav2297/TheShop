using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Mappers;

namespace TheShop.Application.Features.Products.Queries.GetProductDetail;

/// <summary>Returns public detail only for published products, including for privileged callers.</summary>
public sealed class GetProductDetailHandler(IProductRepository products, IFileStorage fileStorage)
    : IRequestHandler<GetProductDetailQuery, Result<ProductDetailDto>>
{
    /// <inheritdoc/>
    public async Task<Result<ProductDetailDto>> Handle(GetProductDetailQuery request, CancellationToken cancellationToken)
    {
        var product = await products.GetPublishedDetailAsync(request.Id, cancellationToken);
        return product is null || !product.IsPublished
            ? Result.Fail<ProductDetailDto>(ProductErrorKeys.DetailUnavailable)
            : Result.Ok(ProductDetailDtoMapper.ToDto(product, fileStorage));
    }
}

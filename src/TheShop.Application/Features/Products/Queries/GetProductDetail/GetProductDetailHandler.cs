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
        var product = request.Id is Guid id
            ? await products.GetPublishedDetailAsync(id, cancellationToken)
            : await products.GetPublishedDetailBySlugAsync(request.ProductSlug!, cancellationToken);
        if (product is null || !product.IsPublished)
            return Result.Fail<ProductDetailDto>(ProductErrorKeys.DetailUnavailable);

        var addresses = await products.GetAddressesAsync(product.Id, cancellationToken);
        if (addresses is null || (request.ProductSlug is not null && request.ProductSlug != addresses.ProductSlug))
            return Result.Fail<ProductDetailDto>(ProductErrorKeys.DetailUnavailable);

        var dto = ProductDetailDtoMapper.ToDto(product, fileStorage, addresses);
        var selected = ProductDetailVariantResolver.ResolveInitial(dto, request.VariantSlug);
        if ((request.VariantSlug is not null || product.HasVariants) && selected is null)
            return Result.Fail<ProductDetailDto>(ProductErrorKeys.DetailUnavailable);

        return Result.Ok(dto with { SelectedVariantId = selected?.Id });
    }
}

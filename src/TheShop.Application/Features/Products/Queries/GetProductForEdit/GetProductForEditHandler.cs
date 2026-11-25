using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Mappers;

namespace TheShop.Application.Features.Products.Queries.GetProductForEdit;

/// <summary>
/// Handles <see cref="GetProductForEditQuery"/>. A miss is not a technical failure — it is the
/// expected outcome of a stale edit link (AC-34) — so it returns
/// <see cref="ProductErrorKeys.NotFound"/> rather than throwing.
/// </summary>
public sealed class GetProductForEditHandler(IProductRepository products, IFileStorage fileStorage)
    : IRequestHandler<GetProductForEditQuery, Result<AdminProductDto>>
{
    /// <inheritdoc/>
    public async Task<Result<AdminProductDto>> Handle(GetProductForEditQuery request, CancellationToken cancellationToken)
    {
        var loaded = await products.GetForEditAsync(request.Id, cancellationToken);
        if (loaded is null)
            return Result.Fail<AdminProductDto>(ProductErrorKeys.NotFound);

        var (product, rowVersion) = loaded.Value;

        return Result.Ok(AdminProductDtoMapper.ToAdminDto(product, fileStorage, rowVersion));
    }
}

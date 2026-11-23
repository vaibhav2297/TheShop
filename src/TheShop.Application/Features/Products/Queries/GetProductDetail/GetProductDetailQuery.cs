using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.GetProductDetail;

/// <summary>Requests one published product's public detail without requiring authentication.</summary>
public sealed record GetProductDetailQuery(Guid? Id = null, string? ProductSlug = null, string? VariantSlug = null)
    : IRequest<Result<ProductDetailDto>>;

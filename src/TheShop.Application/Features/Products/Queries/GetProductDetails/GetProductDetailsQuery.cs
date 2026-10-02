using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.GetProductDetails;

/// <summary>
/// Requests the customer product-details payload — details, gallery, options, specifications,
/// and variants — for one published product, by id. Open to guests and signed-in customers alike.
/// </summary>
public sealed record GetProductDetailsQuery(Guid Id) : IRequest<Result<ProductDetailsDto>>;

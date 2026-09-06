using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.GetProductForEdit;

/// <summary>
/// Requests the full edit payload — details, gallery, option types, and variants — for one
/// product, by id.
/// </summary>
[RequiresPermission("products.view")]
public sealed record GetProductForEditQuery(Guid Id) : IRequest<Result<AdminProductDto>>;

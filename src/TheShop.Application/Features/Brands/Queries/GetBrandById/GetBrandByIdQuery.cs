using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;

namespace TheShop.Application.Features.Brands.Queries.GetBrandById;

/// <summary>
/// Requests a single brand by id, for the edit form. Requires only <c>brands.view</c> — the edit
/// page's <c>brands.edit</c> gate is what denies a view-only admin's direct link (plan §5
/// Decision 11).
/// </summary>
[RequiresPermission("brands.view")]
public sealed record GetBrandByIdQuery(Guid Id) : IRequest<Result<BrandDto>>;

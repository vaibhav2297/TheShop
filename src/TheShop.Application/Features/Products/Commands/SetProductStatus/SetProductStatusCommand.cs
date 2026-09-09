using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Commands.SetProductStatus;

/// <summary>
/// Activates or deactivates every product in <paramref name="ProductIds"/>. Covers both the
/// inline single-row toggle and the bulk action (plan §5 Decision 2) — a one-element list is the
/// single-row case.
/// </summary>
[RequiresPermission("products.edit")]
public sealed record SetProductStatusCommand(
    IReadOnlyList<Guid> ProductIds,
    bool IsActive) : IRequest<Result<ProductStatusChangeDto>>;

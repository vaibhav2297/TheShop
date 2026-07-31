using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;

namespace TheShop.Application.Features.Brands.Commands.SetBrandStatus;

/// <summary>
/// Activates or deactivates every brand in <paramref name="BrandIds"/>. Covers both the inline
/// single-row toggle and the bulk action (plan §5 Decision 1) — a one-element list is the
/// single-row case.
/// </summary>
[RequiresPermission("brands.edit")]
public sealed record SetBrandStatusCommand(
    IReadOnlyList<Guid> BrandIds,
    bool IsActive) : IRequest<Result<BrandStatusChangeDto>>;

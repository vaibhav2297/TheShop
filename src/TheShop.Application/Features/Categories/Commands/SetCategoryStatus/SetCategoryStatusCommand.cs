using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;

namespace TheShop.Application.Features.Categories.Commands.SetCategoryStatus;

/// <summary>
/// Activates or deactivates every category in <paramref name="CategoryIds"/>. Covers both the
/// inline single-row toggle and the bulk action (plan §5 Decision 4) — a one-element list is the
/// single-row case.
/// </summary>
[RequiresPermission("categories.edit")]
public sealed record SetCategoryStatusCommand(
    IReadOnlyList<Guid> CategoryIds,
    bool IsActive) : IRequest<Result<CategoryStatusChangeDto>>;

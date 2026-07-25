using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Admin.DTOs;

namespace TheShop.Application.Features.Admin.Queries.GetAdminDashboard;

/// <summary>
/// Handles <see cref="GetAdminDashboardQuery"/> by counting each module the current user is
/// permitted to view. A module the user cannot view is omitted entirely (spec FR-5 / RULE-2); a
/// module whose count cannot be retrieved still renders with a placeholder while the rest of the
/// dashboard is unaffected (spec FR-7 / AC-5).
/// </summary>
public sealed class GetAdminDashboardHandler(
    IAdminDashboardRepository repository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetAdminDashboardQuery, Result<AdminDashboardDto>>
{
    /// <inheritdoc/>
    public async Task<Result<AdminDashboardDto>> Handle(
        GetAdminDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var cards = new List<AdminModuleCardDto>();

        foreach (var entry in AdminDashboardCatalogue.Modules)
        {
            if (!currentUser.HasPermission(entry.ViewPermissionCode))
                continue;

            int? count;
            try
            {
                count = await repository.CountModuleAsync(entry.Module, cancellationToken);
            }
            catch (Exception)
            {
                count = null;
            }

            cards.Add(new AdminModuleCardDto(entry.Module, count));
        }

        return Result.Ok(new AdminDashboardDto(cards));
    }
}

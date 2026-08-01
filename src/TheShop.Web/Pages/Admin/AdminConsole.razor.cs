using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using TheShop.Application.Features.Admin.DTOs;
using TheShop.Application.Features.Admin.Queries.GetAdminDashboard;
using TheShop.Web.Common;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// The admin console landing page at <c>/admin</c> — one overview card per governed module the
/// signed-in staff member is permitted to view, each showing a current record count and a link
/// to that module's management page (spec Behavior 1). Gated on its own screen permission,
/// <c>dashboard.view</c> (<c>PolicyNames.AdminDashboard</c>), like every other admin screen;
/// per-module visibility is decided by the query handler, so no per-card <c>AuthorizeView</c>
/// is needed here.
/// </summary>
[Route(Routes.Admin.Console)]
[Authorize(Policy = PolicyNames.AdminDashboard)]
public partial class AdminConsole : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    private IReadOnlyList<AdminModuleCardDto> _modules = [];

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        await BusyState.RunAsync(BusyKeys.AdminDashboard, async () =>
        {
            var result = await Mediator.Send(new GetAdminDashboardQuery());
            if (result.IsSuccess)
                _modules = result.Value.Modules;
        });
    }
}

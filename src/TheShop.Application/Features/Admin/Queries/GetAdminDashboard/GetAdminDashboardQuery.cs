using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Admin.DTOs;

namespace TheShop.Application.Features.Admin.Queries.GetAdminDashboard;

/// <summary>
/// Requests the admin console dashboard's module cards for the current user. Gated on the
/// dashboard screen's own permission, <c>dashboard.view</c>; each module card is additionally
/// gated by <see cref="Common.Interfaces.ICurrentUserService.HasPermission"/> inside the
/// handler (spec FR-5 / RULE-2).
/// </summary>
[RequiresPermission("dashboard.view")]
public sealed record GetAdminDashboardQuery : IRequest<Result<AdminDashboardDto>>;

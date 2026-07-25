using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Admin.DTOs;

namespace TheShop.Application.Features.Admin.Queries.GetAdminDashboard;

/// <summary>
/// Requests the admin console dashboard's module cards for the current user. Carries no
/// permission requirement of its own — the page is gated by the admin-area policy, and each
/// module card is individually gated by <see cref="Common.Interfaces.ICurrentUserService.HasPermission"/>
/// inside the handler (spec: access to <c>/admin</c> requires only one admin-area permission).
/// </summary>
public sealed record GetAdminDashboardQuery : IRequest<Result<AdminDashboardDto>>;

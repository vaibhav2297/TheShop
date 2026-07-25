namespace TheShop.Application.Features.Admin.DTOs;

/// <summary>
/// The admin console dashboard's full set of cards — only modules the current user is permitted
/// to view are present (spec FR-5 / RULE-2). An empty list means the user holds no admin-area
/// permission for any of the five governed modules (spec Edge case 1).
/// </summary>
public sealed record AdminDashboardDto(IReadOnlyList<AdminModuleCardDto> Modules);

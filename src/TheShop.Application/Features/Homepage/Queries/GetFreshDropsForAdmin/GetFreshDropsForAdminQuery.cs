using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Queries.GetFreshDropsForAdmin;

/// <summary>Loads Fresh Drops for a staff member with Homepage View.</summary>
[RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode)]
public sealed record GetFreshDropsForAdminQuery : IRequest<Result<FreshDropsDto?>>;

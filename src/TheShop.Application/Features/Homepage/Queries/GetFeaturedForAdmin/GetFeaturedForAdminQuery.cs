using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Queries.GetFeaturedForAdmin;

/// <summary>Reads the saved configuration; successful null confirms absence.</summary>
[RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode)]
public sealed record GetFeaturedForAdminQuery : IRequest<Result<FeaturedDto?>>;

using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Queries.GetHeroBannerForAdmin;

/// <summary>Reads the admin configuration; successful null confirms no saved banner.</summary>
[RequiresPermission(PermissionCatalogue.Homepage.ViewCode)]
public sealed record GetHeroBannerForAdminQuery : IRequest<Result<HeroBannerDto?>>;


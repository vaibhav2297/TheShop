using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Queries.ValidateHeroBannerImage;

/// <summary>Validates a local candidate before replacing the accepted preview.</summary>
[RequiresPermission(PermissionCatalogue.Homepage.ViewCode)]
public sealed record ValidateHeroBannerImageQuery(HeroBannerImageRole ImageRole, ImageUpload Image) : IRequest<Result>;


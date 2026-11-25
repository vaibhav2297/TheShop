using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Queries.ValidateFeaturedImage;

/// <summary>Validates a local candidate before replacing accepted tile artwork.</summary>
[RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode)]
public sealed record ValidateFeaturedImageQuery(ImageUpload Image) : IRequest<Result>;

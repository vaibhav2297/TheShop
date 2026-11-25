using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Validation;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Commands.UpdateFeatured;

/// <summary>Replaces the saved Featured configuration with normalized draft content.</summary>
[RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode, PermissionCatalogue.Homepage.EditCode)]
public sealed record UpdateFeaturedCommand(string Title, FeaturedTileInput Tile1,
    FeaturedTileInput Tile2, bool IsVisible) : IRequest<Result>
{
    public string Title { get; init; } = HeroBannerInput.Required(Title);
}

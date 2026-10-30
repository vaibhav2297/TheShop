using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Validation;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Commands.UpdateHeroBanner;

/// <summary>Updates the saved singleton with normalized text and staged artwork.</summary>
[RequiresPermission(PermissionCatalogue.Homepage.EditCode)]
public sealed record UpdateHeroBannerCommand(string Heading, string? Description, string? ButtonLabel,
    string? ButtonLink, ImageUpload? NewDesktopImage, ImageUpload? NewMobileImage,
    bool RemoveDesktopImage, bool RemoveMobileImage, bool IsVisible) : IRequest<Result>
{
    public string Heading { get; init; } = HeroBannerInput.Required(Heading);
    public string? Description { get; init; } = HeroBannerInput.Optional(Description);
    public string? ButtonLabel { get; init; } = HeroBannerInput.Optional(ButtonLabel);
    public string? ButtonLink { get; init; } = HeroBannerInput.Optional(ButtonLink);
}

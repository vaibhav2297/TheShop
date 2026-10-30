using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Validation;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Commands.CreateHeroBanner;

/// <summary>Creates the singleton with normalized text and staged artwork.</summary>
[RequiresPermission(PermissionCatalogue.Homepage.CreateCode)]
public sealed record CreateHeroBannerCommand(string Heading, string? Description, string? ButtonLabel,
    string? ButtonLink, ImageUpload? DesktopImage, ImageUpload? MobileImage, bool IsVisible) : IRequest<Result>
{
    public string Heading { get; init; } = HeroBannerInput.Required(Heading);
    public string? Description { get; init; } = HeroBannerInput.Optional(Description);
    public string? ButtonLabel { get; init; } = HeroBannerInput.Optional(ButtonLabel);
    public string? ButtonLink { get; init; } = HeroBannerInput.Optional(ButtonLink);
}

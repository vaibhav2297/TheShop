using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Validation;
using TheShop.Domain.Enums;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Commands.CreateFreshDrops;

/// <summary>Creates the absent Fresh Drops configuration.</summary>
[RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode, PermissionCatalogue.Homepage.CreateCode)]
public sealed record CreateFreshDropsCommand(string Title, string? ButtonLabel, string? ButtonLink,
    FreshDropsSelectionMode SelectionMode, IReadOnlyList<Guid> ManualProductIds, bool IsVisible) : IRequest<Result>
{
    /// <summary>Normalized title.</summary>
    public string Title { get; init; } = HeroBannerInput.Required(Title);
    /// <summary>Normalized optional button label.</summary>
    public string? ButtonLabel { get; init; } = HeroBannerInput.Optional(ButtonLabel);
    /// <summary>Normalized optional button link.</summary>
    public string? ButtonLink { get; init; } = HeroBannerInput.Optional(ButtonLink);
}

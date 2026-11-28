using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Validation;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Commands.UpdateHomepageBrand;

/// <summary>Replaces the saved homepage brand section for staff with Homepage Edit.</summary>
[RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode, PermissionCatalogue.Homepage.EditCode)]
public sealed record UpdateHomepageBrandCommand(string Title, string ButtonLabel, string ButtonLink,
    IReadOnlyList<Guid> BrandIds, bool IsVisible) : IRequest<Result>
{
    /// <summary>Normalized section title.</summary>
    public string Title { get; init; } = HeroBannerInput.Required(Title);
    /// <summary>Normalized button text.</summary>
    public string ButtonLabel { get; init; } = HeroBannerInput.Required(ButtonLabel);
    /// <summary>Normalized destination.</summary>
    public string ButtonLink { get; init; } = HeroBannerInput.Required(ButtonLink);
}

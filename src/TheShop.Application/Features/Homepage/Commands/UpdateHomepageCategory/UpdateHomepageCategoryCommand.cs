using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Validation;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Commands.UpdateHomepageCategory;

/// <summary>Replaces the saved homepage category section.</summary>
[RequiresAllPermissions(PermissionCatalogue.Homepage.ViewCode, PermissionCatalogue.Homepage.EditCode)]
public sealed record UpdateHomepageCategoryCommand(string Title, string ButtonLabel, string ButtonLink,
    IReadOnlyList<Guid> CategoryIds, bool IsVisible) : IRequest<Result>
{
    /// <summary>Normalized section title.</summary>
    public string Title { get; init; } = HeroBannerInput.Required(Title);
    /// <summary>Normalized button text.</summary>
    public string ButtonLabel { get; init; } = HeroBannerInput.Required(ButtonLabel);
    /// <summary>Normalized button destination.</summary>
    public string ButtonLink { get; init; } = HeroBannerInput.Required(ButtonLink);
}

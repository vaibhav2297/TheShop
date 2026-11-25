using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Validation;

namespace TheShop.Application.Features.Homepage.DTOs;

/// <summary>Normalized tile draft; existing artwork is resolved from persistence, never from client keys.</summary>
public sealed record FeaturedTileInput(string? Label, string Title, string? ButtonLabel,
    string? ButtonLink, ImageUpload? NewImage, bool RemoveImage)
{
    public string? Label { get; init; } = HeroBannerInput.Optional(Label);
    public string Title { get; init; } = HeroBannerInput.Required(Title);
    public string? ButtonLabel { get; init; } = HeroBannerInput.Optional(ButtonLabel);
    public string? ButtonLink { get; init; } = HeroBannerInput.Optional(ButtonLink);
}

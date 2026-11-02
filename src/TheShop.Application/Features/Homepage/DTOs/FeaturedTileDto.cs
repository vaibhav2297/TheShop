namespace TheShop.Application.Features.Homepage.DTOs;

/// <summary>The saved content of one fixed Featured tile.</summary>
public sealed record FeaturedTileDto(string? Label, string Title, string? ButtonLabel,
    string? ButtonLink, FeaturedImageDto Image);

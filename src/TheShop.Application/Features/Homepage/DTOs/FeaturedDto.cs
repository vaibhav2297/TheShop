namespace TheShop.Application.Features.Homepage.DTOs;

/// <summary>The complete administrative Featured configuration.</summary>
public sealed record FeaturedDto(string Title, FeaturedTileDto Tile1, FeaturedTileDto Tile2, bool IsVisible);

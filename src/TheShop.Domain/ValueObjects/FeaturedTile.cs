using TheShop.Domain.Exceptions;

namespace TheShop.Domain.ValueObjects;

/// <summary>Immutable content and private artwork for one fixed Featured position.</summary>
public sealed record FeaturedTile
{
    public string? Label { get; }
    public string Title { get; }
    public string? ButtonLabel { get; }
    public string? ButtonLink { get; }
    public string ImageKey { get; }

    private FeaturedTile(string? label, string title, string? buttonLabel, string? buttonLink, string imageKey)
        => (Label, Title, ButtonLabel, ButtonLink, ImageKey) = (label, title, buttonLabel, buttonLink, imageKey);

    /// <summary>Creates a tile with required title/artwork and an optional complete button pair.</summary>
    /// <exception cref="FeaturedTileTitleRequiredException">Title is blank.</exception>
    /// <exception cref="FeaturedImageRequiredException">Artwork is blank.</exception>
    /// <exception cref="FeaturedButtonPairRequiredException">Only one button field is supplied.</exception>
    public static FeaturedTile Create(string? label, string title, string? buttonLabel, string? buttonLink, string imageKey)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new FeaturedTileTitleRequiredException();
        if (string.IsNullOrWhiteSpace(imageKey)) throw new FeaturedImageRequiredException();
        if (string.IsNullOrWhiteSpace(buttonLabel) != string.IsNullOrWhiteSpace(buttonLink))
            throw new FeaturedButtonPairRequiredException();
        return new FeaturedTile(label, title, buttonLabel, buttonLink, imageKey);
    }
}

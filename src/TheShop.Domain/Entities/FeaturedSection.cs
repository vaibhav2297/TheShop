using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Domain.Entities;

/// <summary>The singleton Featured configuration with two fixed, ordered tiles.</summary>
public sealed class FeaturedSection
{
    public short Id => 1;
    public string Title { get; private set; } = string.Empty;
    public FeaturedTile Tile1 { get; private set; } = null!;
    public FeaturedTile Tile2 { get; private set; } = null!;
    public bool IsVisible { get; private set; }

    private FeaturedSection() { }

    /// <summary>Creates a complete configuration, including hidden configurations.</summary>
    public static FeaturedSection Create(string title, FeaturedTile tile1, FeaturedTile tile2, bool isVisible)
    {
        var section = new FeaturedSection();
        section.Update(title, tile1, tile2, isVisible);
        return section;
    }

    /// <summary>Restores a saved configuration while preserving its invariants.</summary>
    public static FeaturedSection Rehydrate(string title, FeaturedTile tile1, FeaturedTile tile2, bool isVisible)
        => Create(title, tile1, tile2, isVisible);

    /// <summary>Replaces the complete configuration only after all invariants pass.</summary>
    /// <exception cref="FeaturedSectionTitleRequiredException">Section title is blank.</exception>
    /// <exception cref="ArgumentNullException">A fixed tile is absent.</exception>
    public void Update(string title, FeaturedTile tile1, FeaturedTile tile2, bool isVisible)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new FeaturedSectionTitleRequiredException();
        ArgumentNullException.ThrowIfNull(tile1);
        ArgumentNullException.ThrowIfNull(tile2);
        (Title, Tile1, Tile2, IsVisible) = (title, tile1, tile2, isVisible);
    }
}

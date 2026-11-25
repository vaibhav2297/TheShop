using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>Restores fixed tile positions without exposing provider records.</summary>
internal static class FeaturedSectionMapper
{
    internal static FeaturedSection ToDomain(this FeaturedSectionRecord record)
        => FeaturedSection.Rehydrate(record.Title,
            FeaturedTile.Create(record.Tile1Label, record.Tile1Title, record.Tile1ButtonLabel,
                record.Tile1ButtonLink, record.Tile1ImageKey),
            FeaturedTile.Create(record.Tile2Label, record.Tile2Title, record.Tile2ButtonLabel,
                record.Tile2ButtonLink, record.Tile2ImageKey), record.IsVisible);
}

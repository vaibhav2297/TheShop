using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

public sealed class FeaturedSectionTests
{
    private static FeaturedTile Tile(string title = "One") => FeaturedTile.Create(null, title, null, null, "featured/image.png");

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void CreateAndRehydrate_PreserveFixedTileOrderAndHiddenSingleton()
    {
        var first = Tile();
        var second = Tile("Two");
        foreach (var section in new[] { FeaturedSection.Create("Featured", first, second, false),
                     FeaturedSection.Rehydrate("Featured", first, second, false) })
        {
            section.Id.Should().Be(1);
            section.Title.Should().Be("Featured");
            section.Tile1.Should().Be(first);
            section.Tile2.Should().Be(second);
            section.IsVisible.Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void Update_InvalidReplacementLeavesEntireSavedConfigurationUnchanged()
    {
        var section = FeaturedSection.Create("Saved", Tile(), Tile("Two"), true);
        Action blank = () => section.Update(" ", Tile("New"), Tile("Other"), false);
        blank.Should().Throw<FeaturedSectionTitleRequiredException>();
        Action absentTile = () => section.Update("Changed", Tile("New"), null!, false);
        absentTile.Should().Throw<ArgumentNullException>();
        section.Title.Should().Be("Saved");
        section.Tile1.Title.Should().Be("One");
        section.Tile2.Title.Should().Be("Two");
        section.IsVisible.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void Update_ValidReplacementChangesBothTilesAndVisibilityTogether()
    {
        var section = FeaturedSection.Create("Saved", Tile(), Tile("Two"), true);
        section.Update("Summer", Tile("Three"), Tile("Four"), false);
        section.Title.Should().Be("Summer");
        section.Tile1.Title.Should().Be("Three");
        section.Tile2.Title.Should().Be("Four");
        section.IsVisible.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void Tile_RequiredTitleAndImageRejectBlankValues()
    {
        foreach (var blank in new[] { "", " \t" })
        {
            Action title = () => FeaturedTile.Create(null, blank, null, null, "key");
            title.Should().Throw<FeaturedTileTitleRequiredException>();
            Action image = () => FeaturedTile.Create(null, "Title", null, null, blank);
            image.Should().Throw<FeaturedImageRequiredException>();
        }
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void Tile_OptionalLabelAndButtonPairAllowBothAbsentAndRejectHalfPair()
    {
        Tile().Label.Should().BeNull();
        Tile().ButtonLink.Should().BeNull();
        var complete = FeaturedTile.Create("New", "Title", "Shop", "/products", "key");
        complete.ButtonLink.Should().Be("/products");
        Action missingLink = () => FeaturedTile.Create(null, "Title", "Shop", null, "key");
        Action missingLabel = () => FeaturedTile.Create(null, "Title", null, "/products", "key");
        missingLink.Should().Throw<FeaturedButtonPairRequiredException>();
        missingLabel.Should().Throw<FeaturedButtonPairRequiredException>();
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public void Tile_ValueEqualityIncludesArtworkAndEveryContentField()
    {
        Tile().Should().Be(Tile());
        Tile().Should().NotBe(Tile("Two"));
        Tile().Should().NotBe(FeaturedTile.Create(null, "One", null, null, "other-key"));
        Tile().Should().NotBe(FeaturedTile.Create("Label", "One", null, null, "featured/image.png"));
    }
}

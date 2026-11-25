using FluentAssertions;
using TheShop.Infrastructure.Persistence.Mappers;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="PlaceholderImage"/> — the placeholder URL builder shared by
/// <see cref="ProductMapper"/> and <see cref="TheShop.Infrastructure.Persistence.Repositories.SupabaseBrandRepository"/>
/// for records with no uploaded image.
/// </summary>
public class PlaceholderImageTests
{
    [Fact]
    [Trait("Feature", "manage-brands")]
    public void For_ReturnsAPlacehold_CoUrl()
    {
        var url = PlaceholderImage.For("Elf Bar");

        url.Should().StartWith("https://placehold.co/");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void For_UrlEncodesTheLabel()
    {
        var url = PlaceholderImage.For("Elf Bar & Co");

        url.Should().Contain(Uri.EscapeDataString("Elf Bar & Co"));
    }
}

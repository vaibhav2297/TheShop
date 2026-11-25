using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

public sealed class HeroBannerTests
{
    private static HeroBanner Banner() => HeroBanner.Create("Heading", null, null, null, "hero/desktop.png", null, true);

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public void Create_ValidContent_PreservesSingletonAndVisibility()
    {
        var banner = HeroBanner.Create("Heading", "Description", "Shop", "/products",
            "hero/desktop.png", "hero/mobile.png", false);
        banner.Id.Should().Be(1);
        banner.Heading.Should().Be("Heading");
        banner.ButtonLink.Should().Be("/products");
        banner.MobileImageKey.Should().Be("hero/mobile.png");
        banner.IsVisible.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public void Create_MissingRequiredContent_RejectsIt()
    {
        var heading = () => HeroBanner.Create(" ", null, null, null, "desktop", null, true);
        var desktop = () => HeroBanner.Create("Heading", null, null, null, " ", null, true);
        heading.Should().Throw<HeroBannerHeadingRequiredException>();
        desktop.Should().Throw<HeroBannerDesktopImageRequiredException>();
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public void ChangeContent_IncompleteButtonPair_PreservesPreviousValues()
    {
        var banner = Banner();
        var action = () => banner.ChangeContent("Changed", null, "Shop", null);
        action.Should().Throw<HeroBannerButtonPairRequiredException>();
        banner.Heading.Should().Be("Heading");
        banner.ButtonLabel.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public void ChangeImagesAndVisibility_RemovingMobile_PreservesDesktopAndContent()
    {
        var banner = HeroBanner.Rehydrate("Heading", null, null, null, "desktop", "mobile", true);
        banner.ChangeImages("desktop", null);
        banner.SetVisibility(false);
        banner.MobileImageKey.Should().BeNull();
        banner.DesktopImageKey.Should().Be("desktop");
        banner.Heading.Should().Be("Heading");
        banner.IsVisible.Should().BeFalse();
    }
}

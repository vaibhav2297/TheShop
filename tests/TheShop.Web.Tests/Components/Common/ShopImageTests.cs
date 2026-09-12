using Bunit;
using FluentAssertions;
using MudBlazor;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopImageTests : TestContext
{
    [Theory]
    [InlineData(ShopImageTreatment.Product, "shop-image-product", ObjectFit.Contain)]
    [InlineData(ShopImageTreatment.Thumbnail, "shop-image-thumbnail", ObjectFit.Contain)]
    [InlineData(ShopImageTreatment.CategoryTile, "shop-image-category-tile", ObjectFit.Cover)]
    [InlineData(ShopImageTreatment.CategoryBanner, "shop-image-category-banner", ObjectFit.Cover)]
    [InlineData(ShopImageTreatment.HeroDesktop, "shop-image-hero-desktop", ObjectFit.Cover)]
    [InlineData(ShopImageTreatment.HeroMobile, "shop-image-hero-mobile", ObjectFit.Cover)]
    [InlineData(ShopImageTreatment.Editorial, "shop-image-editorial", ObjectFit.Cover)]
    [InlineData(ShopImageTreatment.BrandLogo, "shop-image-brand-logo", ObjectFit.Contain)]
    [InlineData(ShopImageTreatment.SocialSharing, "shop-image-social-sharing", ObjectFit.Contain)]
    [Trait("Feature", "reusable-image-treatments")]
    public void Render_WithTreatment_AppliesItsStableClassAndFit(
        ShopImageTreatment treatment,
        string expectedClass,
        ObjectFit expectedFit)
    {
        var cut = Render<ShopImage>(parameters => parameters
            .Add(component => component.Src, "https://example.com/image.webp")
            .Add(component => component.Alt, "Example image")
            .Add(component => component.Treatment, treatment));

        var image = cut.FindComponent<MudImage>();
        image.Instance.ObjectFit.Should().Be(expectedFit);
        image.Instance.ObjectPosition.Should().Be(ObjectPosition.Center);
        cut.Find(".shop-image").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    [Trait("Feature", "reusable-image-treatments")]
    public void Render_WithCustomDisplayWidth_AppliesItToTheImage()
    {
        var cut = Render<ShopImage>(parameters => parameters
            .Add(component => component.Src, "https://example.com/image.webp")
            .Add(component => component.Alt, "Example image")
            .Add(component => component.Treatment, ShopImageTreatment.Editorial)
            .Add(component => component.DisplayWidth, "18rem"));

        cut.Find(".shop-image").GetAttribute("style").Should().Contain("width:18rem");
    }

    [Fact]
    [Trait("Feature", "reusable-image-treatments")]
    public void Render_WithNoDisplayWidthOverride_UsesFullAvailableWidth()
    {
        var cut = Render<ShopImage>(parameters => parameters
            .Add(component => component.Src, "https://example.com/image.webp")
            .Add(component => component.Alt, "Example image")
            .Add(component => component.Treatment, ShopImageTreatment.Product));

        cut.Find(".shop-image").GetAttribute("style").Should().Contain("width:100%");
    }

    [Fact]
    [Trait("Feature", "reusable-image-treatments")]
    public void Render_WithImageAndCallerAttributes_ForwardsAllValuesToTheRoot()
    {
        var cut = Render<ShopImage>(parameters => parameters
            .Add(component => component.Src, "https://example.com/image.webp")
            .Add(component => component.Alt, "Localized alternate text")
            .Add(component => component.FallbackSrc, "images/fallback.svg")
            .Add(component => component.Treatment, ShopImageTreatment.Product)
            .Add(component => component.Class, "consumer-class")
            .Add(component => component.Style, "border:1px solid currentColor")
            .AddUnmatched("data-testid", "treated-image"));

        var root = cut.Find(".shop-image");
        var image = cut.Find("img");
        image.GetAttribute("src").Should().Be("https://example.com/image.webp");
        image.GetAttribute("alt").Should().Be("Localized alternate text");
        root.GetAttribute("data-testid").Should().Be("treated-image");
        root.GetAttribute("class").Should().EndWith("consumer-class");
        root.GetAttribute("style").Should().EndWith("border:1px solid currentColor;");
        cut.FindComponent<MudImage>().Instance.FallbackSrc.Should().Be("images/fallback.svg");
    }
}

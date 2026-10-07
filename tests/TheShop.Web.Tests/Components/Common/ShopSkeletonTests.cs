using Bunit;
using FluentAssertions;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Admin;
using TheShop.Web.Components.Common;
using TheShop.Web.Components.Products;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopSkeletonTests : TestContext
{
    [Fact]
    public void Render_Defaults_IsAHiddenEmptyRectangleThatForwardsRootStyling()
    {
        var cut = Render<ShopSkeleton>(p => p.Add(c => c.Class, "custom").Add(c => c.Style, "--size: 1")
            .AddUnmatched("data-testid", "skeleton"));
        var skeleton = cut.Find("span");
        skeleton.ClassList.Should().Contain(["shop-skeleton", "shop-skeleton-rectangle", "custom"])
            .And.NotContain("shop-native", "the shared native root would reset the inherited text style");
        skeleton.GetAttribute("style").Should().Be("--size: 1");
        skeleton.GetAttribute("data-testid").Should().Be("skeleton");
        skeleton.GetAttribute("aria-hidden").Should().Be("true");
        skeleton.ChildElementCount.Should().Be(0);
        skeleton.TextContent.Should().BeEmpty();
        cut.Markup.Should().NotContain("mud-");
    }

    [Fact]
    public void Render_TextShape_UsesTheTextModifier()
    {
        var cut = Render<ShopSkeleton>(p => p.Add(c => c.Shape, ShopSkeletonShape.Text));
        cut.Find("span").ClassList.Should().Contain("shop-skeleton-text").And.NotContain("shop-skeleton-rectangle");
    }

    [Fact]
    public void Render_CallerAriaHidden_CannotExposeTheSkeleton()
    {
        var cut = Render<ShopSkeleton>(p => p.AddUnmatched("aria-hidden", "false"));
        cut.Find("span").GetAttribute("aria-hidden").Should().Be("true");
    }

    [Fact]
    public void Render_UndeclaredShape_FailsClearly()
    {
        var render = () => Render<ShopSkeleton>(p => p.Add(c => c.Shape, (ShopSkeletonShape)99));
        render.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ProductCardSkeleton_ReusesCardClassesAndStaysHidden()
    {
        var cut = Render<ProductCardSkeleton>(p => p.Add(c => c.Class, "custom"));
        var root = cut.Find("div");
        root.ClassList.Should().Contain(["shop-product-card", "shop-product-card-skeleton", "custom"]);
        root.GetAttribute("aria-hidden").Should().Be("true");
        cut.Find(".shop-skeleton.shop-product-card-media").Should().NotBeNull();
        cut.FindAll(".shop-product-card-content .shop-skeleton-text").Select(e => e.ClassList[2])
            .Should().Equal("shop-product-card-brand", "shop-product-card-title", "shop-product-card-price");
        cut.FindAll("button, a, img").Should().BeEmpty();
    }

    [Fact]
    public void AdminSkeletons_AreHiddenAndUseOwnedClasses()
    {
        var card = Render<AdminModuleCardSkeleton>();
        card.Find("div").GetAttribute("aria-hidden").Should().Be("true");
        card.FindAll(".shop-skeleton").Should().HaveCount(3);

        var form = Render<AdminFormSkeleton>();
        form.Find("div").ClassList.Should().Contain("shop-admin-form-skeleton");
        form.Find("div").GetAttribute("aria-hidden").Should().Be("true");
        form.FindAll(".shop-skeleton").Should().HaveCount(3);
    }
}

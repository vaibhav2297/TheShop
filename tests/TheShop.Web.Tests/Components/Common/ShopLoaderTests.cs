using Bunit;
using FluentAssertions;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopLoaderTests : TestContext
{
    [Fact]
    public void Render_ForwardsRootAttributesAndEnforcesDecorativeSemantics()
    {
        var cut = Render<ShopLoader>(p => p.Add(c => c.Class, "custom")
            .Add(c => c.Style, "--shop-loader-color: currentColor")
            .AddUnmatched("data-testid", "loader").AddUnmatched("aria-hidden", "false"));

        var root = cut.Find("span");
        root.ClassList.Should().Contain(["shop-loader", "custom"]);
        root.GetAttribute("data-testid").Should().Be("loader");
        root.GetAttribute("style").Should().Be("--shop-loader-color: currentColor");
        root.GetAttribute("aria-hidden").Should().Be("true");
        cut.Find("svg").GetAttribute("focusable").Should().Be("false");
        cut.FindAll("[role=status], button, input").Should().BeEmpty();
        cut.Markup.Should().NotContain("mud-");
    }

    [Fact]
    public void LoadingButton_ReusesLoaderAndKeepsOneAnnouncementAndItsName()
    {
        var cut = Render<ShopButton>(p => p.Add(c => c.Loading, true).AddChildContent("Save"));
        cut.FindComponent<ShopLoader>().Should().NotBeNull();
        cut.Find("button").TextContent.Should().Be("Save");
        cut.FindAll("[role=status]").Should().HaveCount(1);
        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
    }
}

using Bunit;
using FluentAssertions;
using TheShop.Web.Components.Common;
using TheShop.Web.Theme;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopIconTests : TestContext
{
    [Fact]
    public void Render_WithATrustedRegistryFragment_RendersSvgContentWithoutEscapingIt()
    {
        var cut = Render<ShopIcon>(p => p.Add(c => c.Icon, ShopIcons.Outlined.Circle));

        var svg = cut.Find("svg.shop-icon");
        svg.GetAttribute("viewBox").Should().Be("0 0 24 24");
        svg.GetAttribute("fill").Should().Be("currentColor");
        svg.QuerySelector("path")!.GetAttribute("stroke").Should().Be("currentColor");
        svg.TextContent.Should().BeNullOrWhiteSpace();
        cut.FindAll("svg").Should().ContainSingle();
    }

    [Fact]
    public void Render_Always_HidesTheDecorativeIconFromAssistiveTechnology()
    {
        var cut = Render<ShopIcon>(p => p.Add(c => c.Icon, ShopIcons.Outlined.Circle));

        var svg = cut.Find("svg");
        svg.GetAttribute("aria-hidden").Should().Be("true");
        svg.GetAttribute("focusable").Should().Be("false");
        svg.HasAttribute("tabindex").Should().BeFalse();
    }

    [Fact]
    public void Render_WithConflictingAttributes_PreservesDecorativeSemantics()
    {
        var cut = Render<ShopIcon>(p => p
            .Add(c => c.Icon, ShopIcons.Outlined.Circle)
            .AddUnmatched("aria-hidden", "false")
            .AddUnmatched("focusable", "true")
            .AddUnmatched("tabindex", "0")
            .AddUnmatched("viewBox", "0 0 100 100"));

        var svg = cut.Find("svg");
        svg.GetAttribute("aria-hidden").Should().Be("true");
        svg.GetAttribute("focusable").Should().Be("false");
        svg.HasAttribute("tabindex").Should().BeFalse();
        svg.GetAttribute("viewBox").Should().Be("0 0 24 24");
    }

    [Fact]
    public void Render_WithConsumerAttributes_ForwardsThemToTheSvg()
    {
        var cut = Render<ShopIcon>(p => p
            .Add(c => c.Icon, ShopIcons.Outlined.Circle)
            .Add(c => c.Class, "navigation-icon")
            .Add(c => c.Style, "--shop-icon-test-size: 2rem;")
            .AddUnmatched("id", "navigation-icon")
            .AddUnmatched("data-testid", "navigation-icon"));

        var svg = cut.Find("svg");
        svg.ClassList.Should().Contain("shop-icon").And.Contain("navigation-icon");
        svg.GetAttribute("style").Should().Be("--shop-icon-test-size: 2rem;");
        svg.Id.Should().Be("navigation-icon");
        svg.GetAttribute("data-testid").Should().Be("navigation-icon");
    }
}

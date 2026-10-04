using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopExpanderTests : TestContext
{
    [Fact]
    public void Toggle_PreservesContentIdentityAndIndependentState()
    {
        var cut = Render<ShopExpander>(p => p.Add(x => x.Title, "Bag")
            .Add(x => x.ChildContent, "<input value='retained' />"));
        var other = Render<ShopExpander>(p => p.Add(x => x.Title, "Other"));
        var trigger = cut.Find("button");
        var bodyId = trigger.GetAttribute("aria-controls");
        trigger.GetAttribute("type").Should().Be("button");
        cut.Find("#" + bodyId).HasAttribute("inert").Should().BeTrue();
        cut.Find("#" + bodyId).GetAttribute("aria-hidden").Should().Be("true");
        trigger.Click();
        trigger.GetAttribute("aria-expanded").Should().Be("true");
        cut.Find("#" + bodyId).HasAttribute("inert").Should().BeFalse();
        cut.Find("#" + bodyId).HasAttribute("aria-hidden").Should().BeFalse();
        cut.Find(".shop-expander").ClassList.Should().Contain("shop-expander-expanded");
        cut.Render(p => p.Add(x => x.Title, "Updated bag"));
        cut.Find("button").GetAttribute("aria-expanded").Should().Be("true");
        cut.Find("input").GetAttribute("value").Should().Be("retained");
        cut.Find("button").Click();
        cut.Find("#" + bodyId).HasAttribute("inert").Should().BeTrue();
        cut.Find("#" + bodyId).HasAttribute("hidden").Should().BeFalse();
        cut.Find(".shop-expander").ClassList.Should().NotContain("shop-expander-expanded");
        other.Find("button").GetAttribute("aria-expanded").Should().Be("false");
        other.Find("button").Id.Should().NotBe(trigger.Id);
    }

    [Fact]
    public void ControlledExpanded_DisabledAndExternalUpdates_AreRespected()
    {
        var values = new List<bool>();
        var cut = Render<ShopExpander>(p => p.Add(x => x.Title, "Bag")
            .Add(x => x.Expanded, true).Add(x => x.ExpandedChanged, value => values.Add(value)));
        cut.Find("button").Click();
        values.Should().Equal(false);
        cut.Render(p => p.Add(x => x.Expanded, false).Add(x => x.Disabled, true));
        cut.Find("button").Click();
        values.Should().Equal(false);
        cut.Render(p => p.Add(x => x.Expanded, true));
        cut.Find(".shop-expander-content").HasAttribute("inert").Should().BeFalse();
    }

    [Fact]
    public void SlotsAndRootAttributes_HaveStableOwners()
    {
        var cut = Render<ShopExpander>(p => p.Add(x => x.TitleContent, "Custom title")
            .Add(x => x.OverviewContent, "2 Items").Add(x => x.Class, "custom")
            .AddUnmatched("data-probe", "value"));
        cut.Find(".shop-expander-title").TextContent.Should().Be("Custom title");
        cut.Find(".shop-expander-overview").TextContent.Should().Be("2 Items");
        cut.Find(".shop-expander").ClassList.Should().Contain("custom");
        cut.Find(".shop-expander").GetAttribute("data-probe").Should().Be("value");
        cut.FindAll("button button").Should().BeEmpty();
    }

    [Fact]
    public void MissingHeading_FailsClearly()
    {
        var act = () => Render<ShopExpander>();
        act.Should().Throw<ArgumentException>().WithParameterName("Title");
    }
}

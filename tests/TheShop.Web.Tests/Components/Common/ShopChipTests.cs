using Bunit;
using FluentAssertions;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using TheShop.Web.Theme;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopChipTests : TestContext
{
    [Theory]
    [InlineData(ShopSize.Small, "small")]
    [InlineData(ShopSize.Medium, "medium")]
    [InlineData(ShopSize.Large, "large")]
    public void ReadOnlyChip_RendersSizedNonInteractiveLabel(ShopSize size, string suffix)
    {
        var cut = Render<ShopChip>(p => p.Add(x => x.ChildContent, "Chip")
            .Add(x => x.Size, size).Add(x => x.Selected, true)
            .Add(x => x.StartIcon, ShopIcons.Outlined.Arrow_Left_MD)
            .Add(x => x.EndIcon, ShopIcons.Outlined.Arrow_Right_MD)
            .Add(x => x.Class, "custom").AddUnmatched("data-probe", "chip"));
        var chip = cut.Find("span.shop-chip");
        chip.ClassList.Should().Contain("shop-chip-" + suffix).And.Contain("shop-chip-selected").And.Contain("custom");
        chip.GetAttribute("data-probe").Should().Be("chip");
        chip.HasAttribute("aria-pressed").Should().BeFalse();
        cut.FindAll("button").Should().BeEmpty();
        cut.FindAll("svg").Should().HaveCount(2).And.OnlyContain(x => x.GetAttribute("aria-hidden") == "true");
    }

    [Fact]
    public void Toggle_CallerOwnsSelection_AndDisabledCannotBeOverridden()
    {
        var values = new List<bool>();
        var cut = Render<ShopChip>(p => p.Add(x => x.ChildContent, "Chip")
            .Add(x => x.SelectedChanged, value => values.Add(value))
            .AddUnmatched("type", "submit").AddUnmatched("aria-pressed", "wrong")
            .AddUnmatched("disabled", false));
        cut.Find("button").GetAttribute("type").Should().Be("button");
        cut.Find("button").GetAttribute("aria-pressed").Should().Be("false");
        cut.Find("button").Click();
        values.Should().Equal(true);
        cut.Find("button").GetAttribute("aria-pressed").Should().Be("false");
        cut.Render(p => p.Add(x => x.Selected, true));
        cut.Find("button").Click();
        values.Should().Equal(true, false);
        cut.Render(p => p.Add(x => x.Disabled, true));
        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
        cut.Find("button").Click();
        values.Should().Equal(true, false);
    }

    [Fact]
    public void UndefinedSize_FailsClearly()
    {
        var act = () => Render<ShopChip>(p => p.Add(x => x.ChildContent, "Chip").Add(x => x.Size, (ShopSize)999));
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("Size");
    }
}

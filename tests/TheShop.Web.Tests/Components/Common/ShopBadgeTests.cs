using Bunit;
using FluentAssertions;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopBadgeTests : TestContext
{
    [Fact]
    public void Render_Defaults_AreNonInteractiveAndForwardRootAttributes()
    {
        var cut = Render<ShopBadge>(p => p.AddChildContent("Primary")
            .Add(c => c.Class, "custom").Add(c => c.Style, "order: 2")
            .AddUnmatched("data-testid", "badge"));
        var badge = cut.Find("span");
        badge.ClassList.Should().Contain(["shop-badge", "shop-badge-filled", "shop-badge-primary", "shop-badge-medium", "custom"]);
        badge.GetAttribute("style").Should().Be("order: 2");
        badge.GetAttribute("data-testid").Should().Be("badge");
        badge.HasAttribute("tabindex").Should().BeFalse();
        badge.HasAttribute("role").Should().BeFalse();
        cut.FindAll("button, a").Should().BeEmpty();
    }

    [Fact]
    public void Render_AllFigmaVariants_UseSharedEnums()
    {
        foreach (var color in new[] { ShopColor.Primary, ShopColor.Secondary, ShopColor.Info, ShopColor.Success, ShopColor.Warning, ShopColor.Error })
            foreach (var variant in new[] { ShopVariant.Filled, ShopVariant.Outlined })
                foreach (var size in Enum.GetValues<ShopSize>())
                {
                    var cut = Render<ShopBadge>(p => p.AddChildContent("Badge")
                        .Add(c => c.Color, color).Add(c => c.Variant, variant).Add(c => c.Size, size));
                    cut.Find("span").ClassList.Should().Contain([
                        ShopCssClass.Modifier("shop-badge", color),
                ShopCssClass.Modifier("shop-badge", variant),
                ShopCssClass.Modifier("shop-badge", size)]);
                }
    }

    [Theory]
    [InlineData(ShopColor.Tertiary)]
    [InlineData(ShopColor.Surface)]
    public void Render_UnsupportedColor_RejectsUndesignedTreatment(ShopColor color)
    {
        var render = () => Render<ShopBadge>(p => p.Add(c => c.Color, color));
        render.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Render_TextVariant_RejectsUndesignedTreatment()
    {
        var render = () => Render<ShopBadge>(p => p.Add(c => c.Variant, ShopVariant.Text));
        render.Should().Throw<ArgumentOutOfRangeException>();
    }
}

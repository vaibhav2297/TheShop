using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.Theme;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopIconButtonTests : TestContext
{
    [Fact]
    public void Render_Defaults_ComposesOneNamedNativeButtonAndDecorativeIcon()
    {
        var cut = Render<ShopIconButton>(p => p
            .Add(c => c.Icon, ShopIcons.Outlined.Heart_01)
            .Add(c => c.Label, Strings.Wishlist_Add));

        cut.FindAll("button").Should().HaveCount(1);
        cut.Find("button").ParentElement!.TagName.Should().NotBe("SPAN", "composition must not introduce an outer layout wrapper");
        var button = cut.Find("button");
        button.GetAttribute("type").Should().Be("button");
        button.GetAttribute("aria-label").Should().Be(Strings.Wishlist_Add);
        button.ClassList.Should().BeEquivalentTo("shop-button", "shop-icon-button",
            "shop-button-primary", "shop-button-filled", "shop-button-medium");
        button.HasAttribute("disabled").Should().BeFalse();
        button.TextContent.Should().BeEmpty();
        cut.Find("button > .shop-button-content > svg").GetAttribute("aria-hidden").Should().Be("true");
        cut.Find("button > .shop-button-content > svg").GetAttribute("focusable").Should().Be("false");
    }

    [Theory]
    [InlineData(ShopVariant.Filled)]
    [InlineData(ShopVariant.Outlined)]
    [InlineData(ShopVariant.Text)]
    public void Render_EachVariant_ForwardsAllColorsToTheSharedButton(ShopVariant variant)
    {
        foreach (var color in Enum.GetValues<ShopColor>())
        {
            var cut = Render<ShopIconButton>(p => p
                .Add(c => c.Icon, ShopIcons.Outlined.Close_MD)
                .Add(c => c.Label, Strings.Close)
                .Add(c => c.Color, color)
                .Add(c => c.Variant, variant));

            cut.Find("button").ClassList.Should().Contain(ShopCssClass.Modifier("shop-button", color))
                .And.Contain(ShopCssClass.Modifier("shop-button", variant));
        }
    }

    [Theory]
    [InlineData(ShopSize.Small)]
    [InlineData(ShopSize.Medium)]
    [InlineData(ShopSize.Large)]
    public void Render_EachSize_ForwardsToEveryVariant(ShopSize size)
    {
        foreach (var variant in Enum.GetValues<ShopVariant>())
        {
            var cut = Render<ShopIconButton>(p => p
                .Add(c => c.Icon, ShopIcons.Outlined.Close_MD)
                .Add(c => c.Label, Strings.Close)
                .Add(c => c.Size, size)
                .Add(c => c.Variant, variant));

            cut.Find("button").ClassList.Should().Contain(ShopCssClass.Modifier("shop-button", size));
            cut.FindComponent<ShopButton>().Instance.Size.Should().Be(size);
        }
    }

    [Fact]
    public void Render_UndefinedSize_RejectsAnUnsupportedTreatment()
    {
        Action render = () => Render<ShopIconButton>(p => p
            .Add(c => c.Icon, ShopIcons.Outlined.Close_MD)
            .Add(c => c.Label, Strings.Close)
            .Add(c => c.Size, (ShopSize)999));
        render.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be(nameof(ShopIconButton.Size));
    }

    [Fact]
    public async Task Click_DisabledAndConflictingAttributes_PreservesTheSharedActivationGuard()
    {
        var calls = 0;
        var attributeCalls = 0;
        var cut = Render<ShopIconButton>(p => p
            .Add(c => c.Icon, ShopIcons.Outlined.Close_MD)
            .Add(c => c.Label, Strings.Close)
            .Add(c => c.Disabled, true)
            .Add(c => c.OnClick, () => calls++)
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["disabled"] = false,
                ["type"] = "submit",
                ["onclick"] = EventCallback.Factory.Create<MouseEventArgs>(this, () => attributeCalls++)
            }));

        var button = cut.Find("button");
        button.GetAttribute("type").Should().Be("button");
        button.HasAttribute("disabled").Should().BeTrue();
        await button.TriggerEventAsync("onclick", new MouseEventArgs());
        calls.Should().Be(0);
        attributeCalls.Should().Be(0);

        cut.Render(p => p.Add(c => c.Disabled, false));
        await cut.Find("button").ClickAsync(new());
        calls.Should().Be(1);
        attributeCalls.Should().Be(0);
    }

    [Fact]
    public void Render_ConsumerAttributes_ForwardsToTheButtonAndProtectsItsAccessibleName()
    {
        var attributes = new Dictionary<string, object>
        {
            ["id"] = "cart-action",
            ["ARIA-LABEL"] = "Wrong name",
            ["aria-labelledby"] = "missing-label",
            ["aria-describedby"] = "cart-hint",
            ["data-testid"] = "cart"
        };
        var cut = Render<ShopIconButton>(p => p
            .Add(c => c.Icon, ShopIcons.Outlined.Shopping_Bag_01)
            .Add(c => c.Label, Strings.AddToCart)
            .Add(c => c.Type, "submit")
            .Add(c => c.Class, "shop-cart-action")
            .Add(c => c.Style, "--shop-test-offset: 1px;")
            .Add(c => c.AdditionalAttributes, attributes));

        var button = cut.Find("button");
        button.Id.Should().Be("cart-action");
        button.ClassList.Should().Contain("shop-cart-action");
        button.GetAttribute("style").Should().Be("--shop-test-offset: 1px;");
        button.GetAttribute("type").Should().Be("submit");
        button.GetAttribute("aria-label").Should().Be(Strings.AddToCart);
        button.HasAttribute("aria-labelledby").Should().BeFalse();
        button.GetAttribute("aria-describedby").Should().Be("cart-hint");
        button.GetAttribute("data-testid").Should().Be("cart");
        attributes["ARIA-LABEL"].Should().Be("Wrong name", "the caller's dictionary must not be mutated");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Render_MissingLabel_RejectsAnUnnamedAction(string? label)
    {
        Action render = () => Render<ShopIconButton>(p => p
            .Add(c => c.Icon, ShopIcons.Outlined.Close_MD)
            .Add(c => c.Label, label!));
        render.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(nameof(ShopIconButton.Label));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Render_MissingIcon_RejectsAnEmptyAction(string? icon)
    {
        Action render = () => Render<ShopIconButton>(p => p
            .Add(c => c.Icon, icon!)
            .Add(c => c.Label, Strings.Close));
        render.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(nameof(ShopIconButton.Icon));
    }

    [Fact]
    public void Render_ChangedParameters_UpdatesNameIconAndTreatment()
    {
        var cut = Render<ShopIconButton>(p => p
            .Add(c => c.Icon, ShopIcons.Outlined.Heart_01)
            .Add(c => c.Label, Strings.Wishlist_Add));
        cut.Render(p => p
            .Add(c => c.Icon, ShopIcons.Outlined.Close_MD)
            .Add(c => c.Label, Strings.Close)
            .Add(c => c.Color, ShopColor.Surface)
            .Add(c => c.Size, ShopSize.Large)
            .Add(c => c.Variant, ShopVariant.Text));

        cut.Find("button").GetAttribute("aria-label").Should().Be(Strings.Close);
        cut.Find("button").ClassList.Should().Contain("shop-button-surface").And.Contain("shop-button-text")
            .And.Contain("shop-button-large").And.NotContain("shop-button-medium")
            .And.NotContain("shop-button-primary").And.NotContain("shop-button-filled");
        cut.FindComponent<ShopIcon>().Instance.Icon.Should().Be(ShopIcons.Outlined.Close_MD);
    }

    [Theory]
    [InlineData(ShopSize.Small)]
    [InlineData(ShopSize.Medium)]
    [InlineData(ShopSize.Large)]
    public async Task Loading_EachSize_ForwardsSharedBehaviorAndRetainsAccessibleName(ShopSize size)
    {
        var calls = 0;
        var cut = Render<ShopIconButton>(p => p
            .Add(c => c.Icon, ShopIcons.Outlined.Close_MD)
            .Add(c => c.Label, Strings.Close)
            .Add(c => c.Size, size)
            .Add(c => c.Loading, true)
            .Add(c => c.OnClick, () => calls++)
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["aria-label"] = "Wrong name",
                ["aria-busy"] = "false",
                ["disabled"] = false
            }));

        cut.FindComponent<ShopButton>().Instance.Loading.Should().BeTrue();
        var button = cut.Find("button");
        button.GetAttribute("aria-label").Should().Be(Strings.Close);
        button.GetAttribute("aria-busy").Should().Be("true");
        button.HasAttribute("disabled").Should().BeTrue();
        button.ClassList.Should().Contain(ShopCssClass.Modifier("shop-button", size));
        cut.FindAll(".shop-loader").Should().HaveCount(1);
        cut.Find("[role=status]").TextContent.Should().Be(Strings.Loading);
        await button.TriggerEventAsync("onclick", new MouseEventArgs());
        calls.Should().Be(0);

        cut.Render(p => p.Add(c => c.Loading, false));
        cut.Find("button").GetAttribute("aria-label").Should().Be(Strings.Close);
        cut.FindAll(".shop-loader").Should().BeEmpty();
        cut.Find("button").Click();
        calls.Should().Be(1);
    }
}

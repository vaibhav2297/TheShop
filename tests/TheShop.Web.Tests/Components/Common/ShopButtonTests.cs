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

public class ShopButtonTests : TestContext
{
    [Fact]
    public void Render_WithDefaults_RendersAnEnabledNativeActionButton()
    {
        var cut = Render<ShopButton>(p => p.AddChildContent("Add to cart"));

        var button = cut.Find("button");
        button.GetAttribute("type").Should().Be("button");
        button.HasAttribute("disabled").Should().BeFalse();
        button.TextContent.Trim().Should().Be("Add to cart");
        button.ClassList.Should().Contain("shop-button")
            .And.Contain("shop-button-primary").And.Contain("shop-button-filled").And.Contain("shop-button-medium");
        cut.FindAll("svg").Should().BeEmpty();
    }

    [Theory]
    [InlineData("submit", "submit")]
    [InlineData("reset", "reset")]
    [InlineData("SUBMIT", "submit")]
    [InlineData("button", "button")]
    [InlineData("invalid", "button")]
    [InlineData("", "button")]
    public void Render_WithAnExplicitType_UsesOnlySupportedNativeTypes(string type, string expected)
    {
        var cut = Render<ShopButton>(p => p.Add(c => c.Type, type));

        cut.Find("button").GetAttribute("type").Should().Be(expected);
    }

    [Theory]
    [InlineData(ShopVariant.Filled, "shop-button-filled")]
    [InlineData(ShopVariant.Outlined, "shop-button-outlined")]
    [InlineData(ShopVariant.Text, "shop-button-text")]
    public void Render_WithAVariant_PreservesTheBaseClassAndAddsItsTreatment(ShopVariant variant, string expected)
    {
        var cut = Render<ShopButton>(p => p.Add(c => c.Variant, variant));

        cut.Find("button").ClassList.Should().Contain("shop-button").And.Contain(expected);
    }

    [Theory]
    [InlineData(ShopSize.Small, "shop-button-small")]
    [InlineData(ShopSize.Medium, "shop-button-medium")]
    [InlineData(ShopSize.Large, "shop-button-large")]
    public void Render_WithASize_AddsItsSizeClass(ShopSize size, string expected)
    {
        var cut = Render<ShopButton>(p => p.Add(c => c.Size, size));

        cut.Find("button").ClassList.Should().Contain(expected);
    }

    [Fact]
    public void Click_WhenEnabled_InvokesTheCallbackOnceWithItsEvent()
    {
        var calls = 0;
        MouseEventArgs? received = null;
        var cut = Render<ShopButton>(p => p.Add(c => c.OnClick, (MouseEventArgs args) =>
        {
            calls++;
            received = args;
        }));

        cut.Find("button").Click(new MouseEventArgs { CtrlKey = true });

        calls.Should().Be(1);
        received.Should().NotBeNull();
        received!.CtrlKey.Should().BeTrue();
    }

    [Theory]
    [InlineData(ShopColor.Primary, "shop-button-primary")]
    [InlineData(ShopColor.Secondary, "shop-button-secondary")]
    [InlineData(ShopColor.Tertiary, "shop-button-tertiary")]
    [InlineData(ShopColor.Info, "shop-button-info")]
    [InlineData(ShopColor.Success, "shop-button-success")]
    [InlineData(ShopColor.Warning, "shop-button-warning")]
    [InlineData(ShopColor.Error, "shop-button-error")]
    [InlineData(ShopColor.Surface, "shop-button-surface")]
    public void Render_EachColor_ComposesIndependentlyWithEveryVariantAndSize(ShopColor color, string expected)
    {
        foreach (var variant in Enum.GetValues<ShopVariant>())
            foreach (var size in Enum.GetValues<ShopSize>())
            {
                var calls = 0;
                var cut = Render<ShopButton>(p => p
                    .Add(c => c.Color, color)
                    .Add(c => c.Variant, variant)
                    .Add(c => c.Size, size)
                    .Add(c => c.OnClick, () => calls++)
                    .AddChildContent("Continue"));

                var button = cut.Find("button");
                button.ClassList.Should().BeEquivalentTo("shop-button", expected,
                    $"shop-button-{variant.ToString().ToLowerInvariant()}",
                    $"shop-button-{size.ToString().ToLowerInvariant()}");
                button.GetAttribute("type").Should().Be("button");
                button.Click();
                calls.Should().Be(1);
            }
    }

    [Theory]
    [InlineData(nameof(ShopButton.Color))]
    [InlineData(nameof(ShopButton.Variant))]
    [InlineData(nameof(ShopButton.Size))]
    public void Render_UndefinedVisualOption_RejectsInsteadOfEmittingAnUnsupportedClass(string parameter)
    {
        Action render = () =>
        {
            _ = parameter switch
            {
                nameof(ShopButton.Color) => Render<ShopButton>(p => p.Add(c => c.Color, (ShopColor)999)),
                nameof(ShopButton.Variant) => Render<ShopButton>(p => p.Add(c => c.Variant, (ShopVariant)999)),
                _ => Render<ShopButton>(p => p.Add(c => c.Size, (ShopSize)999))
            };
        };

        render.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be(parameter);
    }

    [Fact]
    public void Render_ChangingVisualOptions_ReplacesClassesWithoutLosingDisabledOrConsumerAttributes()
    {
        var cut = Render<ShopButton>(p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.Class, "shop-checkout-action")
            .AddUnmatched("aria-label", "Continue"));

        cut.Render(p => p
            .Add(c => c.Color, ShopColor.Error)
            .Add(c => c.Variant, ShopVariant.Outlined)
            .Add(c => c.Size, ShopSize.Small));

        var button = cut.Find("button");
        button.ClassList.Should().BeEquivalentTo("shop-button", "shop-button-error",
            "shop-button-outlined", "shop-button-small", "shop-checkout-action");
        button.HasAttribute("disabled").Should().BeTrue();
        button.GetAttribute("aria-label").Should().Be("Continue");
    }

    [Fact]
    public async Task Click_WhenDisabled_DoesNotDispatchEvenASyntheticEvent()
    {
        var calls = 0;
        var cut = Render<ShopButton>(p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.OnClick, () => calls++));

        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
        await cut.Find("button").TriggerEventAsync("onclick", new MouseEventArgs());

        calls.Should().Be(0);
    }

    [Fact]
    public async Task Render_WithConflictingAttributes_PreservesExplicitButtonBehavior()
    {
        var calls = 0;
        var attributeCalls = 0;
        IReadOnlyDictionary<string, object> attributes = new Dictionary<string, object>
        {
            ["disabled"] = false,
            ["type"] = "submit",
            ["onclick"] = EventCallback.Factory.Create<MouseEventArgs>(this, () => attributeCalls++)
        };
        var cut = Render<ShopButton>(p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.Type, "button")
            .Add(c => c.OnClick, () => calls++)
            .Add(c => c.AdditionalAttributes, attributes));

        var button = cut.Find("button");
        button.HasAttribute("disabled").Should().BeTrue();
        button.GetAttribute("type").Should().Be("button");
        await button.TriggerEventAsync("onclick", new MouseEventArgs());
        calls.Should().Be(0);
        attributeCalls.Should().Be(0);

        cut.Render(p => p.Add(c => c.Disabled, false));
        cut.Find("button").Click();
        calls.Should().Be(1);
        attributeCalls.Should().Be(0);
    }

    [Fact]
    public void Render_WithConsumerAttributes_ForwardsThemToTheNativeButton()
    {
        var cut = Render<ShopButton>(p => p
            .Add(c => c.Class, "checkout-action")
            .Add(c => c.Style, "--shop-button-test-size: 2rem;")
            .AddUnmatched("id", "checkout")
            .AddUnmatched("aria-label", "Proceed to checkout")
            .AddUnmatched("data-testid", "checkout-action"));

        var button = cut.Find("button");
        button.ClassList.Should().Contain("shop-button").And.Contain("checkout-action");
        button.GetAttribute("style").Should().Be("--shop-button-test-size: 2rem;");
        button.Id.Should().Be("checkout");
        button.GetAttribute("aria-label").Should().Be("Proceed to checkout");
        button.GetAttribute("data-testid").Should().Be("checkout-action");
    }

    [Fact]
    public void Render_WithStartAndEndIcons_KeepsThemDecorativeAndPreservesTheLabel()
    {
        var cut = Render<ShopButton>(p => p
            .Add(c => c.StartIcon, ShopIcons.Outlined.Circle)
            .Add(c => c.EndIcon, ShopIcons.Outlined.Square)
            .AddChildContent("Continue"));

        var icons = cut.FindAll("button > .shop-button-content > svg.shop-button-icon");
        icons.Should().HaveCount(2);
        foreach (var icon in icons)
        {
            icon.GetAttribute("aria-hidden").Should().Be("true");
            icon.GetAttribute("focusable").Should().Be("false");
            icon.QuerySelector("path").Should().NotBeNull();
        }
        cut.Find("button").TextContent.Trim().Should().Be("Continue");
    }

    [Fact]
    public async Task Loading_Toggles_PreservesContentAndGuardsActivationAndAttributes()
    {
        var calls = 0;
        var cut = Render<ShopButton>(p => p
            .Add(c => c.StartIcon, ShopIcons.Outlined.Circle)
            .Add(c => c.EndIcon, ShopIcons.Outlined.Square)
            .Add(c => c.OnClick, () => calls++)
            .Add(c => c.AdditionalAttributes, new Dictionary<string, object>
            {
                ["disabled"] = false,
                ["aria-busy"] = "false"
            })
            .AddChildContent(Strings.Save));
        var content = cut.Find(".shop-button-content");
        var contentMarkup = content.InnerHtml;
        var status = cut.Find("[role=status]");
        status.TextContent.Should().BeEmpty();
        cut.FindAll(".shop-loader").Should().BeEmpty();

        cut.Render(p => p.Add(c => c.Loading, true));

        var button = cut.Find("button");
        button.ClassList.Should().Contain("shop-button-loading");
        button.HasAttribute("disabled").Should().BeTrue();
        button.GetAttribute("aria-busy").Should().Be("true");
        button.TextContent.Trim().Should().Be(Strings.Save);
        cut.Find(".shop-button-content").InnerHtml.Should().Be(contentMarkup);
        content.HasAttribute("aria-hidden").Should().BeFalse();
        content.QuerySelectorAll("svg").Should().HaveCount(2);
        cut.Find(".shop-button-spinner").GetAttribute("aria-hidden").Should().Be("true");
        status.TextContent.Should().Be(Strings.Loading);
        status.ClassList.Should().Contain("shop-visually-hidden");
        button.QuerySelector("[role=status]").Should().BeNull("status must not change the button name or be gated by its aria-busy");
        await button.TriggerEventAsync("onclick", new MouseEventArgs());
        calls.Should().Be(0);

        cut.Render(p => p.Add(c => c.Loading, false));
        cut.Find("button").HasAttribute("disabled").Should().BeFalse();
        cut.Find("button").GetAttribute("aria-busy").Should().Be("false");
        cut.Find("button").ClassList.Should().NotContain("shop-button-loading");
        cut.FindAll(".shop-loader").Should().BeEmpty();
        status.TextContent.Should().BeEmpty();
        cut.Find("button").Click();
        calls.Should().Be(1);
    }

    [Fact]
    public void Loading_Finishes_PreservesExplicitDisabledState()
    {
        var cut = Render<ShopButton>(p => p.Add(c => c.Loading, true).Add(c => c.Disabled, true));
        cut.Render(p => p.Add(c => c.Loading, false));
        cut.Find("button").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll(".shop-loader").Should().BeEmpty();
    }
}

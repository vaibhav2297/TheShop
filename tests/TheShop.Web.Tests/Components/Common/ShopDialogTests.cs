using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopDialogTests : TestContext
{
    public ShopDialogTests() => JSInterop.SetupModule("./js/shopDialog.js").Mode = JSRuntimeMode.Loose;

    private static RenderFragment Title => builder =>
    {
        builder.OpenElement(0, "h2");
        builder.AddAttribute(1, "class", "shop-dialog-title");
        builder.AddContent(2, "Example title");
        builder.CloseElement();
    };

    [Theory]
    [InlineData(ShopMaxWidth.ExtraSmall, "extra-small")]
    [InlineData(ShopMaxWidth.Small, "small")]
    [InlineData(ShopMaxWidth.Medium, "medium")]
    [InlineData(ShopMaxWidth.Large, "large")]
    [InlineData(ShopMaxWidth.ExtraLarge, "extra-large")]
    [InlineData(ShopMaxWidth.ExtraExtraLarge, "extra-extra-large")]
    [InlineData(ShopMaxWidth.None, "none")]
    public void MaxWidth_UsesSharedModifierAndCanReturnToDefault(ShopMaxWidth width, string suffix)
    {
        var cut = Render<ShopDialog>(p => p.Add(x => x.TitleContent, Title).Add(x => x.MaxWidth, width));
        cut.Find("dialog").ClassList.Should().Contain("shop-dialog-width-" + suffix);
        cut.Render(p => p.Add(x => x.MaxWidth, (ShopMaxWidth?)null));
        cut.Find("dialog").ClassName.Should().Be("shop-native shop-dialog shop-dialog-width-none");
    }

    [Fact]
    public void MaxWidth_Omitted_UsesNone()
    {
        var cut = Render<ShopDialog>(p => p.Add(x => x.TitleContent, Title));
        cut.Instance.MaxWidth.Should().Be(ShopMaxWidth.None);
        cut.Find("dialog").ClassList.Should().Contain("shop-dialog-width-none");
    }

    [Fact]
    public void MaxWidth_UndefinedValueFailsClearly()
    {
        var act = () => Render<ShopDialog>(p => p.Add(x => x.TitleContent, Title)
            .Add(x => x.MaxWidth, (ShopMaxWidth)999));
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("MaxWidth");
    }

    [Fact]
    public void Slots_HaveSeparateOwnersAndStableAccessibleNaming()
    {
        var cut = Render<ShopDialog>(p => p.Add(x => x.TitleContent, Title)
            .Add(x => x.DialogContent, "Body content")
            .Add(x => x.DialogActions, "Action content")
            .Add(x => x.DescriptionId, "example-description"));
        var titleId = cut.Find("dialog").GetAttribute("aria-labelledby");
        cut.Find("#" + titleId).TextContent.Should().Be("Example title");
        cut.FindAll("h2").Should().ContainSingle();
        cut.Find(".shop-dialog-content").TextContent.Should().Be("Body content");
        cut.Find(".shop-dialog-content").GetAttribute("tabindex").Should().Be("0");
        cut.Find(".shop-dialog-content").GetAttribute("aria-labelledby").Should().Be(titleId);
        cut.Find("footer").TextContent.Should().Be("Action content");
        cut.Find("dialog").GetAttribute("aria-describedby").Should().Be("example-description");
        cut.Find("dialog").HasAttribute("open").Should().BeFalse();
        cut.Render();
        cut.Find("dialog").GetAttribute("aria-labelledby").Should().Be(titleId);
        var other = Render<ShopDialog>(p => p.Add(x => x.TitleContent, Title));
        other.Find("dialog").GetAttribute("aria-labelledby").Should().NotBe(titleId);
    }
}

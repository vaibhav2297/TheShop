using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopDrawerTests : TestContext
{
    public ShopDrawerTests() => JSInterop.SetupModule("./js/shopDialog.js").Mode = JSRuntimeMode.Loose;

    private static RenderFragment Header => b => b.AddMarkupContent(0, "<h2>Account</h2>");

    [Fact]
    public void Slots_RenderSeparateRegionsAndOptionalFooter_WithStableUniqueNames()
    {
        var cut = Render<ShopDrawer>(p => p.Add(x => x.HeaderContent, Header)
            .Add(x => x.DrawerContent, "Body").Add(x => x.ActionContent, "Actions"));
        var id = cut.Find("dialog").GetAttribute("aria-labelledby");
        cut.Find("#" + id).TextContent.Should().Be("Account");
        cut.Find(".shop-drawer-content").TextContent.Should().Be("Body");
        cut.Find(".shop-drawer-content").GetAttribute("tabindex").Should().Be("0");
        cut.Find("footer").TextContent.Should().Be("Actions");
        cut.Find("dialog").HasAttribute("open").Should().BeFalse();
        cut.Render();
        cut.Find("dialog").GetAttribute("aria-labelledby").Should().Be(id);
        var other = Render<ShopDrawer>(p => p.Add(x => x.HeaderContent, Header));
        other.FindAll("footer").Should().BeEmpty();
        other.Find("dialog").GetAttribute("aria-labelledby").Should().NotBe(id);
    }

    [Fact]
    public void Attributes_ForwardToRoot_ButCannotBypassControlledOpenOrNaming()
    {
        var cut = Render<ShopDrawer>(p => p.Add(x => x.HeaderContent, Header)
            .Add(x => x.Class, "custom").Add(x => x.Style, "order: 1")
            .AddUnmatched("data-testid", "drawer").AddUnmatched("open", true)
            .AddUnmatched("aria-labelledby", "wrong"));
        var dialog = cut.Find("dialog");
        dialog.ClassList.Should().Contain("custom");
        dialog.GetAttribute("style").Should().Be("order: 1");
        dialog.GetAttribute("data-testid").Should().Be("drawer");
        dialog.HasAttribute("open").Should().BeFalse();
        dialog.GetAttribute("aria-labelledby").Should().NotBe("wrong");
    }

    [Fact]
    public async Task Open_ChangesSynchronizeBrowser_AndDismissalRequestsParentUpdate()
    {
        var requests = new List<bool>();
        var cut = Render<ShopDrawer>(p => p.Add(x => x.HeaderContent, Header)
            .Add(x => x.OpenChanged, value => requests.Add(value)));
        JSInterop.Invocations.Should().BeEmpty();
        cut.Render(p => p.Add(x => x.Open, true));
        JSInterop.Invocations["setOpen"].Last().Arguments.Last().Should().Be(true);
        await cut.Find("[data-testid=drawer-close]").ClickAsync(new());
        requests.Should().Equal(false);
        cut.Instance.Open.Should().BeTrue();
        cut.Render(p => p.Add(x => x.Open, false));
        JSInterop.Invocations["setOpen"].Last().Arguments.Last().Should().Be(false);
        await cut.InvokeAsync(cut.Instance.DismissAsync);
        requests.Should().ContainSingle();
        await cut.Instance.DisposeAsync();
        JSInterop.Invocations["dispose"].Should().ContainSingle();
        await cut.Instance.DisposeAsync();
        JSInterop.Invocations["dispose"].Should().ContainSingle();
    }
}

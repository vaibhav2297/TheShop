using Bunit;
using FluentAssertions;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopAppBarTests : TestContext
{
    public ShopAppBarTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/shopAppBar.js").Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Menu_ReportsOpenStateAndRequestsLayoutActions_AndDisposesObserver()
    {
        this.AddAuthorization().SetNotAuthorized();
        var opens = 0;
        var closes = 0;
        var cut = Render<ShopAppBar>(p => p.Add(x => x.MenuOpen, true)
            .Add(x => x.MenuDrawerId, "navigation")
            .Add(x => x.OnMenuClick, () => opens++)
            .Add(x => x.OnMenuUnavailable, () => closes++));
        var menu = cut.Find($"button[aria-label='{Strings.Nav_Menu}']");
        menu.GetAttribute("aria-controls").Should().Be("navigation");
        menu.GetAttribute("aria-expanded").Should().Be("true");
        menu.Click();
        opens.Should().Be(1);
        await cut.InvokeAsync(cut.Instance.MenuUnavailableAsync);
        closes.Should().Be(1);
        cut.Instance.MenuOpen.Should().BeTrue("open state belongs to the layout");
        await cut.Instance.DisposeAsync();
        await cut.Instance.DisposeAsync();
        await cut.InvokeAsync(cut.Instance.MenuUnavailableAsync);
        closes.Should().Be(1);
        JSInterop.Invocations["dispose"].Should().ContainSingle();
    }

    [Fact]
    public void Render_Anonymous_ShowsNativeNavigationAndNamedActionsWithoutMudServices()
    {
        this.AddAuthorization().SetNotAuthorized();
        var cut = Render<ShopAppBar>(p => p.Add(c => c.Class, "custom")
            .Add(c => c.Style, "order: 1").AddUnmatched("data-testid", "appbar"));

        cut.Find("header").ClassList.Should().Contain("custom");
        cut.Find("header").GetAttribute("style").Should().Be("order: 1");
        cut.Find("header").GetAttribute("data-testid").Should().Be("appbar");
        cut.Find("nav").GetAttribute("aria-label").Should().Be(Strings.Nav_Primary);
        cut.FindAll("nav a").Select(a => a.GetAttribute("href")).Should()
            .Equal(Routes.Home, Routes.Products, Routes.Categories, Routes.Brands, Routes.Deals);
        cut.Find($"a[aria-label='{Strings.Nav_Cart}']").GetAttribute("href").Should().Be(Routes.Cart);
        cut.Find($"a[aria-label='{Strings.Nav_Account}']").GetAttribute("href").Should().Be(Routes.Auth.SignIn);
        cut.Find($"button[aria-label='{Strings.Nav_Search}']").GetAttribute("type").Should().Be("button");
        cut.Find(".shop-appbar-logo img").GetAttribute("alt").Should().Be(Strings.AppName);
        cut.Markup.Should().NotContain("mud-");
    }

    [Fact]
    public void Render_Authenticated_RaisesAccountRequestInsteadOfOwningDrawer()
    {
        this.AddAuthorization().SetAuthorized("customer");
        var requests = 0;
        var cut = Render<ShopAppBar>(p => p.Add(x => x.OnAccountClick, () => requests++)
            .Add(x => x.AccountOpen, true));

        cut.FindAll($"a[href='{Routes.Auth.SignIn}']").Should().BeEmpty();
        var account = cut.Find($"button[aria-label='{Strings.Nav_Account}']");
        account.GetAttribute("aria-haspopup").Should().Be("dialog");
        account.GetAttribute("aria-expanded").Should().Be("true");
        account.Click();
        requests.Should().Be(1);
        cut.FindAll("dialog").Should().BeEmpty();
    }
}

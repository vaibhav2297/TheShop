using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class ResponsiveNavigationJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(320, false)]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(767, false)]
    [InlineData(768, false)]
    [InlineData(834, false)]
    [InlineData(834, true)]
    [InlineData(1023, false)]
    [InlineData(1024, false)]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    public async Task Shell_ResponsiveNavigationPreservesFocusDismissalAndFooter(int width, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", route => route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Body = "[]",
            Headers = new Dictionary<string, string> { ["Content-Range"] = "0-0/0" }
        }));
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync(WebRoutes.Products);
        var appbar = Page.Locator(".shop-appbar");
        await appbar.WaitForAsync();
        await Page.EvaluateAsync("""
            async withoutVendorCss => {
                await document.fonts.load('500 14px "Space Grotesk"');
                await document.fonts.load('700 24px "Barlow Condensed"');
                await document.fonts.ready;
                if (withoutVendorCss) {
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) throw new Error('Unexpected legacy stylesheet');

                }
            }
            """, withoutVendorCss);
        var menu = appbar.GetByRole(AriaRole.Button, new() { Name = Strings.Nav_Menu, Exact = true });
        var drawer = Page.Locator("dialog.shop-navigation-drawer");
        (await appbar.BoundingBoxAsync())!.Height.Should().BeApproximately(64, 1);
        await Assertions.Expect(Page.Locator(".shop-footer")).ToHaveCountAsync(1);
        var columns = await Page.Locator(".shop-footer-sections").EvaluateAsync<int>("el => getComputedStyle(el).gridTemplateColumns.split(' ').length");
        columns.Should().Be(width < 768 ? 1 : width < 1024 ? 2 : 4);
        await Assertions.Expect(Page.Locator(".shop-footer a[href^='mailto:']")).ToHaveCountAsync(1);
        await Assertions.Expect(drawer).ToBeHiddenAsync();
        if (width >= 1024)
        {
            await Assertions.Expect(menu).ToBeHiddenAsync();
            await Assertions.Expect(appbar.Locator("nav")).ToBeVisibleAsync();
        }
        else
        {
            await Assertions.Expect(appbar.Locator("nav")).ToBeHiddenAsync();
            await menu.FocusAsync();
            await Page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(drawer).ToBeVisibleAsync();
            await Assertions.Expect(menu).ToHaveAttributeAsync("aria-expanded", "true");
            await Assertions.Expect(drawer).ToHaveCSSAsync("transform", "matrix(1, 0, 0, 1, 0, 0)");
            var box = (await drawer.BoundingBoxAsync())!;
            box.X.Should().BeApproximately(0, 1);
            box.Width.Should().BeApproximately(width < 768 ? width : 400, 1);
            box.Height.Should().BeApproximately(900, 1);
            await Assertions.Expect(drawer.Locator("footer")).ToHaveCountAsync(0);
            await Assertions.Expect(drawer.Locator("nav a")).ToHaveCountAsync(5);
            await Assertions.Expect(drawer.GetByRole(AriaRole.Button, new() { Name = Strings.Close, Exact = true })).ToBeFocusedAsync();
            await Page.Keyboard.PressAsync("Shift+Tab");
            await Assertions.Expect(drawer.Locator("nav a").Last).ToBeFocusedAsync();
            await Page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(drawer).ToBeHiddenAsync();
            await Assertions.Expect(menu).ToBeFocusedAsync();
            await menu.ClickAsync();
            await Assertions.Expect(drawer).ToBeVisibleAsync();
            await drawer.GetByRole(AriaRole.Link, new() { Name = Strings.Nav_Products, Exact = true }).ClickAsync();
            await Assertions.Expect(drawer).ToBeHiddenAsync();
            await menu.ClickAsync();
            await Assertions.Expect(drawer).ToBeVisibleAsync();
            if (width >= 768)
            {
                await Page.Mouse.ClickAsync(width - 10, 300);
                await Assertions.Expect(drawer).ToBeHiddenAsync();
                await menu.ClickAsync();
                await Assertions.Expect(drawer).ToBeVisibleAsync();
            }
            await Page.SetViewportSizeAsync(1024, 900);
            await Assertions.Expect(drawer).ToBeHiddenAsync();
            await Assertions.Expect(Page.Locator("dialog:modal")).ToHaveCountAsync(0);
            (await Page.EvaluateAsync<bool>("() => !!document.activeElement?.getClientRects().length")).Should().BeTrue();
            await Page.SetViewportSizeAsync(width, 900);
            await Assertions.Expect(menu).ToHaveAttributeAsync("aria-expanded", "false");
            await menu.ClickAsync();
            await Assertions.Expect(drawer).ToBeVisibleAsync();
            await drawer.GetByRole(AriaRole.Link, new() { Name = Strings.Nav_Home, Exact = true }).ClickAsync();
            await Assertions.Expect(Page).ToHaveURLAsync(E2EEnvironment.AppBaseUrl + WebRoutes.Home);
            await Assertions.Expect(Page.Locator(".shop-home-page h1")).ToHaveTextAsync(Strings.Stay_Tuned);
            await Assertions.Expect(drawer).ToBeHiddenAsync();
        }
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '32px'");
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
        errors.Should().BeEmpty();
    }
}

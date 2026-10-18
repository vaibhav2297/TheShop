using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeShellJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, true)]
    [InlineData(390, false)]
    [InlineData(1440, true)]
    [InlineData(1440, false)]
    public async Task ShellAndBadges_FigmaGeometryAndNavigation_WorkWithoutVendorCss(int width, bool withoutVendorCss)
    {
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", route =>
            route.Request.Method == "GET" || new Uri(route.Request.Url).AbsolutePath.Contains("/rpc/")
                ? route.FulfillAsync(new()
                {
                    ContentType = "application/json",
                    Body = "[]",
                    Headers = new Dictionary<string, string> { ["Content-Range"] = "0-0/0", ["Access-Control-Expose-Headers"] = "Content-Range" }
                })
                : route.AbortAsync());
        await Page.SetViewportSizeAsync(width, 1000);
        await Page.GotoAsync(WebRoutes.Products);
        var appbar = Page.Locator(".shop-appbar");
        var trail = Page.Locator(".shop-breadcrumbs");
        await appbar.WaitForAsync(new() { Timeout = 30_000 });
        await trail.WaitForAsync();
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        var badges = await RenderBadgesAsync();
        await Page.EvaluateAsync("""
            async args => {
                document.body.style.margin = '0';

                document.querySelector('[data-shop-scroll-root]').style.display = 'none';
                if (args.withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) throw new Error('Unexpected legacy stylesheet');
                const specimen = document.createElement('main');
                specimen.id = 'badge-probe';
                specimen.style.cssText = 'display:flex;flex-wrap:wrap;align-items:center;gap:16px;padding:24px;background:white';
                specimen.innerHTML = args.badges;
                document.body.appendChild(specimen);
                await document.fonts.load('400 12px "Space Grotesk"');
                await document.fonts.load('400 14px "Space Grotesk"');
                await document.fonts.load('400 16px "Space Grotesk"');
                await document.fonts.load('500 14px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, new { badges, withoutVendorCss });

        await Assertions.Expect(appbar).ToHaveCSSAsync("background-color", "rgb(255, 255, 255)");
        await Assertions.Expect(appbar.Locator("nav a")).ToHaveCountAsync(5);
        await Assertions.Expect(appbar.Locator("nav a").First).ToHaveCSSAsync("text-transform", "capitalize");
        await Assertions.Expect(appbar.Locator(".shop-appbar-logo")).ToHaveCSSAsync("width", "52px");
        (await appbar.Locator(".shop-appbar-logo img").BoundingBoxAsync())!.Height.Should().BeApproximately(52, 1);
        foreach (var action in await appbar.Locator(".shop-appbar-actions > button, .shop-appbar-actions > a").AllAsync())
            (await action.BoundingBoxAsync())!.Width.Should().BeApproximately(34, 1);
        if (width == 1440)
            (await appbar.BoundingBoxAsync())!.Height.Should().BeApproximately(64, 1);

        await Assertions.Expect(trail).ToHaveCSSAsync("background-color", "rgb(245, 245, 245)");
        (await trail.BoundingBoxAsync())!.Height.Should().BeApproximately(42, 1);
        await Assertions.Expect(trail).ToHaveCSSAsync("padding-left", width == 1440 ? "36px" : "16px");
        await Assertions.Expect(trail.Locator("[aria-current='page']")).ToHaveTextAsync(Strings.Nav_Products);
        await Assertions.Expect(trail.Locator(".shop-icon")).ToHaveCSSAsync("width", "24px");
        var keyboardTarget = width < 1024 ? appbar.GetByRole(AriaRole.Button, new() { Name = Strings.Nav_Menu }) : appbar.Locator("nav a").First;
        await keyboardTarget.FocusAsync();
        await Assertions.Expect(keyboardTarget).ToHaveCSSAsync("outline-style", "solid");

        foreach (var (size, height, font, padding) in new[] { ("small", 24, "12px", "8px"), ("medium", 32, "14px", "12px"), ("large", 40, "16px", "16px") })
        {
            var badge = Page.Locator($"#badge-probe .shop-badge-{size}").First;
            (await badge.BoundingBoxAsync())!.Height.Should().BeApproximately(height, 1);
            await Assertions.Expect(badge).ToHaveCSSAsync("font-size", font);
            (await badge.EvaluateAsync<string>("el => getComputedStyle(el).fontFamily")).Should().Contain("Space Grotesk");
            await Assertions.Expect(badge).ToHaveCSSAsync("padding-left", padding);
            await Assertions.Expect(badge).ToHaveCSSAsync("border-radius", "0px");
        }
        await Assertions.Expect(Page.Locator("#badge-probe .shop-badge-primary.shop-badge-filled").First)
            .ToHaveCSSAsync("color", "rgb(255, 255, 255)");
        await Assertions.Expect(Page.Locator("#badge-probe .shop-badge-primary.shop-badge-outlined").First)
            .ToHaveCSSAsync("box-shadow", "rgb(224, 224, 224) 0px 0px 0px 1px inset");
        await Assertions.Expect(Page.Locator("#badge-probe .shop-badge-error.shop-badge-outlined").First)
            .ToHaveCSSAsync("color", "rgb(255, 66, 66)");
        await AssertNoOverflowAsync();
        await SaveAsync($"shell-{width}-{withoutVendorCss}");

        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
        await Assertions.Expect(Page.Locator(".shop-badge").First).ToHaveCSSAsync("border-top-style", "solid");
        await SaveAsync($"shell-forced-{width}-{withoutVendorCss}");
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.None });
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '32px'");
        await AssertNoOverflowAsync();
        await SaveAsync($"shell-zoom-{width}-{withoutVendorCss}");

        await trail.GetByRole(AriaRole.Link, new() { Name = Strings.Nav_Home, Exact = true }).ClickAsync();
        await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath == WebRoutes.Home);
        await Assertions.Expect(Page.Locator(".shop-breadcrumbs")).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData(390, true)]
    [InlineData(390, false)]
    [InlineData(1440, true)]
    [InlineData(1440, false)]
    public async Task Shell_FallbackPagesAndAuthorizing_PreserveNativeNavigationAndScroll(int width, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", route => route.AbortAsync());
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync(WebRoutes.NotFound);
        var main = Page.Locator("main[data-shop-scroll-root]");
        await main.WaitForAsync();
        await Page.EvaluateAsync("""
            async withoutVendorCss => {
                if (withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam/i.test(link.href)) throw new Error('Unexpected legacy stylesheet');
                await document.fonts.load('700 34px "Barlow Condensed"');
                await document.fonts.load('500 16px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, withoutVendorCss);
        await Assertions.Expect(Page.Locator("main")).ToHaveCountAsync(1);
        await Assertions.Expect(Page.Locator(".shop-shell [class*='mud-']")).ToHaveCountAsync(0);
        await Assertions.Expect(main.Locator("h1")).ToHaveTextAsync(Strings.NotFound_Heading);
        await Assertions.Expect(main.Locator("h1")).ToHaveCSSAsync("font-size", "34px");
        await Assertions.Expect(Page.Locator(".shop-not-found-page")).ToHaveCSSAsync("gap", "16px");
        var home = main.GetByRole(AriaRole.Link, new() { Name = Strings.NotFound_GoHome, Exact = true });
        await Assertions.Expect(home).ToHaveAttributeAsync("href", WebRoutes.Home);
        await home.FocusAsync();
        await Assertions.Expect(home).ToHaveCSSAsync("outline-style", "solid");
        await SaveAsync($"native-not-found-{width}-{withoutVendorCss}");
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath == WebRoutes.Home);
        await Assertions.Expect(main.Locator("h1")).ToHaveTextAsync(Strings.Stay_Tuned);
        await Assertions.Expect(main.Locator("h1")).ToBeFocusedAsync();
        await Assertions.Expect(main.Locator("h1")).ToHaveCSSAsync("font-size", "34px");
        await SaveAsync($"native-home-{width}-{withoutVendorCss}");
        await Page.GoBackAsync();
        await Assertions.Expect(main.Locator("h1")).ToHaveTextAsync(Strings.NotFound_Heading);
        await Page.GotoAsync(WebRoutes.NotFound + "/missing");
        await Assertions.Expect(main.Locator("h1")).ToHaveTextAsync(Strings.NotFound_Heading);
        await Page.EvaluateAsync("""
            async withoutVendorCss => {
                if (withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam/i.test(link.href)) throw new Error('Unexpected legacy stylesheet');
                await document.fonts.load('500 16px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, withoutVendorCss);

        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var authorizing = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<AuthorizingView>()).ToHtmlString());
        await Page.EvaluateAsync("html => document.querySelector('.shop-shell-page').insertAdjacentHTML('afterbegin', html)", authorizing);
        var status = main.Locator(".shop-authorizing-view");
        await Assertions.Expect(status).ToHaveAttributeAsync("role", "status");
        await Assertions.Expect(status).ToHaveCSSAsync("padding-top", "80px");
        await Assertions.Expect(status).ToHaveCSSAsync("gap", "16px");
        await Assertions.Expect(status.Locator("p")).ToHaveTextAsync(Strings.Authorizing_Loading);
        await Assertions.Expect(status.Locator("p")).ToHaveCSSAsync("font-weight", "500");
        await Assertions.Expect(status.Locator(".shop-loader")).ToHaveCSSAsync("width", "48px");
        await status.ScreenshotAsync(new() { Path = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence", $"native-authorizing-{width}-{withoutVendorCss}.png") });
        var appbarY = (await Page.Locator(".shop-appbar").BoundingBoxAsync())!.Y;
        await main.EvaluateAsync("el => { const tall = document.createElement('div'); tall.style.height = '1500px'; el.append(tall); el.scrollTop = el.scrollHeight; }");
        (await main.EvaluateAsync<double>("el => el.scrollTop")).Should().BeGreaterThan(0);
        (await Page.Locator(".shop-appbar").BoundingBoxAsync())!.Y.Should().BeApproximately(appbarY, 1);
        (await Page.EvaluateAsync<double>("() => document.scrollingElement.scrollTop")).Should().Be(0);
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '32px'");
        await AssertNoOverflowAsync();
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        errors.Should().BeEmpty();
    }

    private async Task AssertNoOverflowAsync() =>
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();

    private static async Task<string> RenderBadgesAsync()
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var html = new StringBuilder();
            foreach (var color in new[] { ShopColor.Primary, ShopColor.Secondary, ShopColor.Info, ShopColor.Success, ShopColor.Warning, ShopColor.Error })
                foreach (var variant in new[] { ShopVariant.Filled, ShopVariant.Outlined })
                    foreach (var size in Enum.GetValues<ShopSize>())
                    {
                        var badge = await renderer.RenderComponentAsync<ShopBadge>(ParameterView.FromDictionary(new Dictionary<string, object?>
                        {
                            [nameof(ShopBadge.Color)] = color,
                            [nameof(ShopBadge.Variant)] = variant,
                            [nameof(ShopBadge.Size)] = size,
                            [nameof(ShopBadge.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "Badge"))
                        }));
                        html.Append(badge.ToHtmlString());
                    }
            return html.ToString();
        });
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), FullPage = true });
    }
}

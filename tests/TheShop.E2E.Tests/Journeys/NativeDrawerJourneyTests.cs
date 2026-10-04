using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeDrawerJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, false)]
    [InlineData(390, true)]
    [InlineData(1440, true)]
    public async Task Drawer_ResponsiveModalChrome_ScrollsOnlyBodyAndCleansUp(int width, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", r => r.AbortAsync());
        await Page.SetViewportSizeAsync(width, 844);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton<IJSRuntime, StaticJs>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ShopDrawer>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ShopDrawer.HeaderContent)] = (RenderFragment)(b => b.AddMarkupContent(0, "<h2 class='shop-drawer-title'>Account</h2>")),
                [nameof(ShopDrawer.DrawerContent)] = (RenderFragment)(b => b.AddMarkupContent(0,
                    string.Concat(Enumerable.Repeat("<p>Drawer content remains independently scrollable.</p>", 70)))),
                [nameof(ShopDrawer.ActionContent)] = (RenderFragment)(b => b.AddMarkupContent(0,
                    "<button type='button' id='drawer-action' class='shop-button shop-button-filled shop-button-primary shop-button-medium'>Save</button>"))
            }))).ToHtmlString());

        // Production component markup + production interop. bUnit covers the controlled Blazor binding.
        await Page.EvaluateAsync("""
            async args => {
                if (args.withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                document.body.style.margin = '0';
                const host = document.createElement('main');
                host.id = 'drawer-probe';
                host.innerHTML = '<button id="drawer-trigger">Open drawer</button>' + args.html;
                document.body.appendChild(host);
                const element = host.querySelector('dialog');
                const module = await import('/js/shopDialog.js');
                const receiver = { invokeMethodAsync: async () => module.setOpen(element, receiver, false) };
                const open = () => { document.getElementById('drawer-trigger').focus(); module.setOpen(element, receiver, true); };
                document.getElementById('drawer-trigger').onclick = open;
                element.querySelector('[data-testid=drawer-close]').onclick = () => receiver.invokeMethodAsync();
                window.drawerProbe = { element, module, receiver, open };
                await document.fonts.load('700 24px "Barlow Condensed"');
                await document.fonts.load('400 16px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, new { html, withoutVendorCss });
        var drawer = Page.Locator(".shop-drawer");
        var trigger = Page.Locator("#drawer-trigger");
        var close = Page.GetByTestId("drawer-close");
        await Assertions.Expect(drawer).ToBeHiddenAsync();
        await trigger.ClickAsync();
        await Assertions.Expect(drawer).ToHaveCSSAsync("transform", "matrix(1, 0, 0, 1, 0, 0)");
        await Assertions.Expect(close).ToBeFocusedAsync();
        await Assertions.Expect(drawer).ToHaveAccessibleNameAsync("Account");
        (await drawer.EvaluateAsync<bool>("el => el.matches(':modal')")).Should().BeTrue();
        var rect = (await drawer.BoundingBoxAsync())!;
        rect.Width.Should().BeApproximately(width < 600 ? width : 400, 1);
        rect.Height.Should().BeApproximately(844, 1);
        rect.Y.Should().BeApproximately(0, 1);
        rect.X.Should().BeApproximately(width < 600 ? 0 : width - 400, 1);
        await Assertions.Expect(drawer.Locator("header")).ToHaveCSSAsync("padding-top", "16px");
        await Assertions.Expect(drawer.Locator("footer")).ToHaveCSSAsync("padding-top", "16px");
        (await drawer.EvaluateAsync<string>("el => getComputedStyle(el, '::backdrop').opacity")).Should().Be("1");
        await Assertions.Expect(Page.Locator("body")).ToHaveCSSAsync("overflow", "hidden");

        var header = (await drawer.Locator("header").BoundingBoxAsync())!;
        var footer = (await drawer.Locator("footer").BoundingBoxAsync())!;
        await drawer.Locator(".shop-drawer-content").EvaluateAsync("el => el.scrollTop = el.scrollHeight");
        (await drawer.Locator(".shop-drawer-content").EvaluateAsync<double>("el => el.scrollTop")).Should().BeGreaterThan(0);
        (await drawer.Locator("header").BoundingBoxAsync())!.Y.Should().Be(header.Y);
        (await drawer.Locator("footer").BoundingBoxAsync())!.Y.Should().Be(footer.Y);
        await close.FocusAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Assertions.Expect(Page.Locator("#drawer-action")).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(close).ToBeFocusedAsync();
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"drawer-{width}-{withoutVendorCss}.png") });

        await Page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(drawer).ToBeHiddenAsync();
        await Assertions.Expect(trigger).ToBeFocusedAsync();
        await Assertions.Expect(Page.Locator("body")).Not.ToHaveCSSAsync("overflow", "hidden");
        await trigger.ClickAsync();
        await Assertions.Expect(drawer).ToHaveCSSAsync("transform", "matrix(1, 0, 0, 1, 0, 0)");
        if (width >= 600) await Page.Mouse.ClickAsync(10, 100);
        else await close.ClickAsync();
        await Assertions.Expect(drawer).ToBeHiddenAsync();

        // Reopen while the close animation is still pending: stale completion must not close it.
        await Page.EvaluateAsync("""
            () => { const p = window.drawerProbe; p.open(); p.module.setOpen(p.element, p.receiver, false);
                p.module.setOpen(p.element, p.receiver, true); }
            """);
        await Assertions.Expect(drawer).ToHaveCSSAsync("transform", "matrix(1, 0, 0, 1, 0, 0)");
        await Assertions.Expect(drawer).ToBeVisibleAsync();

        // Opening an existing ShopDialog replaces the drawer rather than leaving a hidden modal active.
        await Page.EvaluateAsync("""
            () => { const p = window.drawerProbe; const next = document.createElement('dialog');
                next.id = 'next-modal'; next.innerHTML = '<button>Close</button>'; document.body.append(next);
                p.module.show(next, { invokeMethodAsync: async () => p.module.dispose(next) }); }
            """);
        await Assertions.Expect(drawer).ToBeHiddenAsync();
        await Assertions.Expect(Page.Locator("dialog:modal")).ToHaveCountAsync(1);
        await Page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Page.Locator("dialog:modal")).ToHaveCountAsync(0);

        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await trigger.ClickAsync();
        await Assertions.Expect(drawer).ToHaveCSSAsync("transition-duration", "0s");
        await Page.EvaluateAsync("document.documentElement.style.fontSize = '200%'");
        (await drawer.EvaluateAsync<double>("el => el.scrollWidth - el.clientWidth")).Should().BeLessThanOrEqualTo(1);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"drawer-large-text-{width}-{withoutVendorCss}.png") });
        await Page.EvaluateAsync("window.drawerProbe.element.remove()");
        await Assertions.Expect(Page.Locator("dialog:modal")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator("body")).Not.ToHaveCSSAsync("overflow", "hidden");
        errors.Should().BeEmpty();
    }

    private sealed class StaticJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new InvalidOperationException();
    }
}

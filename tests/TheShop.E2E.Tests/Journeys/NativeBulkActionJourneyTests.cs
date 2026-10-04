using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.Theme;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeBulkActionJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    public async Task BulkBar_DocksSmoothlyWithoutMovingContentOrLosingFocus(int width, bool withoutVendorCss)
    {
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", route => route.AbortAsync());
        await Page.SetViewportSizeAsync(width, 900);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        var html = await RenderBarAsync();
        await Page.EvaluateAsync("""
            async args => {
                if (args.withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('div');
                host.id = 'bulk-probe';
                host.innerHTML = '<header style="height:100px;padding:24px;background:white">Appbar / Breadcrumbs</header>' +
                    '<main data-shop-scroll-root style="position:fixed;top:100px;left:0;right:0;bottom:0;overflow:auto;background:white">' +
                    '<div id="bulk-container" style="width:calc(100% - 32px);max-width:900px;margin:200px 16px 0 auto">' + args.html +
                    '<div id="bulk-table" style="height:1400px;background:#fafafa;margin-top:24px">Table content</div></div></main>';
                document.body.append(host);
                await document.fonts.load('700 24px "Barlow Condensed"');
                await document.fonts.load('500 14px "Space Grotesk"');
                await document.fonts.ready;
                const module = await import('/js/shopBulkActionBar.js');
                module.sync(host.querySelector('.shop-bulk-action-slot').id);
                await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
            }
            """, new { html, withoutVendorCss });
        var slot = Page.Locator("#bulk-probe .shop-bulk-action-slot");
        var bar = slot.Locator("section");
        var root = Page.Locator("#bulk-probe main");
        var close = bar.GetByRole(AriaRole.Button, new() { Name = Strings.Close_BulkActionBar, Exact = true });
        await Assertions.Expect(bar).ToHaveCSSAsync("background-color", "rgb(245, 245, 245)");
        await Assertions.Expect(bar).ToHaveCSSAsync("padding-top", "16px");
        await Assertions.Expect(bar.Locator(".shop-bulk-action-count")).ToHaveTextAsync("02");
        if (width == 1440) (await bar.BoundingBoxAsync())!.Height.Should().BeApproximately(72, 1);
        var inlineWidth = (await bar.BoundingBoxAsync())!.Width;
        var slotHeight = (await slot.BoundingBoxAsync())!.Height;
        await close.FocusAsync();
        await SaveAsync($"bulk-inline-{width}-{withoutVendorCss}");

        var probe = await Page.EvaluateAsync<double[]>("""
            async () => {
                const slot = document.querySelector('#bulk-probe .shop-bulk-action-slot');
                const bar = slot.firstElementChild;
                const root = slot.closest('main');
                const table = document.getElementById('bulk-table');
                const before = table.getBoundingClientRect().top + root.scrollTop;
                window.bulkButton = bar.querySelector('button');
                window.bulkThreshold = slot.getBoundingClientRect().top - root.getBoundingClientRect().top + root.scrollTop;
                root.scrollTop = window.bulkThreshold + 8;
                await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
                const animation = bar.getAnimations()[0];
                if (animation) { animation.pause(); animation.currentTime = 80; }
                const mid = bar.getBoundingClientRect().width;
                animation?.finish();
                return [before, table.getBoundingClientRect().top + root.scrollTop, mid, animation ? 1 : 0];
            }
            """);
        probe[1].Should().BeApproximately(probe[0], 1, "docking must reserve the in-page footprint");
        probe[3].Should().Be(1, "normal motion must animate, not snap");
        probe[2].Should().BeGreaterThan(inlineWidth);
        probe[2].Should().BeLessThan(width);
        await Assertions.Expect(slot).ToHaveAttributeAsync("data-docked", "");
        await Assertions.Expect(close).ToBeFocusedAsync();
        (await slot.BoundingBoxAsync())!.Height.Should().BeApproximately(slotHeight, 1);
        (await bar.BoundingBoxAsync())!.Y.Should().BeApproximately(100, 1);
        (await bar.BoundingBoxAsync())!.Width.Should().BeApproximately(await root.EvaluateAsync<int>("e => e.clientWidth"), 1);
        (await Page.EvaluateAsync<bool>("() => window.bulkButton === document.querySelector('#bulk-probe section button')")).Should().BeTrue();
        await SaveAsync($"bulk-docked-{width}-{withoutVendorCss}");

        var undock = await Page.EvaluateAsync<double[]>("""
            async () => {
                const root = document.querySelector('#bulk-probe main');
                const bar = root.querySelector('section');
                root.scrollTop = window.bulkThreshold - 3;
                await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
                const animation = bar.getAnimations()[0];
                if (animation) { animation.pause(); animation.currentTime = 80; }
                const width = bar.getBoundingClientRect().width;
                animation?.finish();
                return [width, animation ? 1 : 0];
            }
            """);
        undock[1].Should().Be(1);
        undock[0].Should().BeGreaterThan(inlineWidth);
        undock[0].Should().BeLessThan(width);
        await Assertions.Expect(slot).Not.ToHaveAttributeAsync("data-docked", "");
        await Assertions.Expect(close).ToBeFocusedAsync();

        await Page.EvaluateAsync("""
            async () => {
                const root = document.querySelector('#bulk-probe main');
                for (const offset of [8, -3, 8, -3, 8]) {
                    root.scrollTop = window.bulkThreshold + offset;
                    await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
                }
                await Promise.all(root.querySelector('section').getAnimations().map(a => a.finished.catch(() => {})));
            }
            """);
        await Assertions.Expect(slot).ToHaveAttributeAsync("data-docked", "");
        await Assertions.Expect(close).ToBeFocusedAsync();
        await root.EvaluateAsync("e => e.scrollTop = window.bulkThreshold - 3");
        await Assertions.Expect(slot).Not.ToHaveAttributeAsync("data-docked", "");
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await root.EvaluateAsync("e => e.scrollTop += 20");
        await Assertions.Expect(slot).ToHaveAttributeAsync("data-docked", "");
        (await bar.EvaluateAsync<int>("e => e.getAnimations().length")).Should().Be(0);
        await Page.SetViewportSizeAsync(width == 1440 ? 1000 : 430, 800);
        await Assertions.Expect(bar).ToHaveCSSAsync("position", "fixed");
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '32px'");
        await Page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
        (await bar.EvaluateAsync<bool>("e => e.scrollWidth <= e.clientWidth + 1")).Should().BeTrue();
        (await bar.BoundingBoxAsync())!.Width.Should().BeApproximately(await root.EvaluateAsync<int>("e => e.clientWidth"), 1);
        await SaveAsync($"bulk-zoom-{width}-{withoutVendorCss}");
        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
        await Assertions.Expect(bar).ToHaveCSSAsync("border-top-style", "solid");
        await SaveAsync($"bulk-forced-{width}-{withoutVendorCss}");

        await root.EvaluateAsync("e => e.scrollTop = 0");
        await Assertions.Expect(slot).Not.ToHaveAttributeAsync("data-docked", "");
        await Page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
        (await slot.BoundingBoxAsync())!.Height.Should().BeApproximately((await bar.BoundingBoxAsync())!.Height, 1,
            "the reserved inline height must adapt to enlarged text even while docked");

        await slot.EvaluateAsync("e => e.remove()");
        await Page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(r))");
        (await root.EvaluateAsync<string>("e => e.style.getPropertyValue('--shop-bulk-docked-height')")).Should().BeEmpty();
        await root.EvaluateAsync("e => e.scrollTop = 0");
        await Assertions.Expect(Page.Locator("#bulk-probe section")).ToHaveCountAsync(0);
    }

    private static async Task<string> RenderBarAsync()
    {
        await using var services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime, StaticJsRuntime>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<ShopBulkActionBar>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ShopBulkActionBar.Visible)] = true,
                [nameof(ShopBulkActionBar.SelectedCount)] = 2,
                [nameof(ShopBulkActionBar.Actions)] = (RenderFragment)(b =>
                {
                    foreach (var (label, icon, color) in new[] {
                        (Strings.ManageBrands_BulkSetActive, ShopIcons.Outlined.Show, ShopColor.Primary),
                        (Strings.ManageBrands_BulkSetInactive, ShopIcons.Outlined.Hide, ShopColor.Primary),
                        (Strings.ManageBrands_BulkDelete, ShopIcons.Outlined.Trash_Empty, ShopColor.Error) })
                    {
                        b.OpenComponent<ShopButton>(0);
                        b.AddAttribute(1, nameof(ShopButton.Variant), ShopVariant.Outlined);
                        b.AddAttribute(2, nameof(ShopButton.Color), color);
                        b.AddAttribute(3, nameof(ShopButton.StartIcon), icon);
                        b.AddAttribute(4, nameof(ShopButton.ChildContent), (RenderFragment)(c => c.AddContent(0, label)));
                        b.CloseComponent();
                    }
                })
            }));
            return rendered.ToHtmlString();
        });
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), FullPage = true });
    }

    private sealed class StaticJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}

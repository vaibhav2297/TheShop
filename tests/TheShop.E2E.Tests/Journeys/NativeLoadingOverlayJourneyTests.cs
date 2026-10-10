using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeLoadingOverlayJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, false)]
    [InlineData(390, true)]
    [InlineData(1440, true)]
    public async Task Overlay_BlocksPointerAndCoversViewport_WithoutVendorCss(int width, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", r => r.AbortAsync());
        await Page.SetViewportSizeAsync(width, 844);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });

        var busy = new BusyState();
        var release = new TaskCompletionSource();
        var run = busy.RunAsync(BusyKeys.Global, () => release.Task);
        await using var services = new ServiceCollection().AddLogging().AddSingleton(busy).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ShopLoadingOverlay>(ParameterView.Empty)).ToHtmlString());
        release.SetResult();
        await run;

        // Production component markup over a clickable page; bUnit covers the BusyState subscription.
        await Page.EvaluateAsync("""
            async args => {
                if (args.withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('main');
                host.id = 'overlay-probe';
                host.innerHTML = '<button type="button" id="behind" style="position:fixed;inset:0">Behind</button>' + args.html;
                document.body.appendChild(host);
                window.behindClicks = 0;
                document.getElementById('behind').onclick = () => window.behindClicks++;
            }
            """, new { html, withoutVendorCss });

        var overlay = Page.GetByTestId("loading-overlay");
        await Assertions.Expect(overlay).ToBeVisibleAsync();
        await Assertions.Expect(overlay).ToHaveCSSAsync("position", "fixed");
        await Assertions.Expect(overlay).ToHaveCSSAsync("z-index", "2000");
        await Assertions.Expect(overlay).ToHaveCSSAsync("background-color", "color(srgb 0.0901961 0.0901961 0.0901961 / 0.4)");
        await Assertions.Expect(overlay).ToHaveCSSAsync("cursor", "progress");
        await Assertions.Expect(Page.Locator("#overlay-probe [role=status]")).ToHaveTextAsync(Strings.Loading);

        // Headed displays can scale CSS pixels; compare with the layout viewport rather than requested size.
        var viewportWidth = await Page.EvaluateAsync<float>("() => document.documentElement.clientWidth");
        var viewportHeight = await Page.EvaluateAsync<float>("() => document.documentElement.clientHeight");
        var bounds = (await overlay.BoundingBoxAsync())!;
        bounds.X.Should().Be(0);
        bounds.Y.Should().Be(0);
        bounds.Width.Should().BeApproximately(viewportWidth, 1);
        bounds.Height.Should().BeApproximately(viewportHeight, 1);

        var spinner = overlay.Locator(".shop-loading-overlay-spinner");
        await Assertions.Expect(spinner).ToHaveCSSAsync("width", "48px");
        var arc = spinner.Locator("circle");
        await Assertions.Expect(arc).ToHaveCSSAsync("stroke-width", "4.8px");
        await Assertions.Expect(arc).ToHaveCSSAsync("stroke-linecap", "round");
        await Assertions.Expect(arc).ToHaveCSSAsync("stroke", "rgb(23, 23, 23)");
        await Assertions.Expect(arc).ToHaveAttributeAsync("stroke-dasharray", "80 20");
        await Assertions.Expect(spinner.Locator("svg")).ToHaveCSSAsync("animation-duration", "0.8s");
        var spin = (await spinner.BoundingBoxAsync())!;
        (spin.X + spin.Width / 2).Should().BeApproximately(viewportWidth / 2, 1);
        (spin.Y + spin.Height / 2).Should().BeApproximately(viewportHeight / 2, 1);

        await Page.Mouse.ClickAsync(10, 10);
        await Page.Mouse.ClickAsync(viewportWidth / 2, viewportHeight / 2);
        (await Page.EvaluateAsync<int>("() => window.behindClicks")).Should().Be(0);
        await SaveAsync($"loading-overlay-{width}-{withoutVendorCss}");

        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await Assertions.Expect(spinner.Locator("svg")).ToHaveCSSAsync("animation-name", "none");
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.NoPreference, ForcedColors = ForcedColors.Active });
        await Assertions.Expect(overlay).ToBeVisibleAsync();
        await Assertions.Expect(arc).ToHaveCSSAsync("stroke", "rgb(0, 0, 0)");
        await SaveAsync($"loading-overlay-forced-colors-{width}-{withoutVendorCss}");

        errors.Should().BeEmpty();
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}

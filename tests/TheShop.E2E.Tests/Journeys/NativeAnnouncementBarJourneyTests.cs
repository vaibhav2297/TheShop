using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Components.Common;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeAnnouncementBarJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, false)]
    [InlineData(390, true)]
    [InlineData(1440, true)]
    public async Task AnnouncementBar_MatchesFigmaAndWrapsLongMessages(int width, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", r => r.AbortAsync());
        await Page.SetViewportSizeAsync(width, 844);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ShopAnnouncementBar>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ShopAnnouncementBar.Message)] = "Free shipping on orders $75+ or promo codes."
            }))).ToHtmlString());

        // Production component markup; bUnit covers parameters and attribute forwarding.
        await Page.EvaluateAsync("""
            async args => {
                if (args.withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                document.body.style.margin = '0';
                const host = document.createElement('header');
                host.id = 'announcement-probe';
                host.innerHTML = args.html;
                document.body.prepend(host);
                await document.fonts.load('500 16px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, new { html, withoutVendorCss });

        var bar = Page.Locator("#announcement-probe .shop-announcement-bar");
        var message = bar.Locator(".shop-announcement-bar-message");
        await Assertions.Expect(bar).ToHaveCSSAsync("background-color", "rgb(23, 23, 23)");
        await Assertions.Expect(bar).ToHaveCSSAsync("padding", "4px 16px");
        await Assertions.Expect(bar).ToHaveCSSAsync("min-height", "32px");
        await Assertions.Expect(message).ToHaveCSSAsync("color", "rgb(255, 255, 255)");
        await Assertions.Expect(message).ToHaveCSSAsync("font-size", "16px");
        await Assertions.Expect(message).ToHaveCSSAsync("font-weight", "500");
        await Assertions.Expect(message).ToHaveCSSAsync("letter-spacing", "0.25px");
        await Assertions.Expect(message).ToHaveCSSAsync("margin", "0px");
        (await message.EvaluateAsync<string>("el => getComputedStyle(el).fontFamily")).Should().StartWith("\"Space Grotesk\"");
        (await message.EvaluateAsync<bool>("el => document.fonts.check('500 16px \"Space Grotesk\"')")).Should().BeTrue();

        var viewportWidth = await Page.EvaluateAsync<float>("() => document.documentElement.clientWidth");
        var barBox = (await bar.BoundingBoxAsync())!;
        barBox.X.Should().Be(0);
        barBox.Width.Should().BeApproximately(viewportWidth, 1);
        var textBox = (await message.BoundingBoxAsync())!;
        // Figma is a 1440px single-line specimen; narrower viewports wrap and the bar hugs the text.
        barBox.Height.Should().BeApproximately(Math.Max(32, textBox.Height + 8), 1);
        if (width >= 1440) barBox.Height.Should().BeApproximately(32, 1);
        (textBox.X + textBox.Width / 2).Should().BeApproximately(barBox.X + barBox.Width / 2, 1);
        (textBox.Y + textBox.Height / 2).Should().BeApproximately(barBox.Y + barBox.Height / 2, 1);
        await SaveAsync($"announcement-{width}-{withoutVendorCss}");

        await message.EvaluateAsync("""
            el => {
                el.textContent = 'Free shipping on orders $75+ or promo codes. Limited-time offer on selected devices and accessories across Canada.';
                document.documentElement.style.fontSize = '32px';
            }
            """);
        await Assertions.Expect(message).ToHaveCSSAsync("font-size", "32px");
        (await bar.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1")).Should().BeTrue();
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1")).Should().BeTrue();
        var wrappedBox = (await bar.BoundingBoxAsync())!;
        wrappedBox.Height.Should().BeGreaterThan(64, "long enlarged text wraps and the bar grows with it");
        await Assertions.Expect(message).ToHaveCSSAsync("text-align", "center");
        await SaveAsync($"announcement-large-text-{width}-{withoutVendorCss}");

        errors.Should().BeEmpty();
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}

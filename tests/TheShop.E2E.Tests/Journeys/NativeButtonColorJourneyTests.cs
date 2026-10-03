using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeButtonColorJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, 844)]
    [InlineData(1440, 900)]
    public async Task Colors_AllVariants_PreserveTokenContractAndRecordKnownContrastGaps(int width, int height)
    {
        var backendRequests = 0;
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            Interlocked.Increment(ref backendRequests);
            await route.AbortAsync();
        });
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        await Page.EvaluateAsync("""
            async () => {
                for (const link of document.querySelectorAll('link[rel="stylesheet"]')) {
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                }
                document.getElementById('app').style.display = 'none';
                document.getElementById('blazor-error-ui').style.display = 'none';
                const host = document.createElement('main');
                host.id = 'button-colors';
                host.className = 'shop-native';
                host.style.cssText = 'display:grid;grid-template-columns:repeat(3,minmax(0,1fr));' +
                    'gap:16px 8px;padding:16px;max-width:760px;background:white;justify-items:start';
                host.innerHTML = '<span>Filled</span><span>Outlined</span><span>Text</span>';
                document.body.appendChild(host);
                await document.fonts.load('500 14px "Space Grotesk"');
                await document.fonts.ready;
                if (![...document.styleSheets].some(sheet => /\/TheShop\.css(?:\?|$)/i.test(sheet.href) && !sheet.disabled))
                    throw new Error('Compiled project CSS must be loaded.');
            }
            """);

        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var host = Page.Locator("#button-colors");
        foreach (var disabled in new[] { false, true })
            foreach (var color in Enum.GetValues<ShopColor>())
                foreach (var variant in Enum.GetValues<ShopVariant>())
                {
                    var parameters = new Dictionary<string, object?>
                    {
                        [nameof(ShopButton.Color)] = color,
                        [nameof(ShopButton.Variant)] = variant,
                        [nameof(ShopButton.Disabled)] = disabled,
                        [nameof(ShopButton.ChildContent)] = (RenderFragment)(builder => builder.AddContent(0, color.ToString())),
                        [nameof(ShopButton.AdditionalAttributes)] = new Dictionary<string, object>
                        {
                            ["data-testid"] = Id(color, variant, disabled)
                        }
                    };
                    var html = await renderer.Dispatcher.InvokeAsync(async () =>
                        (await renderer.RenderComponentAsync<ShopButton>(ParameterView.FromDictionary(parameters))).ToHtmlString());
                    await host.EvaluateAsync("(host, html) => host.insertAdjacentHTML('beforeend', html)", html);
                }

        // Independent expectations: base/contrast roles come directly from the palette; hover states remain separate.
        (ShopColor Color, string Fill, string Foreground, string Text, string Hover, string Pressed, string OnInteraction)[] colors =
        [
            (ShopColor.Primary, "rgb(23, 23, 23)", "rgb(255, 255, 255)", "rgb(23, 23, 23)", "rgb(51, 51, 51)", "rgb(0, 0, 0)", "rgb(255, 255, 255)"),
            (ShopColor.Secondary, "rgb(122, 122, 122)", "rgb(255, 255, 255)", "rgb(122, 122, 122)", "rgb(89, 89, 89)", "rgb(89, 89, 89)", "rgb(255, 255, 255)"),
            (ShopColor.Tertiary, "rgb(232, 232, 232)", "rgb(23, 23, 23)", "rgb(232, 232, 232)", "rgb(237, 237, 237)", "rgb(224, 224, 224)", "rgb(23, 23, 23)"),
            (ShopColor.Info, "rgb(66, 157, 255)", "rgb(255, 255, 255)", "rgb(66, 157, 255)", "rgb(7, 92, 168)", "rgb(7, 92, 168)", "rgb(255, 255, 255)"),
            (ShopColor.Success, "rgb(66, 255, 131)", "rgb(255, 255, 255)", "rgb(66, 255, 131)", "rgb(20, 108, 55)", "rgb(20, 108, 55)", "rgb(255, 255, 255)"),
            (ShopColor.Warning, "rgb(255, 192, 66)", "rgb(255, 255, 255)", "rgb(255, 192, 66)", "rgb(128, 84, 0)", "rgb(128, 84, 0)", "rgb(255, 255, 255)"),
            (ShopColor.Error, "rgb(255, 66, 66)", "rgb(255, 255, 255)", "rgb(255, 66, 66)", "rgb(180, 35, 24)", "rgb(180, 35, 24)", "rgb(255, 255, 255)"),
            (ShopColor.Surface, "rgb(255, 255, 255)", "rgb(23, 23, 23)", "rgb(23, 23, 23)", "rgb(245, 245, 245)", "rgb(232, 232, 232)", "rgb(23, 23, 23)")
        ];

        await Page.Keyboard.PressAsync("Tab");
        var first = Page.GetByTestId(Id(ShopColor.Primary, ShopVariant.Filled));
        await Assertions.Expect(first).ToBeFocusedAsync();
        (await first.EvaluateAsync<bool>("el => el.matches(':focus-visible') && getComputedStyle(el).outlineStyle === 'solid' && parseFloat(getComputedStyle(el).outlineWidth) > 0"))
            .Should().BeTrue("keyboard focus must remain visible");

        foreach (var expected in colors)
            foreach (var variant in Enum.GetValues<ShopVariant>())
            {
                var button = Page.GetByTestId(Id(expected.Color, variant));
                var subject = $"{expected.Color} {variant}";
                var transparentContrastGap = variant != ShopVariant.Filled &&
                    expected.Color is not (ShopColor.Primary or ShopColor.Surface);
                var restingContrastGap = transparentContrastGap || variant == ShopVariant.Filled &&
                    expected.Color is not (ShopColor.Primary or ShopColor.Tertiary or ShopColor.Surface);
                await Page.Mouse.MoveAsync(0, 0);
                await AssertColorsAsync(button, variant == ShopVariant.Filled ? expected.Fill : "rgba(0, 0, 0, 0)",
                    variant == ShopVariant.Filled ? expected.Foreground : expected.Text, subject + " resting", restingContrastGap);
                var shadow = await button.EvaluateAsync<string>("el => getComputedStyle(el).boxShadow");
                if (variant == ShopVariant.Outlined)
                {
                    shadow.Should().Contain("inset", subject + " keeps an outline");
                    var line = expected.Color is ShopColor.Primary or ShopColor.Tertiary or ShopColor.Surface
                        ? "rgb(224, 224, 224)" : expected.Text;
                    shadow.Should().Contain(line, subject + " uses its own outline color");
                }
                else
                    shadow.Should().Be("none", subject + " must not inherit an outline");

                await button.HoverAsync();
                await AssertColorsAsync(button, variant == ShopVariant.Filled ? expected.Hover : "rgb(245, 245, 245)",
                    variant == ShopVariant.Filled ? expected.OnInteraction : expected.Text, subject + " hover", transparentContrastGap);
                await Page.Mouse.DownAsync();
                try
                {
                    await AssertColorsAsync(button, variant == ShopVariant.Filled ? expected.Pressed : "rgb(232, 232, 232)",
                        variant == ShopVariant.Filled ? expected.OnInteraction : expected.Text, subject + " pressed", transparentContrastGap);
                }
                finally
                {
                    await Page.Mouse.UpAsync();
                }

                var disabled = Page.GetByTestId(Id(expected.Color, variant, true));
                await Assertions.Expect(disabled).ToBeDisabledAsync();
                await disabled.EvaluateAsync("el => { el.dataset.clicks = '0'; el.onclick = () => el.dataset.clicks++; el.click(); }");
                await disabled.HoverAsync();
                await Page.Mouse.DownAsync();
                try
                {
                    (await disabled.EvaluateAsync<string[]>("el => [getComputedStyle(el).backgroundColor, getComputedStyle(el).color]"))
                        .Should().Equal(variant == ShopVariant.Filled ? "rgb(232, 232, 232)" : "rgba(0, 0, 0, 0)", "rgb(122, 122, 122)");
                }
                finally
                {
                    await Page.Mouse.UpAsync();
                }
                (await disabled.GetAttributeAsync("data-clicks")).Should().Be("0", "disabled controls cannot activate");
            }

        await Page.Mouse.MoveAsync(0, 0);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"))
            .Should().BeTrue("the color matrix must fit the viewport");
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        await host.ScreenshotAsync(new() { Path = Path.Combine(directory, $"button-colors-{width}.png") });

        await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
        foreach (var variant in Enum.GetValues<ShopVariant>())
        {
            (await Page.GetByTestId(Id(ShopColor.Error, variant)).EvaluateAsync<bool>("el => getComputedStyle(el).borderTopStyle === 'solid' && parseFloat(getComputedStyle(el).borderTopWidth) > 0"))
                .Should().BeTrue("forced colors must retain a control boundary when shadows are suppressed");
        }
        backendRequests.Should().Be(0, "color verification must not call application services");
    }

    private static string Id(ShopColor color, ShopVariant variant, bool disabled = false) => $"{color}-{variant}-{disabled}";

    private static async Task AssertColorsAsync(ILocator button, string background, string foreground, string subject, bool knownContrastGap)
    {
        await Assertions.Expect(button).ToHaveCSSAsync("background-color", background);
        await Assertions.Expect(button).ToHaveCSSAsync("color", foreground);
        (await button.EvaluateAsync<string[]>("el => [getComputedStyle(el).backgroundColor, getComputedStyle(el).color]"))
            .Should().Equal([background, foreground], subject);
        var contrast = await button.EvaluateAsync<double>("""
            el => {
                const style = getComputedStyle(el);
                const rgb = value => value.match(/[\d.]+/g).map(Number);
                const foreground = rgb(style.color), background = rgb(style.backgroundColor);
                // This specimen owns an opaque white surface beneath transparent outlined/text buttons.
                if (background[3] === 0) background.splice(0, 3, 255, 255, 255);
                const luminance = color => color.slice(0, 3).map(value => {
                    const channel = value / 255;
                    return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
                }).reduce((sum, value, index) => sum + value * [0.2126, 0.7152, 0.0722][index], 0);
                const a = luminance(foreground), b = luminance(background);
                return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
            }
            """);
        if (knownContrastGap)
        {
            // Owner-selected palette defaults are an explicit accessibility gap, not an AA pass.
            contrast.Should().BeLessThan(4.5, subject + " has a documented palette contrast gap");
            TestContext.Current.TestOutputHelper?.WriteLine($"KNOWN CONTRAST GAP: {subject}: {contrast:F2}:1 (needs 4.5:1)");
        }
        else
            contrast.Should().BeGreaterThanOrEqualTo(4.5, subject + " uses small button text");
    }
}

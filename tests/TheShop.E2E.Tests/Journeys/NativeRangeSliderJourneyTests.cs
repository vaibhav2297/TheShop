using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeRangeSliderJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, false)]
    [InlineData(390, true)]
    [InlineData(1440, true)]
    public async Task Catalogue_RangeEditorsKeyboardAndPointer_PreserveQueryAndNativeGeometry(int width, bool withoutVendorCss)
    {
        var unexpectedRequests = 0;
        var browserErrors = new List<string>();
        Page.PageError += (_, error) => browserErrors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (path.EndsWith("/rest/v1/products", StringComparison.Ordinal) && route.Request.Method == "GET")
                await route.FulfillAsync(new()
                {
                    ContentType = "application/json",
                    Headers = new Dictionary<string, string> { ["Content-Range"] = "*/0", ["Access-Control-Expose-Headers"] = "Content-Range" },
                    Body = "[]"
                });
            else if (path.EndsWith("/rpc/get_catalogue_filters", StringComparison.Ordinal))
                await route.FulfillAsync(new() { ContentType = "application/json", Body = """
                    {"categories":[],"brands":[],"option_types":[],"price_min":0,"price_max":100}
                    """ });
            else { Interlocked.Increment(ref unexpectedRequests); await route.AbortAsync(); }
        });
        await Page.SetViewportSizeAsync(width, 1000);
        await Page.GotoAsync(WebRoutes.Products);
        var expander = Page.Locator(".shop-expander").Filter(new() { HasText = Strings.Filter_Price });
        await expander.Locator(".shop-expander-trigger").ClickAsync(new() { Timeout = 30_000 });
        var slider = expander.Locator(".shop-range");
        await Assertions.Expect(slider).ToBeVisibleAsync();
        if (withoutVendorCss)
            await Page.EvaluateAsync("""
                () => { for (const link of document.querySelectorAll('link[rel="stylesheet"]'))
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true; }
                """);
        var lower = slider.Locator(".shop-range-lower");
        var upper = slider.Locator(".shop-range-upper");
        var minimum = slider.Locator(".shop-field-input").Nth(0);
        var maximum = slider.Locator(".shop-field-input").Nth(1);
        // The arrow belongs to the price box, not a second copy of the slider percentage.
        await lower.FocusAsync();
        await AssertTooltipCenteredAsync(slider);
        await upper.FocusAsync();
        await AssertTooltipCenteredAsync(slider);
        await lower.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(lower).ToHaveValueAsync("0.01");
        await Page.Keyboard.PressAsync("PageUp");
        await Assertions.Expect(lower).ToHaveValueAsync("0.11");
        await Page.Keyboard.PressAsync("Home");
        await Assertions.Expect(lower).ToHaveValueAsync("0");
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(upper).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Assertions.Expect(upper).ToHaveValueAsync("99.99");
        await Page.Keyboard.PressAsync("End");
        await Assertions.Expect(upper).ToHaveValueAsync("100");

        await minimum.FocusAsync();
        await Assertions.Expect(minimum).ToHaveValueAsync("0");
        await minimum.FillAsync("20.25");
        await minimum.PressAsync("Enter");
        await Assertions.Expect(lower).ToHaveValueAsync("20.25");
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("price_min=20.25"));
        await Assertions.Expect(minimum).ToBeFocusedAsync();
        await maximum.FocusAsync();
        await Assertions.Expect(minimum).ToHaveValueAsync(new Regex("20.25"));
        await maximum.FillAsync("75.50");
        await maximum.PressAsync("Tab");
        await Assertions.Expect(upper).ToHaveValueAsync("75.5");
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("price_max=75.5"));
        await minimum.FocusAsync();
        await minimum.FillAsync("90");
        await minimum.PressAsync("Tab");
        await Assertions.Expect(minimum).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(lower).ToHaveValueAsync("20.25");
        await minimum.FocusAsync();
        await minimum.PressAsync("Escape");
        await Assertions.Expect(minimum).Not.ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(minimum).ToHaveValueAsync("20.25");

        await upper.FocusAsync();
        await Page.Keyboard.PressAsync("Home");
        await Assertions.Expect(upper).ToHaveValueAsync("20.25");
        await lower.FocusAsync();
        await Page.Keyboard.PressAsync("End");
        await Assertions.Expect(lower).ToHaveValueAsync("20.25");
        await slider.ScrollIntoViewIfNeededAsync();
        var overlapStage = (await slider.Locator(".shop-range-stage").BoundingBoxAsync())!;
        await Page.Mouse.ClickAsync(overlapStage.X + overlapStage.Width * 0.1f, overlapStage.Y + 64);
        await Assertions.Expect(lower).Not.ToHaveValueAsync("20.25");
        await Assertions.Expect(upper).ToHaveValueAsync("20.25");
        await lower.FocusAsync();
        await Page.Keyboard.PressAsync("End");
        await Assertions.Expect(lower).ToHaveValueAsync("20.25");
        await upper.FocusAsync();
        await AssertTooltipCenteredAsync(slider);
        await Page.Keyboard.PressAsync("End");
        await Assertions.Expect(upper).ToHaveValueAsync("100");

        await slider.ScrollIntoViewIfNeededAsync();
        var stage = (await slider.Locator(".shop-range-stage").BoundingBoxAsync())!;
        await Page.Mouse.ClickAsync(stage.X + stage.Width * 0.35f, stage.Y + 64);
        await Assertions.Expect(lower).Not.ToHaveValueAsync("20.25");
        var beforeDrag = decimal.Parse(await lower.InputValueAsync(), System.Globalization.CultureInfo.InvariantCulture);
        var x = stage.X + 12 + (stage.Width - 24) * (float)beforeDrag / 100;
        await Page.Mouse.MoveAsync(x, stage.Y + 64);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(stage.X + stage.Width * 0.45f, stage.Y + 64, new() { Steps = 8 });
        await Page.Mouse.UpAsync();
        (decimal.Parse(await lower.InputValueAsync(), System.Globalization.CultureInfo.InvariantCulture)).Should().BeGreaterThan(beforeDrag);
        await Assertions.Expect(slider.Locator(".shop-range-bubble")).ToBeVisibleAsync();
        await AssertTooltipCenteredAsync(slider);
        (await slider.Locator(".shop-range-rail").BoundingBoxAsync())!.Height.Should().BeApproximately(2, 0.1f);
        (await slider.Locator(".shop-range-fill").BoundingBoxAsync())!.Height.Should().BeApproximately(3, 0.1f);
        var inputs = (await slider.Locator(".shop-range-inputs").BoundingBoxAsync())!;
        (await slider.Locator(".shop-field-control").First.BoundingBoxAsync())!.Height.Should().BeGreaterThanOrEqualTo(57);
        (await lower.BoundingBoxAsync())!.Height.Should().Be(44);
        (inputs.X + inputs.Width).Should().BeLessThanOrEqualTo(width + 1);
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        await slider.ScreenshotAsync(new() { Path = Path.Combine(directory, $"range-{width}-{withoutVendorCss}.png") });
        await minimum.FocusAsync();
        await Assertions.Expect(minimum).ToHaveValueAsync(new Regex(@"^\d+(\.\d{1,2})?$"));
        await upper.FocusAsync();
        await AssertTooltipCenteredAsync(slider);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"range-tooltip-endpoint-{width}-{withoutVendorCss}.png") });
        var scrollRoot = Page.Locator("[data-shop-scroll-root]");
        await scrollRoot.EvaluateAsync("async el => { el.scrollBy(0, 10); await new Promise(requestAnimationFrame); await new Promise(requestAnimationFrame); }");
        await AssertTooltipCenteredAsync(slider);
        await expander.Locator(".shop-expander-trigger").ClickAsync();
        await Assertions.Expect(Page.Locator(".shop-range-bubble:popover-open")).ToHaveCountAsync(0);
        await expander.Locator(".shop-expander-trigger").ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = Strings.Filter_Clear, Exact = true })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = Strings.Filter_Clear, Exact = true }).ClickAsync();
        await Assertions.Expect(lower).ToHaveValueAsync("0");
        await Assertions.Expect(upper).ToHaveValueAsync("100");
        await Assertions.Expect(Page).Not.ToHaveURLAsync(new Regex("price_min|price_max"));
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '200%'");
        await Assertions.Expect(slider.Locator(".shop-field-label").First).ToBeVisibleAsync();
        (await slider.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1")).Should().BeTrue();
        await slider.ScreenshotAsync(new() { Path = Path.Combine(directory, $"range-large-text-{width}-{withoutVendorCss}.png") });
        var labelGeometry = await slider.Locator(".shop-field-label").EvaluateAllAsync<string>("labels => JSON.stringify(labels.map(el => ({scroll:el.scrollWidth,width:el.clientWidth,font:getComputedStyle(el).fontSize,container:el.closest('.shop-range').clientWidth,columns:getComputedStyle(el.closest('.shop-range-inputs')).gridTemplateColumns})))");
        (await slider.Locator(".shop-field-label").EvaluateAllAsync<bool>("labels => labels.every(el => el.scrollWidth <= el.clientWidth + 1)")).Should().BeTrue(labelGeometry);
        if (!withoutVendorCss) await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        // app.css normally hides this fallback; disabling that stylesheet does not mean Blazor failed.
        (await Page.Locator("#blazor-error-ui").EvaluateAsync<string>("el => el.style.display")).Should().NotBe("block");
        browserErrors.Should().BeEmpty();
        unexpectedRequests.Should().Be(0);
    }

    private static async Task AssertTooltipCenteredAsync(ILocator slider)
    {
        var focusedThumb = slider.Locator("input[type='range']:focus");
        var lower = (await focusedThumb.GetAttributeAsync("class"))!.Contains("shop-range-lower", StringComparison.Ordinal);
        await Assertions.Expect(slider.Locator(".shop-range-bubble")).ToHaveClassAsync(new Regex(lower ? "shop-range-bubble-lower" : "shop-range-bubble-upper"));
        var price = slider.Locator(".shop-range-bubble span");
        await Assertions.Expect(price).ToBeVisibleAsync();
        var textLines = await price.EvaluateAsync<int>("""
            el => {
                const range = document.createRange();
                range.selectNodeContents(el);
                return range.getClientRects().length;
            }
            """);
        textLines.Should().Be(1, "a normal currency value must not wrap when its thumb reaches a mobile viewport edge");
        var offset = await price.EvaluateAsync<double>("""
            el => {
                const arrow = getComputedStyle(el, '::after');
                const transform = new DOMMatrixReadOnly(arrow.transform);
                const arrowWidth = parseFloat(arrow.borderLeftWidth) + parseFloat(arrow.borderRightWidth);
                return parseFloat(arrow.left) + transform.m41 + arrowWidth / 2 - el.clientWidth / 2;
            }
            """);
        offset.Should().BeApproximately(0, 0.6, "the pointer must stay centered below the price box at every thumb position");
        var box = (await price.BoundingBoxAsync())!;
        var thumbCenter = await focusedThumb.EvaluateAsync<double>("""
            el => {
                const rect = el.getBoundingClientRect();
                const radius = parseFloat(getComputedStyle(document.documentElement).fontSize) * 0.75;
                const fraction = (Number(el.value) - Number(el.min)) / (Number(el.max) - Number(el.min));
                return rect.left + radius + fraction * (rect.width - 2 * radius);
            }
            """);
        (box.X + box.Width / 2d).Should().BeApproximately(thumbCenter, 0.6, "the price box and its centered arrow must stay above the thumb, including endpoints");
        (await slider.Locator(".shop-range-bubble").EvaluateAsync<bool>("el => el.matches(':popover-open')"))
            .Should().BeTrue("the tooltip must escape the expander's clipping boundary");
    }
}

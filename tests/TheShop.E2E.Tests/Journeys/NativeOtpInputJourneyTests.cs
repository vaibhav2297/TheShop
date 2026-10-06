using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeOtpInputJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    public async Task OtpInput_MatchesFigmaAndDrivesKeyboardAndPaste(int width, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", r => r.AbortAsync());
        await Page.SetViewportSizeAsync(width, 844);
        await Page.GotoAsync(WebRoutes.Auth.SignInVerifyWith("otp-journey@example.invalid"));

        var otp = Page.GetByTestId("otp-input");
        var boxes = otp.Locator("input.shop-otp-digit");
        await Assertions.Expect(boxes).ToHaveCountAsync(6, new() { Timeout = 30_000 });
        await Assertions.Expect(boxes.First).ToBeFocusedAsync();
        await Page.EvaluateAsync("""
            async withoutVendorCss => {
                if (withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                await document.fonts.load('500 20px "Space Grotesk"');
                await document.fonts.ready;
            }
            """, withoutVendorCss);

        // Figma 2629:1708: no labels, H6 centered digits, 1px idle and 2px primary focused outline.
        (await otp.Locator("label").CountAsync()).Should().Be(0);
        var first = boxes.First;
        await Assertions.Expect(first).ToHaveCSSAsync("font-size", "20px");
        await Assertions.Expect(first).ToHaveCSSAsync("font-weight", "500");
        await Assertions.Expect(first).ToHaveCSSAsync("letter-spacing", "0.25px");
        await Assertions.Expect(first).ToHaveCSSAsync("text-align", "center");
        (await first.EvaluateAsync<string>("el => getComputedStyle(el).fontFamily")).Should().StartWith("\"Space Grotesk\"");
        var controls = otp.Locator(".shop-field-control");
        await Assertions.Expect(controls.First).ToHaveCSSAsync("box-shadow", "rgb(23, 23, 23) 0px 0px 0px 2px inset");
        await Assertions.Expect(controls.Nth(1)).ToHaveCSSAsync("box-shadow", "rgb(224, 224, 224) 0px 0px 0px 1px inset");
        await Assertions.Expect(otp.Locator(".shop-otp-input-digits"))
            .ToHaveCSSAsync("column-gap", width >= 600 ? "24px" : "8px");

        var widths = new List<float>();
        for (var i = 0; i < 6; i++)
        {
            var box = (await controls.Nth(i).BoundingBoxAsync())!;
            box.Height.Should().BeApproximately(63, 1.5f);
            widths.Add(box.Width);
        }
        widths.Max().Should().BeApproximately(widths.Min(), 1);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1")).Should().BeTrue();
        await SaveAsync($"otp-{width}-{withoutVendorCss}-idle");

        // Repeated digits still advance; letters are rejected.
        await Page.Keyboard.TypeAsync("11a2", new() { Delay = 60 });
        await ExpectValuesAsync(boxes, "1", "1", "2", "", "", "");
        await Assertions.Expect(boxes.Nth(3)).ToBeFocusedAsync();

        // Backspace on an empty box clears and focuses the previous one; arrows move focus.
        await Page.Keyboard.PressAsync("Backspace");
        await ExpectValuesAsync(boxes, "1", "1", "", "", "", "");
        await Assertions.Expect(boxes.Nth(2)).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Assertions.Expect(boxes.Nth(1)).ToBeFocusedAsync();

        await first.EvaluateAsync("""
            el => {
                const data = new DataTransfer();
                data.setData('text', '98-76-54');
                el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
            }
            """);
        await ExpectValuesAsync(boxes, "9", "8", "7", "6", "5", "4");
        await Assertions.Expect(Page.GetByTestId("otp-submit")).ToBeEnabledAsync();
        await SaveAsync($"otp-{width}-{withoutVendorCss}-filled");

        errors.Should().BeEmpty();
        // app.css hides the error banner, so it is only meaningful while that sheet is enabled.
        if (!withoutVendorCss)
            await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    private static async Task ExpectValuesAsync(ILocator boxes, params string[] values)
    {
        for (var i = 0; i < values.Length; i++)
            await Assertions.Expect(boxes.Nth(i)).ToHaveValueAsync(values[i]);
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}

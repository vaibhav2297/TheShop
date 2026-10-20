using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeSignUpLayoutJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(1440, 900, false)]
    [InlineData(1440, 900, true)]
    [InlineData(834, 1194, true)]
    [InlineData(390, 844, true)]
    public async Task SignUpAndVerify_MatchFigmaAcrossViewportsAndReflow(int width, int height, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (path.EndsWith("/rpc/customer_exists"))
                await route.FulfillAsync(new() { ContentType = "application/json", Body = "false" });
            else if (path.EndsWith("/auth/v1/otp"))
                await route.FulfillAsync(new() { ContentType = "application/json", Body = "{}" });
            else await route.AbortAsync();
        });
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync(WebRoutes.Auth.SignUp);
        await Page.Locator("#signup-first-name").WaitForAsync(new() { Timeout = 30000 });
        await PrepareStylesAsync(withoutVendorCss);
        var signupPanel = (await Page.Locator(".shop-auth-panel").BoundingBoxAsync())!;
        signupPanel.Width.Should().BeApproximately(width == 390 ? 358 : 589.7143f, 1);
        await Assertions.Expect(Page.Locator(".shop-auth-panel [class*='mud-']")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator(".shop-auth-navigation a")).ToHaveAttributeAsync("href", WebRoutes.Auth.SignIn);
        await Assertions.Expect(Page.GetByTestId("signup-submit")).ToBeEnabledAsync();
        await Page.Locator("#signup-first-name").FillAsync("John");
        var firstName = (await Page.Locator(".shop-auth-name-fields .shop-field-control").First.BoundingBoxAsync())!;
        var lastName = (await Page.Locator(".shop-auth-name-fields .shop-field-control").Last.BoundingBoxAsync())!;
        firstName.Y.Should().BeApproximately(height - (width == 390 ? 450 : 401), 3);
        if (width == 390) lastName.Y.Should().BeGreaterThan(firstName.Y);
        else lastName.Y.Should().BeApproximately(firstName.Y, 1);
        await AssertNoOverflowAsync();
        await SaveAsync($"signup-layout-{width}-{withoutVendorCss}");
        if (width == 390)
        {
            await Page.EvaluateAsync("document.documentElement.style.fontSize='200%'");
            await AssertNoOverflowAsync();
            (await Page.Locator(".shop-auth-signup-fields").EvaluateAsync<bool>("""
                form => [...form.querySelectorAll('.shop-field-control')].every(control => {
                    const field = control.getBoundingClientRect();
                    const bounds = form.getBoundingClientRect();
                    return field.left >= bounds.left - 1 && field.right <= bounds.right + 1;
                })
                """)).Should().BeTrue("enlarged fields stay inside the form's content area");
            await SaveAsync("signup-large-text-390");
            await Page.EvaluateAsync("document.documentElement.style.fontSize=''");
        }
        await FillAndSubmitSignUpAsync();
        var otp = Page.GetByTestId("otp-input");
        await otp.WaitForAsync(new() { Timeout = 30000 });
        await Page.EvaluateAsync("""
            async withoutVendorCss => {
                await document.fonts.load('500 16px "Space Grotesk"');
                await document.fonts.load('800 60px "Barlow Condensed"');
                await document.fonts.ready;
                if (withoutVendorCss) {
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) throw new Error('Unexpected legacy stylesheet');

                }
            }
            """, withoutVendorCss);
        var panel = Page.Locator(".shop-auth-panel");
        await Assertions.Expect(panel.Locator("[class*='mud-']")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByTestId("otp-change"))
            .ToHaveAttributeAsync("href", WebRoutes.Auth.SignUp);
        await Assertions.Expect(otp).ToHaveAccessibleNameAsync(Strings.Verify_CodeLabel);
        await Assertions.Expect(otp.Locator("input").First).ToBeFocusedAsync();
        await Assertions.Expect(Page.GetByTestId("otp-submit")).ToBeEnabledAsync();
        await Assertions.Expect(Page.GetByTestId("otp-countdown")).ToHaveAttributeAsync("role", "timer");

        var box = (await panel.BoundingBoxAsync())!;
        box.X.Should().BeApproximately(width == 1440 ? 425.1429f : width == 834 ? 122.1429f : 16, 1);
        box.Width.Should().BeApproximately(width == 390 ? 358 : 589.7143f, 1);
        box.Height.Should().BeApproximately(height - (width == 390 ? 32 : 64), 1);
        var identity = (await Page.Locator(".shop-auth-identity").BoundingBoxAsync())!;
        identity.Y.Should().BeApproximately(width == 390 ? 232 : 256, 1);
        identity.Height.Should().BeApproximately(92, 1);
        var controls = otp.Locator(".shop-field-control");
        var first = (await controls.First.BoundingBoxAsync())!;
        // Native normal line-height can round by a device pixel at the host display scale.
        first.Y.Should().BeApproximately(width == 390 ? 679 : height - 189, 1.5f);
        first.Height.Should().BeApproximately(63, 1.5f);
        first.Width.Should().BeApproximately(width == 390 ? 45 : 67.619f, 1);
        (await Page.GetByTestId("otp-submit").BoundingBoxAsync())!.Y
            .Should().BeApproximately(height - (width == 390 ? 86 : 110), 1);
        await AssertNoOverflowAsync();
        await SaveAsync($"signup-verify-{width}-{withoutVendorCss}");

        if (width == 390)
        {
            foreach (var probe in new[] { 320, 767, 768, 1023, 1024 })
            {
                await Page.SetViewportSizeAsync(probe, 844);
                await AssertNoOverflowAsync();
            }
            await Page.SetViewportSizeAsync(width, height);
            await Page.EvaluateAsync("document.documentElement.style.fontSize='200%'");
            await AssertNoOverflowAsync();
            await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active, ReducedMotion = ReducedMotion.Reduce });
            await otp.Locator("input").First.FocusAsync();
            await Page.Keyboard.TypeAsync("012345");
            await Assertions.Expect(Page.GetByTestId("otp-submit")).ToBeEnabledAsync();
            for (var i = 0; i < 6; i++)
                await Assertions.Expect(otp.Locator("input").Nth(i)).ToHaveValueAsync(i.ToString());
            (await otp.Locator("input").First.EvaluateAsync<bool>("""
                el => {
                    const style = getComputedStyle(el);
                    const canvas = document.createElement('canvas').getContext('2d');
                    canvas.font = style.font;
                    return el.clientWidth >= canvas.measureText('0').width + parseFloat(style.paddingLeft) + parseFloat(style.paddingRight);
                }
                """)).Should().BeTrue("enlarged digits must fit inside their inputs");
            await otp.Locator("input").First.FocusAsync();
            await Assertions.Expect(controls.First).ToHaveCSSAsync("outline-style", "solid");
            await SaveAsync("signup-verify-accessibility-390");
        }
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_EnterFailureAndResend_PreserveCodeAndBusyContracts()
    {
        var releaseVerify = new TaskCompletionSource();
        var releaseResend = new TaskCompletionSource();
        var verifies = 0;
        var resends = 0;
        var signupRequested = false;
        string? payload = null;
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (path.EndsWith("/auth/v1/verify"))
            {
                verifies++;
                payload = route.Request.PostData;
                await releaseVerify.Task;
                await route.FulfillAsync(new()
                {
                    Status = 403,
                    ContentType = "application/json",
                    Body = """{"code":403,"error_code":"otp_expired","msg":"Token has expired or is invalid"}"""
                });
            }
            else if (path.EndsWith("/rpc/customer_exists"))
                await route.FulfillAsync(new() { ContentType = "application/json", Body = "false" });
            else if (path.EndsWith("/auth/v1/otp"))
            {
                if (!signupRequested)
                {
                    signupRequested = true;
                    await route.FulfillAsync(new() { ContentType = "application/json", Body = "{}" });
                    return;
                }
                resends++;
                await releaseResend.Task;
                await route.FulfillAsync(new() { ContentType = "application/json", Body = "{}" });
            }
            else await route.AbortAsync();
        });
        try
        {
            await Page.SetViewportSizeAsync(390, 844);
            await Page.GotoAsync(WebRoutes.Auth.SignUp);
            await FillAndSubmitSignUpAsync();
            var inputs = Page.GetByTestId("otp-input").Locator("input");
            await inputs.First.WaitForAsync(new() { Timeout = 30000 });
            await inputs.First.FocusAsync();
            await Page.Keyboard.TypeAsync("012345");
            var submit = Page.GetByTestId("otp-submit");
            await Assertions.Expect(submit).ToBeEnabledAsync();
            await inputs.Last.PressAsync("Enter");
            await Assertions.Expect(submit).ToHaveAttributeAsync("aria-busy", "true");
            await Assertions.Expect(inputs.First).ToBeDisabledAsync();
            verifies.Should().Be(1);
            payload.Should().Contain("012345");
            releaseVerify.SetResult();
            await Assertions.Expect(inputs.First).ToBeEnabledAsync();
            await Assertions.Expect(inputs.First).ToHaveValueAsync("");
            await Assertions.Expect(submit).ToBeEnabledAsync();
            await Assertions.Expect(Page.GetByTestId("notification")).ToContainTextAsync(Strings.Auth_Code_InvalidOrExpired);
            await Page.GetByTestId("notification").GetByRole(AriaRole.Button).ClickAsync();

            var resend = Page.GetByTestId("otp-resend");
            await Assertions.Expect(resend).ToBeVisibleAsync(new() { Timeout = 70000 });
            await SaveAsync("signup-verify-resend-390");
            await resend.ClickAsync();
            await Assertions.Expect(resend).ToHaveAttributeAsync("aria-busy", "true");
            await Assertions.Expect(inputs.First).ToBeDisabledAsync();
            resends.Should().Be(1);
            releaseResend.SetResult();
            await Assertions.Expect(Page.GetByTestId("otp-countdown")).ToBeVisibleAsync();
            await Assertions.Expect(inputs.First).ToBeEnabledAsync();
        }
        finally
        {
            releaseVerify.TrySetResult();
            releaseResend.TrySetResult();
        }
    }

    private async Task FillAndSubmitSignUpAsync()
    {
        var first = Page.Locator("#signup-first-name");
        await first.WaitForAsync(new() { Timeout = 30000 });
        await first.FocusAsync();
        await first.FillAsync("John");
        await first.PressAsync("Tab");
        await Assertions.Expect(Page.Locator("#signup-last-name")).ToBeFocusedAsync();
        await Page.Locator("#signup-last-name").FillAsync("Doe");
        await Page.Locator("#signup-email").FillAsync("JohnDoe@gmail.com");
        await Page.Locator("#signup-date").FillAsync("2000-01-01");
        await Page.Locator("#signup-age").CheckAsync();
        await Assertions.Expect(Page.GetByTestId("signup-submit")).ToBeEnabledAsync();
        await Page.GetByTestId("signup-submit").PressAsync("Enter");
        await Page.GetByTestId("otp-input").WaitForAsync();
        var firstDigit = Page.GetByTestId("otp-input").Locator("input").First;
        await Assertions.Expect(firstDigit).ToBeFocusedAsync();
        var dismiss = Page.GetByTestId("notification").GetByRole(AriaRole.Button);
        if (await dismiss.CountAsync() > 0) await dismiss.ClickAsync();
        await firstDigit.FocusAsync();
    }

    private Task PrepareStylesAsync(bool withoutVendorCss) => Page.EvaluateAsync("""
        async withoutVendorCss => {
            await document.fonts.load('500 16px "Space Grotesk"');
            await document.fonts.load('800 60px "Barlow Condensed"');
            await document.fonts.ready;
            if (withoutVendorCss) {
                for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                    if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) throw new Error('Unexpected legacy stylesheet');

            }
        }
        """, withoutVendorCss);

    private async Task AssertNoOverflowAsync() =>
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"))
            .Should().BeTrue();

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new()
        {
            Path = Path.Combine(directory, name + ".png"),
            FullPage = true,
            Animations = ScreenshotAnimations.Disabled
        });
    }
}


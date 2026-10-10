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
public sealed class NativeDateFieldJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, false)]
    [InlineData(1440, true)]
    public async Task SignUp_NativeDate_ValidatesAgeAndPreservesSubmission(int width, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        var releaseOtp = new TaskCompletionSource();
        var otpRequests = 0;
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (path.EndsWith("/rpc/customer_exists"))
                await route.FulfillAsync(new() { ContentType = "application/json", Body = "false" });
            else if (path.EndsWith("/auth/v1/otp"))
            {
                otpRequests++;
                await releaseOtp.Task;
                await route.FulfillAsync(new() { ContentType = "application/json", Body = "{}" });
            }
            else await route.AbortAsync();
        });
        try
        {
            await Page.SetViewportSizeAsync(width, 1000);
            await Page.GotoAsync(WebRoutes.Auth.SignUp);
            var date = Page.GetByLabel(Strings.DateOfBirth_Label, new() { Exact = true });
            await date.WaitForAsync(new() { Timeout = 30000 });
            await Assertions.Expect(date).ToHaveAttributeAsync("type", "date");
            await Assertions.Expect(date).ToHaveAttributeAsync("autocomplete", "bday");
            var maximum = await date.GetAttributeAsync("max");
            var cutoff = DateOnly.ParseExact(maximum!, "yyyy-MM-dd");
            var field = Page.Locator(".shop-date-field");
            var submit = Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(Regex.Escape(Strings.Auth_Create_Submit) + "$") });
            await Page.GetByPlaceholder(Strings.FirstName_Label, new() { Exact = true }).FillAsync("Date");
            await Page.GetByPlaceholder(Strings.LastName_Label, new() { Exact = true }).FillAsync("Probe");
            await Page.GetByPlaceholder(Strings.Email_Label, new() { Exact = true }).FillAsync("date-probe@example.test");
            await Page.GetByRole(AriaRole.Checkbox).CheckAsync();
            await Assertions.Expect(submit).ToBeDisabledAsync();

            await date.FillAsync(cutoff.AddDays(1).ToString("yyyy-MM-dd"));
            await date.PressAsync("Tab");
            await Assertions.Expect(field.Locator(".shop-field-error")).ToHaveTextAsync(Strings.Auth_Underage);
            await Assertions.Expect(submit).ToBeDisabledAsync();
            otpRequests.Should().Be(0);
            await date.FillAsync("2000-02-29");
            await date.PressAsync("Tab");
            await Assertions.Expect(date).ToHaveValueAsync("2000-02-29");
            await Assertions.Expect(submit).ToBeEnabledAsync();

            if (withoutVendorCss)
                await Page.EvaluateAsync("""
                    () => {
                        for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                            if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                    }
                    """);
            await date.FocusAsync();
            await Assertions.Expect(field.Locator(".shop-field-control")).ToHaveCSSAsync("box-shadow", "rgb(23, 23, 23) 0px 0px 0px 2px inset");
            (await field.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1")).Should().BeTrue();
            var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
            Directory.CreateDirectory(directory);
            await field.ScreenshotAsync(new() { Path = Path.Combine(directory, $"date-{width}.png") });
            await Page.EvaluateAsync("document.documentElement.style.fontSize='200%'");
            await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active, ReducedMotion = ReducedMotion.Reduce });
            await Assertions.Expect(field.Locator(".shop-field-control")).ToHaveCSSAsync("outline-style", "solid");
            (await field.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1")).Should().BeTrue();
            await field.ScreenshotAsync(new() { Path = Path.Combine(directory, $"date-accessibility-{width}.png") });
            await Page.EvaluateAsync("""
                () => {
                    document.documentElement.style.fontSize='';
                    for (const link of document.querySelectorAll('link[rel=stylesheet]')) link.disabled = false;
                }
                """);
            await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.None, ReducedMotion = ReducedMotion.NoPreference });
            await date.FillAsync("");
            await date.PressAsync("Tab");
            await Assertions.Expect(field.Locator(".shop-field-error")).ToHaveTextAsync(Strings.Auth_Dob_InPast);
            await Assertions.Expect(submit).ToBeDisabledAsync();
            await date.FillAsync(maximum!);
            await date.PressAsync("Tab");
            await Assertions.Expect(submit).ToBeEnabledAsync();
            await submit.ClickAsync();
            await Assertions.Expect(date).ToBeDisabledAsync();
            await Assertions.Expect(submit).ToBeDisabledAsync();
            releaseOtp.SetResult();
            await Assertions.Expect(Page).ToHaveURLAsync(new Regex(Regex.Escape(WebRoutes.Auth.SignUpVerify) + "$"));
            otpRequests.Should().Be(1);
            errors.Should().BeEmpty();
        }
        finally { releaseOtp.TrySetResult(); }
    }

    [Fact]
    public async Task SignUp_NativeDate_OpensPickerFromAnywhereInTheControl()
    {
        await Page.GotoAsync(WebRoutes.Auth.SignUp);
        var date = Page.GetByLabel(Strings.DateOfBirth_Label, new() { Exact = true });
        await date.WaitForAsync(new() { Timeout = 30000 });
        // Headless browsers cannot observe the native popup; count showPicker calls instead.
        await Page.EvaluateAsync("""
            () => {
                window.__shopDatePickerCalls = 0;
                HTMLInputElement.prototype.showPicker = function () { window.__shopDatePickerCalls++; };
            }
            """);
        var control = Page.Locator(".shop-date-field .shop-field-control");
        await Assertions.Expect(control).ToHaveCSSAsync("cursor", "pointer");
        await Assertions.Expect(date).ToHaveCSSAsync("cursor", "pointer");

        var box = (await control.BoundingBoxAsync())!;
        await control.ClickAsync(new() { Position = new() { X = 2, Y = box.Height / 2 } });
        await Assertions.Expect(date).ToBeFocusedAsync();
        await control.Locator("label").ClickAsync();
        await date.ClickAsync();
        (await Page.EvaluateAsync<int>("() => window.__shopDatePickerCalls")).Should().Be(3);
    }
}

using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeSignInLayoutJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    [InlineData(390, false)]
    [InlineData(390, true)]
    public async Task SignIn_MatchesDesktopFigmaAndAdaptsToMobileWithoutVendorCss(int width, bool withoutVendorCss)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", r => r.AbortAsync());
        await Page.SetViewportSizeAsync(width, width == 1440 ? 900 : 844);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").WaitForAsync(new() { Timeout = 30_000 });
        await Page.EvaluateAsync("""
            async () => {
                await document.fonts.load('500 16px "Space Grotesk"');
                await document.fonts.load('800 60px "Barlow Condensed"');
                await document.fonts.ready;
                for (const family of ['Space Grotesk', 'Barlow Condensed'])
                    if (![...document.fonts].some(f => f.family.includes(family) && f.status === 'loaded'))
                        throw new Error(family + ' must load before layout measurement');
            }
            """);
        if (withoutVendorCss)
            await Page.EvaluateAsync("""
                () => {
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam|\/css\/app\.css(?:\?|$)/i.test(link.href)) link.disabled = true;
                    document.getElementById('blazor-error-ui').style.display = 'none';
                }
                """);

        var panel = Page.Locator(".shop-auth-panel");
        var mark = panel.Locator(".shop-auth-mark");
        var signup = panel.GetByRole(AriaRole.Link, new() { Name = Strings.SignUp });
        var title = panel.GetByRole(AriaRole.Heading, new() { Level = 1 });
        var email = Page.GetByTestId("signin-email");
        var submit = Page.GetByTestId("signin-submit");
        await Assertions.Expect(Page.Locator("main.shop-auth-layout")).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator(".mud-layout, .mud-main-content, .shop-auth-brand")).ToHaveCountAsync(0);
        await Assertions.Expect(signup).ToHaveAttributeAsync("href", WebRoutes.Auth.SignUp);
        await Assertions.Expect(mark).ToHaveCSSAsync("color", "rgb(122, 122, 122)");
        await Assertions.Expect(title).ToHaveCSSAsync("font-size", "60px");
        await Assertions.Expect(title).ToHaveCSSAsync("font-weight", "800");
        await Assertions.Expect(panel).ToHaveCSSAsync("border-top-color", "rgb(224, 224, 224)");
        await Assertions.Expect(Page.Locator("#signin-instruction")).ToHaveCSSAsync("clip-path", "inset(50%)");
        await email.FillAsync("JohnDoe@gmail.com");

        if (width == 1440)
        {
            var box = (await panel.BoundingBoxAsync())!;
            box.X.Should().BeApproximately(425.1429f, 1);
            box.Y.Should().BeApproximately(32, 1);
            box.Width.Should().BeApproximately(589.7143f, 1);
            box.Height.Should().BeApproximately(836, 1);
            var logo = (await mark.BoundingBoxAsync())!;
            logo.X.Should().BeApproximately(457.1429f, 1);
            logo.Y.Should().BeApproximately(64, 1);
            logo.Width.Should().BeApproximately(52, 1);
            logo.Height.Should().BeApproximately(52, 1);
            (await title.BoundingBoxAsync())!.Y.Should().BeApproximately(148, 1);
            var field = (await Page.Locator(".shop-field-control").BoundingBoxAsync())!;
            field.X.Should().BeApproximately(457.1429f, 1);
            field.Y.Should().BeApproximately(713, 1);
            field.Width.Should().BeApproximately(525.7143f, 1);
            field.Height.Should().BeApproximately(61, 1);
            var action = (await submit.BoundingBoxAsync())!;
            action.X.Should().BeApproximately(457.1429f, 1);
            action.Y.Should().BeApproximately(790, 1);
            action.Height.Should().BeApproximately(46, 1);
        }

        await AssertNoOverflowAsync();
        await SaveAsync($"signin-layout-{width}-{withoutVendorCss}");
        await signup.FocusAsync();
        (await signup.EvaluateAsync<bool>("el => el.matches(':focus-visible') && getComputedStyle(el).outlineStyle === 'solid'"))
            .Should().BeTrue();
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(email).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(submit).ToBeFocusedAsync();
        await email.FillAsync("invalid-email");
        await submit.ClickAsync();
        await Assertions.Expect(Page.Locator("#signin-email-error")).ToContainTextAsync(Strings.Email_Invalid);
        await Assertions.Expect(email).ToHaveAttributeAsync("aria-invalid", "true");
        await SaveAsync($"signin-layout-error-{width}-{withoutVendorCss}");
        await Page.EvaluateAsync("() => document.documentElement.style.fontSize = '32px'");
        await AssertNoOverflowAsync();
        await email.ScrollIntoViewIfNeededAsync();
        await Assertions.Expect(email).ToBeVisibleAsync();
        await submit.ScrollIntoViewIfNeededAsync();
        await Assertions.Expect(submit).ToBeVisibleAsync();
        await SaveAsync($"signin-layout-large-text-{width}-{withoutVendorCss}");
        errors.Should().BeEmpty();
    }

    private async Task AssertNoOverflowAsync() =>
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"))
            .Should().BeTrue("authentication content must stay within the viewport");

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

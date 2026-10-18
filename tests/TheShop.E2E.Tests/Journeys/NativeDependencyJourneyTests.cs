using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeDependencyJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390)]
    [InlineData(1440)]
    public async Task Boot_DeepLinksAndRuntimeErrorStyles_WorkWithoutLegacyAssets(int width)
    {
        var requests = new List<string>();
        var errors = new List<string>();
        var failedAssets = new List<string>();
        Page.Request += (_, request) => requests.Add(request.Url);
        Page.PageError += (_, error) => errors.Add(error);
        Page.Response += (_, response) =>
        {
            if (response.Status >= 400 && new Uri(response.Url).Authority == new Uri(E2EEnvironment.AppBaseUrl).Authority)
                failedAssets.Add(response.Status + " " + new Uri(response.Url).AbsolutePath);
        };
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", route => route.AbortAsync());
        var releaseBoot = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.RouteAsync("**/_framework/**", async route =>
        {
            await releaseBoot.Task;
            await route.ContinueAsync();
        });
        await Page.SetViewportSizeAsync(width, 900);
        try
        {
            await Page.GotoAsync(WebRoutes.NotFound + "/dependency-probe", new() { WaitUntil = WaitUntilState.Commit });
            var boot = Page.Locator(".shop-boot-progress");
            await Assertions.Expect(boot).ToBeVisibleAsync();
            await Assertions.Expect(boot).ToHaveCSSAsync("width", "128px");
            await Assertions.Expect(boot.Locator("circle").Last).ToHaveCSSAsync("stroke", "rgb(23, 23, 23)");
            await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
            await ScreenshotAsync($"native-dependency-boot-{width}");
        }
        finally
        {
            releaseBoot.TrySetResult();
        }

        await Assertions.Expect(Page.Locator(".shop-not-found-page h1")).ToHaveTextAsync(Strings.NotFound_Heading);
        await Assertions.Expect(Page.Locator(".shop-boot-progress")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator("main")).ToHaveCountAsync(1);
        await Assertions.Expect(Page.Locator("[class*='mud-']")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();

        var errorUi = Page.Locator("#blazor-error-ui");
        await errorUi.EvaluateAsync("el => el.style.display = 'block'");
        await Assertions.Expect(errorUi).ToBeVisibleAsync();
        await Assertions.Expect(errorUi).ToHaveCSSAsync("position", "fixed");
        await Assertions.Expect(errorUi).ToHaveCSSAsync("background-color", "rgb(23, 23, 23)");
        await Assertions.Expect(errorUi).ToHaveCSSAsync("color", "rgb(255, 255, 255)");
        await ScreenshotAsync($"native-dependency-error-style-{width}");
        await errorUi.EvaluateAsync("el => el.style.removeProperty('display')");
        await Assertions.Expect(errorUi).ToBeHiddenAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = Strings.NotFound_GoHome, Exact = true }).ClickAsync();
        await Assertions.Expect(Page.Locator(".shop-home-page h1")).ToHaveTextAsync(Strings.Stay_Tuned);

        requests.Should().NotContain(url => url.Contains("MudBlazor", StringComparison.OrdinalIgnoreCase)
            || url.Contains("CodeBeam", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith("/css/app.css", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith("/TheShop.Web.styles.css", StringComparison.OrdinalIgnoreCase));
        failedAssets.Should().BeEmpty();
        errors.Should().BeEmpty();
    }

    private Task ScreenshotAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}

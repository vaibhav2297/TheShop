using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Admin browser evidence for .specs/003_manage-hero-banner/spec.md.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "003_manage-hero-banner")]
public sealed class ManageHeroBannerJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    private ManageHeroBannerPage Editor => new(Page);

    private static async Task ResetBannerAsync()
    {
        using var client = new HttpClient();
        var key = E2EEnvironment.Get("SERVICE_ROLE_KEY");
        client.DefaultRequestHeaders.Add("apikey", key);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var url = E2EEnvironment.Get("API_URL").TrimEnd('/') + "/rest/v1/hero_banners?id=eq.1";
        using var response = await client.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }

    private async Task OpenEmptyAsync()
    {
        await ResetBannerAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
    }

    private async Task<byte[]> PngAsync(int width, int height, string color = "#c83232")
    {
        var data = await Page.EvaluateAsync<string>("""args => { const c = document.createElement('canvas'); c.width = args.width; c.height = args.height; const x = c.getContext('2d'); x.fillStyle = args.color; x.fillRect(0, 0, c.width, c.height); return c.toDataURL('image/png').split(',')[1]; }""", new { width, height, color });
        return Convert.FromBase64String(data);
    }

    [Fact]
    public async Task AC1_ConsoleCardOpensEmptyAddForm()
    {
        await ResetBannerAsync();
        await Page.GotoAsync(WebRoutes.Admin.Console);
        await Page.Locator("article[data-testid='admin-module-homepage'] a").ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
        await Page.GetByTestId("manage-hero-banner").ClickAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
        await Assertions.Expect(Editor.Save).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByTestId("hero-visible")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AC5_ImageSelectionAndReplacementStayInDraft()
    {
        await OpenEmptyAsync();
        await Editor.SelectDesktopAsync(await PngAsync(1920, 1080));
        await Assertions.Expect(Editor.Desktop.Locator("img")).ToBeVisibleAsync();
        await Editor.SelectDesktopAsync(await PngAsync(1920, 1080, "#3299cc"), "replacement.png");
        await Assertions.Expect(Editor.Desktop.Locator("img")).ToBeVisibleAsync();
        await Editor.SelectMobileAsync(await PngAsync(800, 1000));
        await Assertions.Expect(Editor.Mobile.Locator("img")).ToBeVisibleAsync();
        await Page.ReloadAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
        await Assertions.Expect(Editor.Desktop.Locator("img")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC6_InvalidImageSizeAndDimensionsShowFeedback()
    {
        await OpenEmptyAsync();
        await Editor.SelectDesktopAsync(await PngAsync(800, 800), "square.png");
        await Assertions.Expect(Editor.Desktop.Locator("[data-file-dropzone]")).ToContainTextAsync(Strings.HeroBanner_DesktopDimensionsInvalid);
        await Editor.SelectDesktopAsync(new byte[5 * 1024 * 1024 + 1], "oversize.png");
        await Assertions.Expect(Editor.Desktop.Locator("[data-file-dropzone]")).ToContainTextAsync(Strings.HeroBanner_ImageTooLarge);
        await Assertions.Expect(Page.Locator("#hero-desktop-hint")).ToBeVisibleAsync();
        await Assertions.Expect(Editor.Save).ToBeEnabledAsync();
    }

    [Fact]
    public async Task AC10_SaveNavigatesHomeAndReopenPersistsContent()
    {
        await OpenEmptyAsync();
        var heading = "E2E hero " + Guid.NewGuid().ToString("N")[..8];
        await Editor.Heading.FillAsync(heading);
        await Editor.Description.FillAsync("Browser persistence");
        await Editor.SelectDesktopAsync(await PngAsync(1920, 1080));
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
        await Page.GetByTestId("manage-hero-banner").ClickAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "edit");
        await Assertions.Expect(Editor.Heading).ToHaveValueAsync(heading);
        await Assertions.Expect(Editor.Description).ToHaveValueAsync("Browser persistence");
        await Assertions.Expect(Editor.Desktop.Locator("img")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AC12_CancelDiscardsUnsavedDraft()
    {
        await OpenEmptyAsync();
        await Editor.Heading.FillAsync("Discard me");
        await Editor.SelectDesktopAsync(await PngAsync(1920, 1080));
        await Editor.Cancel.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
        await Page.GetByTestId("manage-hero-banner").ClickAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
        await Assertions.Expect(Editor.Heading).ToHaveValueAsync("");
    }

    [Fact]
    public async Task AC16_NarrowAndWideFormsRemainUsableByKeyboard()
    {
        await ResetBannerAsync();
        foreach (var width in new[] { 390, 1440 })
        {
            await Page.SetViewportSizeAsync(width, 844);
            await Editor.GotoAsync();
            await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
            (await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth"))
                .Should().BeLessThanOrEqualTo(width);
            await Assertions.Expect(Editor.Heading).ToHaveAttributeAsync("required", "");
            await Editor.Heading.FocusAsync();
            await Assertions.Expect(Editor.Heading).ToBeFocusedAsync();
            await Page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(Editor.Description).ToBeFocusedAsync();
        }
    }
}

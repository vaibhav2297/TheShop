using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Admin browser evidence for .specs/006_homepage-category/spec.md.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "006_homepage-category")]
public sealed class ManageHomepageCategoryJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    private ManageHomepageCategoryPage Editor => new(Page);
    private static readonly string[] Categories = ["Disposables", "Pod Systems", "E-Liquids"];

    private static async Task ResetSectionAsync()
    {
        using var client = new HttpClient();
        var key = E2EEnvironment.Get("SERVICE_ROLE_KEY");
        client.DefaultRequestHeaders.Add("apikey", key);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var url = E2EEnvironment.Get("API_URL").TrimEnd('/') + "/rest/v1/homepage_category_section?id=eq.1";
        using var response = await client.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }

    private async Task OpenAddAsync()
    {
        await ResetSectionAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
    }

    private async Task CreateAsync()
    {
        await OpenAddAsync();
        await Editor.ChooseAsync(Categories);
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/admin/homepage$"));
    }

    [Fact]
    public async Task AC1_AddPrefillsFieldsButNotCategories()
    {
        await OpenAddAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(Strings.HomepageCategory_DefaultTitle);
        await Assertions.Expect(Editor.ButtonLabel).ToHaveValueAsync(Strings.HomepageCategory_ViewAllCategories);
        await Assertions.Expect(Editor.ButtonLink).ToHaveValueAsync(WebRoutes.Categories);
        await Assertions.Expect(Editor.Status(Strings.HomepageCategory_Visible)).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(Editor.Selected.GetByText(Categories[0])).ToHaveCountAsync(0);
        await Assertions.Expect(Editor.Selected).ToContainTextAsync(Strings.HomepageCategory_EmptyTitle);
        await Assertions.Expect(Editor.Root).ToContainTextAsync(string.Format(Strings.HomepageCategory_SelectedCount, 0));
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(
            string.Format(Strings.HomepageCategory_AddHeading, Strings.HomepageCategory_DefaultTitle));
    }

    [Fact]
    public async Task AC6_SelectedOrderAndCategoryMediaSurviveSave()
    {
        await OpenAddAsync();
        await Editor.ChooseAsync(Categories);
        await Assertions.Expect(Editor.Rows).ToHaveCountAsync(3);
        await Editor.Rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.HomepageCategory_MoveDown }).ClickAsync();
        var order = await Editor.Rows.Locator("strong").AllInnerTextsAsync();
        order.Should().Equal(Categories[1], Categories[0], Categories[2]);
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/admin/homepage$"));
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "edit");
        (await Editor.Rows.Locator("strong").AllInnerTextsAsync()).Should().Equal(order);
        foreach (var name in order)
        {
            var row = Editor.Rows.Filter(new() { Has = Page.GetByText(name, new() { Exact = true }) });
            await Assertions.Expect(row).ToContainTextAsync(name);
            var mediaCount = await row.Locator("img[alt], .shop-image-label").CountAsync();
            mediaCount.Should().BeGreaterThan(0, $"{name} has an image or named placeholder");
        }
    }

    [Fact]
    public async Task AC8_ChangedFieldsSurviveSaveAndReopen()
    {
        await CreateAsync();
        await Editor.GotoAsync();
        var title = "Curated categories " + Guid.NewGuid().ToString("N")[..8];
        await Editor.Title.FillAsync(title);
        await Editor.ButtonLabel.FillAsync("Explore all categories");
        await Editor.ButtonLink.FillAsync("/catalog/categories");
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/admin/homepage$"));
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(title);
        await Assertions.Expect(Editor.ButtonLabel).ToHaveValueAsync("Explore all categories");
        await Assertions.Expect(Editor.ButtonLink).ToHaveValueAsync("/catalog/categories");
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(string.Format(Strings.HomepageCategory_EditHeading, title));
    }

    [Fact]
    public async Task AC9_HiddenStatusSurvivesSaveAndRemainsEditable()
    {
        await CreateAsync();
        await Editor.GotoAsync();
        await Editor.Status(Strings.HomepageCategory_Hidden).ClickAsync();
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/admin/homepage$"));
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Status(Strings.HomepageCategory_Hidden)).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(Editor.Title).ToBeEditableAsync();
        await Assertions.Expect(Editor.Save).ToBeEnabledAsync();
    }

    [Fact]
    public async Task AC10_CancelDiscardsUnsavedEdits()
    {
        await CreateAsync();
        await Editor.GotoAsync();
        await Editor.Title.FillAsync("Unsaved title");
        await Editor.ButtonLink.FillAsync("/unsaved");
        await Editor.Status(Strings.HomepageCategory_Hidden).ClickAsync();
        await Editor.Rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.HomepageCategory_MoveDown }).ClickAsync();
        await Editor.Cancel.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/admin/homepage$"));
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(Strings.HomepageCategory_DefaultTitle);
        await Assertions.Expect(Editor.ButtonLink).ToHaveValueAsync(WebRoutes.Categories);
        await Assertions.Expect(Editor.Status(Strings.HomepageCategory_Visible)).ToHaveAttributeAsync("aria-pressed", "true");
        (await Editor.Rows.Locator("strong").AllInnerTextsAsync()).Should().Equal(Categories);
    }

    [Fact]
    public async Task AC11_ExistingSectionOpensEditInsteadOfSecondAdd()
    {
        await CreateAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "edit");
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(
            string.Format(Strings.HomepageCategory_EditHeading, Strings.HomepageCategory_DefaultTitle));
        await Assertions.Expect(Editor.Rows).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task AC12_ResponsiveKeyboardControlsRemainOperable()
    {
        await OpenAddAsync();
        await Editor.ChooseAsync(Categories);
        foreach (var width in new[] { 1440, 834, 390, 320 })
        {
            await Page.SetViewportSizeAsync(width, 900);
            await Assertions.Expect(Editor.Root).ToBeVisibleAsync();
            var fits = await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth");
            fits.Should().BeTrue($"editor fits a {width}px viewport");
            await Editor.Title.FocusAsync();
            await Assertions.Expect(Editor.Title).ToBeFocusedAsync();
            await Editor.ButtonLabel.FocusAsync();
            await Assertions.Expect(Editor.ButtonLabel).ToBeFocusedAsync();
            await Editor.ButtonLink.FocusAsync();
            await Assertions.Expect(Editor.ButtonLink).ToBeFocusedAsync();
            await Assertions.Expect(Editor.Rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.HomepageCategory_MoveDown })).ToBeVisibleAsync();
            await Assertions.Expect(Editor.Status(Strings.HomepageCategory_Hidden)).ToBeVisibleAsync();
            await Assertions.Expect(Editor.Save).ToBeVisibleAsync();
            await Assertions.Expect(Editor.Cancel).ToBeVisibleAsync();
        }
        var move = Editor.Rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.HomepageCategory_MoveDown });
        await move.FocusAsync();
        await move.PressAsync("Enter");
        await Assertions.Expect(Editor.Rows.Nth(1).Locator("strong")).ToHaveTextAsync(Categories[0]);
        var hidden = Editor.Status(Strings.HomepageCategory_Hidden);
        await hidden.FocusAsync();
        await hidden.PressAsync("Enter");
        await Assertions.Expect(hidden).ToHaveAttributeAsync("aria-pressed", "true");
        await Editor.Save.FocusAsync();
        await Assertions.Expect(Editor.Save).ToBeFocusedAsync();
        await Editor.Cancel.FocusAsync();
        await Assertions.Expect(Editor.Cancel).ToBeFocusedAsync();
    }
}

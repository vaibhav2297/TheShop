using System.Net.Http.Headers;
using System.Net.Http.Json;
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

/// <summary>Admin browser evidence for .specs/008_homepage-brand/spec.md.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "008_homepage-brand")]
public sealed class ManageHomepageBrandJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    private ManageHomepageBrandPage Editor => new(Page);
    private static readonly string[] Brands = ["Elf Bar", "Geek Bar", "Vaporesso", "SMOK", "Naked 100", "Juul"];
    private static readonly Regex HomepageUrl = new(Regex.Escape(WebRoutes.Admin.ManageHomepage) + "$", RegexOptions.Compiled);

    private static HttpClient ServiceClient()
    {
        var client = new HttpClient();
        var key = E2EEnvironment.Get("SERVICE_ROLE_KEY");
        client.DefaultRequestHeaders.Add("apikey", key);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    private static async Task ResetSectionAsync()
    {
        using var client = ServiceClient();
        var url = E2EEnvironment.Get("API_URL").TrimEnd('/') + "/rest/v1/homepage_brand_section?id=eq.1";
        using var response = await client.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }

    private async Task OpenAddAsync()
    {
        await ResetSectionAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
    }

    private async Task CreateAsync(params string[] names)
    {
        await OpenAddAsync();
        await Editor.ChooseAsync(names.Length == 0 ? [Brands[0]] : names);
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(HomepageUrl);
    }

    [Fact]
    public async Task AC1_AddPrefillsFieldsButNoBrands()
    {
        await OpenAddAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(Strings.HomepageBrand_DefaultTitle);
        await Assertions.Expect(Editor.ButtonLabel).ToHaveValueAsync(Strings.HomepageBrand_ViewAllBrands);
        await Assertions.Expect(Editor.ButtonLink).ToHaveValueAsync(WebRoutes.Brands);
        await Assertions.Expect(Editor.Status(Strings.HomepageBrand_Visible)).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(Editor.Selected).ToContainTextAsync(Strings.HomepageBrand_EmptyTitle);
        await Assertions.Expect(Editor.Root).ToContainTextAsync(string.Format(Strings.HomepageBrand_SelectedCount, 0));
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(
            string.Format(Strings.HomepageBrand_AddHeading, Strings.HomepageBrand_DefaultTitle));
        await Editor.Title.FillAsync("Curated " + Guid.NewGuid().ToString("N")[..8]);
        await Assertions.Expect(Editor.Root.Locator("h1")).ToContainTextAsync("Curated");
    }

    [Fact]
    public async Task AC2_EditRestoresSavedFieldsStatusAndOrder()
    {
        await CreateAsync(Brands[1], Brands[0]);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "edit");
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(Strings.HomepageBrand_DefaultTitle);
        await Assertions.Expect(Editor.ButtonLabel).ToHaveValueAsync(Strings.HomepageBrand_ViewAllBrands);
        await Assertions.Expect(Editor.ButtonLink).ToHaveValueAsync(WebRoutes.Brands);
        await Assertions.Expect(Editor.Status(Strings.HomepageBrand_Visible)).ToHaveAttributeAsync("aria-pressed", "true");
        (await Editor.Rows.Locator("strong").AllInnerTextsAsync()).Should().Equal(Brands[1], Brands[0]);
    }

    [Fact]
    public async Task AC3_ChoosingActiveBrandsUpdatesDraftOnceWithIdentity()
    {
        await OpenAddAsync();
        await Editor.ChooseAsync(Brands[0], Brands[1]);
        await Assertions.Expect(Editor.Rows).ToHaveCountAsync(2);
        (await Editor.Rows.Locator("strong").AllInnerTextsAsync()).Should().Equal(Brands[0], Brands[1]);
        await Assertions.Expect(Editor.Root).ToContainTextAsync(string.Format(Strings.HomepageBrand_SelectedCount, 2));
        foreach (var name in Brands.Take(2))
        {
            var row = Editor.Rows.Filter(new() { Has = Page.GetByText(name, new() { Exact = true }) });
            await Assertions.Expect(row).ToContainTextAsync(name);
            (await row.Locator("img[alt], .shop-image-label").CountAsync()).Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task AC5_ReorderedBrandsPersistAfterSave()
    {
        await OpenAddAsync();
        await Editor.ChooseAsync(Brands[0], Brands[1], Brands[2]);
        await Assertions.Expect(Editor.Rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.HomepageBrand_MoveUp })).ToBeDisabledAsync();
        await Assertions.Expect(Editor.Rows.Last.GetByRole(AriaRole.Button, new() { Name = Strings.HomepageBrand_MoveDown })).ToBeDisabledAsync();
        await Editor.Rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.HomepageBrand_MoveDown }).ClickAsync();
        var order = await Editor.Rows.Locator("strong").AllInnerTextsAsync();
        order.Should().Equal(Brands[1], Brands[0], Brands[2]);
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(HomepageUrl);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "edit");
        await Assertions.Expect(Editor.Rows.Locator("strong")).ToHaveTextAsync(order);
    }

    [Fact]
    public async Task AC8_ChangedFieldsAndHiddenStatusPersistTogether()
    {
        await CreateAsync();
        await Editor.GotoAsync();
        var title = "Curated brands " + Guid.NewGuid().ToString("N")[..8];
        await Editor.Title.FillAsync(title);
        await Editor.ButtonLabel.FillAsync("Explore brands");
        await Editor.ButtonLink.FillAsync("/catalog/brands");
        await Editor.Status(Strings.HomepageBrand_Hidden).ClickAsync();
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(HomepageUrl);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(title);
        await Assertions.Expect(Editor.ButtonLabel).ToHaveValueAsync("Explore brands");
        await Assertions.Expect(Editor.ButtonLink).ToHaveValueAsync("/catalog/brands");
        await Assertions.Expect(Editor.Status(Strings.HomepageBrand_Hidden)).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(Editor.Title).ToBeEditableAsync();
        await Assertions.Expect(Editor.Rows).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task AC9_CancelDiscardsUnsavedChanges()
    {
        await CreateAsync(Brands[0], Brands[1]);
        await Editor.GotoAsync();
        await Editor.Title.FillAsync("Unsaved title");
        await Editor.ButtonLink.FillAsync("/unsaved");
        await Editor.Status(Strings.HomepageBrand_Hidden).ClickAsync();
        await Editor.Rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.HomepageBrand_MoveDown }).ClickAsync();
        await Editor.Cancel.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(HomepageUrl);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(Strings.HomepageBrand_DefaultTitle);
        await Assertions.Expect(Editor.ButtonLink).ToHaveValueAsync(WebRoutes.Brands);
        await Assertions.Expect(Editor.Status(Strings.HomepageBrand_Visible)).ToHaveAttributeAsync("aria-pressed", "true");
        (await Editor.Rows.Locator("strong").AllInnerTextsAsync()).Should().Equal(Brands[0], Brands[1]);
    }

    [Fact]
    public async Task AC12_ResponsiveKeyboardControlsRemainOperable()
    {
        await OpenAddAsync();
        await Editor.ChooseAsync(Brands[0], Brands[1]);
        foreach (var width in new[] { 1440, 834, 390, 320 })
        {
            await Page.SetViewportSizeAsync(width, 900);
            await Assertions.Expect(Editor.Root).ToBeVisibleAsync();
            (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"))
                .Should().BeTrue($"editor fits a {width}px viewport");
            await Editor.Title.FocusAsync();
            await Assertions.Expect(Editor.Title).ToBeFocusedAsync();
            await Editor.ButtonLabel.FocusAsync();
            await Assertions.Expect(Editor.ButtonLabel).ToBeFocusedAsync();
            await Editor.ButtonLink.FocusAsync();
            await Assertions.Expect(Editor.ButtonLink).ToBeFocusedAsync();
            await Assertions.Expect(Editor.Status(Strings.HomepageBrand_Hidden)).ToBeVisibleAsync();
            await Assertions.Expect(Editor.Save).ToBeVisibleAsync();
            await Assertions.Expect(Editor.Cancel).ToBeVisibleAsync();
        }
        var move = Editor.Rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.HomepageBrand_MoveDown });
        await move.FocusAsync();
        await move.PressAsync("Enter");
        await Assertions.Expect(Editor.Rows.Nth(1).Locator("strong")).ToHaveTextAsync(Brands[0]);
        var hidden = Editor.Status(Strings.HomepageBrand_Hidden);
        await hidden.FocusAsync();
        await hidden.PressAsync("Enter");
        await Assertions.Expect(hidden).ToHaveAttributeAsync("aria-pressed", "true");
        await Editor.Save.FocusAsync();
        await Assertions.Expect(Editor.Save).ToBeFocusedAsync();
        await Editor.Cancel.FocusAsync();
        await Assertions.Expect(Editor.Cancel).ToBeFocusedAsync();
    }

    [Fact]
    public async Task AC13_InactiveBrandNeverAppearsInPickerOrSearch()
    {
        await OpenAddAsync();
        var inactiveName = "e2e-inactive-brand-" + Guid.NewGuid().ToString("N")[..8];
        using (var client = ServiceClient())
        {
            var url = E2EEnvironment.Get("API_URL").TrimEnd('/') + "/rest/v1/brands";
            using var response = await client.PostAsJsonAsync(url, new { name = inactiveName, is_active = false });
            response.EnsureSuccessStatusCode();
        }
        await Editor.OpenPicker.ClickAsync();
        await Assertions.Expect(Editor.Picker).ToBeVisibleAsync();
        await Assertions.Expect(Editor.Choice(inactiveName)).ToHaveCountAsync(0);
        await Editor.Search.FillAsync(inactiveName);
        await Assertions.Expect(Editor.NoResults).ToBeVisibleAsync();
        await Assertions.Expect(Editor.Choice(inactiveName)).ToHaveCountAsync(0);
        await Editor.Search.FillAsync(Brands[0]);
        await Assertions.Expect(Editor.Choice(Brands[0])).ToBeVisibleAsync();
        await Editor.Choice(Brands[0]).ClickAsync();
        await Assertions.Expect(Editor.Picker).ToContainTextAsync(string.Format(Strings.HomepageBrand_SelectedCount, 1));
    }

    [Fact]
    public async Task AC14_DialogCancelAndCloseDiscardChoicesUntilApply()
    {
        await OpenAddAsync();
        await Editor.OpenPicker.ClickAsync();
        await Editor.Choice(Brands[0]).ClickAsync();
        await Editor.PickerCancel.ClickAsync();
        await Assertions.Expect(Editor.Rows).ToHaveCountAsync(1);
        await Assertions.Expect(Editor.Selected).ToContainTextAsync(Strings.HomepageBrand_EmptyTitle);
        await Editor.OpenPicker.ClickAsync();
        await Editor.Choice(Brands[0]).ClickAsync();
        await Page.GetByTestId("dialog-close").ClickAsync();
        await Assertions.Expect(Editor.Selected).ToContainTextAsync(Strings.HomepageBrand_EmptyTitle);
        await Editor.ChooseAsync(Brands[0]);
        await Assertions.Expect(Editor.Rows.Locator("strong")).ToHaveTextAsync([Brands[0]]);
    }

    [Fact]
    public async Task AC15_UnmatchedSearchShowsGuidanceAndClearRestoresChoices()
    {
        await OpenAddAsync();
        await Editor.OpenPicker.ClickAsync();
        await Editor.Choice(Brands[0]).ClickAsync();
        var search = "northstar-" + Guid.NewGuid().ToString("N")[..8];
        await Editor.Search.FillAsync(search);
        await Assertions.Expect(Editor.NoResults.GetByRole(AriaRole.Heading)).ToHaveTextAsync(Strings.HomepageBrand_NoResults);
        await Assertions.Expect(Editor.NoResults).ToContainTextAsync(string.Format(Strings.HomepageBrand_NoResultsHint, search));
        await Assertions.Expect(Editor.Picker).ToContainTextAsync(string.Format(Strings.HomepageBrand_SelectedCount, 1));
        (await Editor.NoResults.EvaluateAsync<string>("element => getComputedStyle(element).borderTopStyle"))
            .Should().Be("dashed");
        await Page.SetViewportSizeAsync(358, 700);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"))
            .Should().BeTrue();
        await Page.GetByRole(AriaRole.Button, new() { Name = Strings.HomepageBrand_ClearSearch }).ClickAsync();
        await Assertions.Expect(Editor.Choice(Brands[0])).ToBeVisibleAsync();
        await Assertions.Expect(Editor.NoResults).ToHaveCountAsync(0);
    }
}

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

/// <summary>Admin browser evidence for .specs/004_fresh-drops/spec.md.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "004_fresh-drops")]
public sealed class ManageFreshDropsJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    private ManageFreshDropsPage Editor => new(Page);

    private static async Task ResetDropsAsync()
    {
        using var client = new HttpClient();
        var key = E2EEnvironment.Get("SERVICE_ROLE_KEY");
        client.DefaultRequestHeaders.Add("apikey", key);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var url = E2EEnvironment.Get("API_URL").TrimEnd('/') + "/rest/v1/fresh_drops?id=eq.1";
        using var response = await client.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
    }

    private async Task OpenEmptyAsync()
    {
        await ResetDropsAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
    }

    private async Task CreateAsync(string title)
    {
        await OpenEmptyAsync();
        await Editor.Title.FillAsync(title);
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
    }

    [Fact]
    public async Task AC1_AddDefaultsAndHeadingTrackTitle()
    {
        await OpenEmptyAsync();
        await Assertions.Expect(Editor.Newest).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(Editor.AutomaticList.Locator("tbody tr")).ToHaveCountAsync(3);
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(Strings.Homepage_FreshDrops);
        await Assertions.Expect(Editor.ButtonLabel).ToHaveValueAsync(Strings.FreshDrops_ViewAllProducts);
        await Assertions.Expect(Editor.ButtonLink).ToHaveValueAsync(WebRoutes.Products);
        await Assertions.Expect(Editor.Status(Strings.FreshDrops_Visible)).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(Editor.Save).ToBeVisibleAsync();
        await Assertions.Expect(Editor.Cancel).ToBeVisibleAsync();
        await Editor.Title.FillAsync("New arrivals");
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(string.Format(Strings.FreshDrops_AddHeading, "New arrivals"));
        await Editor.Manual.ClickAsync();
        await Assertions.Expect(Editor.ManualList.Locator("tbody tr")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC2_EditLoadsSavedValuesAndHeadingTracksTitle()
    {
        var title = "Saved drops " + Guid.NewGuid().ToString("N")[..8];
        await CreateAsync(title);
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "edit");
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(title);
        await Assertions.Expect(Editor.Newest).ToHaveAttributeAsync("aria-pressed", "true");
        await Editor.Title.FillAsync("Changed draft");
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(string.Format(Strings.FreshDrops_EditHeading, "Changed draft"));
    }

    [Fact]
    public async Task AC6_PickerCancelAndModeSwitchPreserveDraftSelections()
    {
        await OpenEmptyAsync();
        await Editor.OpenManualPickerAsync();
        var first = Editor.Dialog.Locator(".shop-image-tile-surface").First;
        await first.ClickAsync();
        await Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = Strings.Cancel }).ClickAsync();
        await Assertions.Expect(Editor.Dialog).ToHaveCountAsync(0);
        await Assertions.Expect(Editor.ManualList.Locator("tbody tr")).ToHaveCountAsync(0);
        await Editor.OpenPicker.ClickAsync();
        await Editor.Dialog.Locator(".shop-image-tile-surface").First.ClickAsync();
        await Editor.ConfirmPicker.ClickAsync();
        await Assertions.Expect(Editor.ManualList.Locator("tbody tr")).ToHaveCountAsync(1);
        await Editor.Newest.ClickAsync();
        await Editor.Manual.ClickAsync();
        await Assertions.Expect(Editor.ManualList.Locator("tbody tr")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task AC7_ThirdSelectionBlocksFourthAndRowsReorder()
    {
        await OpenEmptyAsync();
        await Editor.OpenManualPickerAsync();
        var tiles = Editor.Dialog.Locator(".shop-image-tile-surface");
        for (var i = 0; i < 3; i++) await tiles.Nth(i).ClickAsync();
        await Assertions.Expect(tiles.Nth(3)).ToBeDisabledAsync();
        await Editor.ConfirmPicker.ClickAsync();
        var rows = Editor.ManualList.Locator("tbody tr");
        await Assertions.Expect(rows).ToHaveCountAsync(3);
        var firstName = await rows.First.Locator("strong").InnerTextAsync();
        await rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.FreshDrops_MoveDown }).ClickAsync();
        await Assertions.Expect(rows.Nth(1).Locator("strong")).ToHaveTextAsync(firstName);
        await rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.FreshDrops_Remove }).ClickAsync();
        await Assertions.Expect(rows).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task AC8_NoResultsKeepDraftSelection()
    {
        await OpenEmptyAsync();
        await Editor.OpenManualPickerAsync();
        await Editor.Dialog.Locator(".shop-image-tile-surface").First.ClickAsync();
        await Editor.Search.FillAsync("no-match-" + Guid.NewGuid().ToString("N"));
        await Assertions.Expect(Editor.Dialog).ToContainTextAsync(Strings.FreshDrops_NoResults);
        await Assertions.Expect(Editor.Dialog).ToContainTextAsync(string.Format(Strings.FreshDrops_SelectedCount, 1));
        await Editor.ConfirmPicker.ClickAsync();
        await Assertions.Expect(Editor.ManualList.Locator("tbody tr")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task AC9_InvalidFieldsBlockSaveAndBlankPairPersists()
    {
        await OpenEmptyAsync();
        await Editor.Title.FillAsync(" ");
        await Assertions.Expect(Editor.Root.Locator("h1")).ToHaveTextAsync(string.Format(Strings.FreshDrops_AddHeading, Strings.Homepage_FreshDrops));
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.Root).ToContainTextAsync(Strings.FreshDrops_TitleRequired);
        await Editor.Title.FillAsync("No button");
        await Editor.ButtonLink.FillAsync("");
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.Root).ToContainTextAsync(Strings.FreshDrops_ButtonPairRequired);
        await Editor.ButtonLabel.FillAsync("");
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.ButtonLabel).ToHaveValueAsync("");
        await Assertions.Expect(Editor.ButtonLink).ToHaveValueAsync("");
    }

    [Fact]
    public async Task AC10_ValidLinkPersistsAndUnsafeLinkFails()
    {
        await OpenEmptyAsync();
        await Editor.ButtonLink.FillAsync("javascript:alert(1)");
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Editor.Root).ToContainTextAsync(Strings.FreshDrops_ButtonLinkInvalid);
        await Editor.ButtonLink.FillAsync("https://example.com/products");
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.ButtonLink).ToHaveValueAsync("https://example.com/products");
    }

    [Fact]
    public async Task AC11_SaveAndReopenPreservesManualOrderAndHiddenStatus()
    {
        await OpenEmptyAsync();
        await Editor.OpenManualPickerAsync();
        var tiles = Editor.Dialog.Locator(".shop-image-tile-surface");
        await tiles.Nth(0).ClickAsync();
        await tiles.Nth(1).ClickAsync();
        await Editor.ConfirmPicker.ClickAsync();
        var rows = Editor.ManualList.Locator("tbody tr");
        await rows.First.GetByRole(AriaRole.Button, new() { Name = Strings.FreshDrops_MoveDown }).ClickAsync();
        var orderedNames = await rows.Locator("strong").AllInnerTextsAsync();
        await Editor.Status(Strings.FreshDrops_Hidden).ClickAsync();
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Manual).ToHaveAttributeAsync("aria-pressed", "true");
        (await Editor.ManualList.Locator("tbody tr strong").AllInnerTextsAsync()).Should().Equal(orderedNames);
        await Assertions.Expect(Editor.Status(Strings.FreshDrops_Hidden)).ToHaveAttributeAsync("aria-pressed", "true");
    }

    [Fact]
    public async Task AC12_CancelDiscardsUnsavedAddAndEdit()
    {
        await OpenEmptyAsync();
        await Editor.Title.FillAsync("Discard add");
        await Editor.Cancel.ClickAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
        await Editor.Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
        await Editor.GotoAsync();
        await Editor.Title.FillAsync("Discard edit");
        await Editor.Cancel.ClickAsync();
        await Editor.GotoAsync();
        await Assertions.Expect(Editor.Title).ToHaveValueAsync(Strings.Homepage_FreshDrops);
    }

    [Fact]
    public async Task AC15_DesktopTabletPhoneKeyboardAndOverflow()
    {
        await ResetDropsAsync();
        foreach (var width in new[] { 390, 768, 1440 })
        {
            await Page.SetViewportSizeAsync(width, 844);
            await Editor.GotoAsync();
            await Assertions.Expect(Editor.Root).ToHaveAttributeAsync("data-state", "add");
            (await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth"))
                .Should().BeLessThanOrEqualTo(width);
            await Editor.Title.FocusAsync();
            await Assertions.Expect(Editor.Title).ToBeFocusedAsync();
            await Editor.Manual.FocusAsync();
            await Page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(Editor.Manual).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(Editor.OpenPicker).ToBeVisibleAsync();
        }
    }
}

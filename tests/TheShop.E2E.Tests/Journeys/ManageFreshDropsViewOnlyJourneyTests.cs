using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>View-only browser evidence for .specs/004_fresh-drops/spec.md AC-4.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "004_fresh-drops")]
public sealed class ManageFreshDropsViewOnlyJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.SupportEmail)
{
    [Fact]
    public async Task AC4_ViewOnlyStaffCannotAddOrEdit()
    {
        using var client = new HttpClient();
        var key = E2EEnvironment.Get("SERVICE_ROLE_KEY");
        client.DefaultRequestHeaders.Add("apikey", key);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var url = E2EEnvironment.Get("API_URL").TrimEnd('/') + "/rest/v1/fresh_drops";
        using (var delete = await client.DeleteAsync(url + "?id=eq.1", TestContext.Current.CancellationToken)) delete.EnsureSuccessStatusCode();

        var editor = new ManageFreshDropsPage(Page);
        await editor.GotoAsync();
        await Assertions.Expect(editor.Root).ToHaveAttributeAsync("data-state", "add");
        await Assertions.Expect(editor.Root).ToContainTextAsync(Strings.FreshDrops_Absent);
        await Assertions.Expect(editor.Save).ToHaveCountAsync(0);
        await Assertions.Expect(editor.Title).ToHaveCountAsync(0);

        using (var insert = await client.PostAsJsonAsync(url, new
        {
            id = 1,
            title = "Read only",
            button_label = "View All Products",
            button_link = "/products",
            selection_mode = "newest",
            manual_product_ids = Array.Empty<Guid>(),
            is_visible = false
        }, TestContext.Current.CancellationToken)) insert.EnsureSuccessStatusCode();
        await editor.GotoAsync();
        await Assertions.Expect(editor.Root).ToHaveAttributeAsync("data-state", "edit");
        await Assertions.Expect(editor.Title).ToHaveValueAsync("Read only");
        await Assertions.Expect(editor.Save).ToHaveCountAsync(0);
        await Assertions.Expect(editor.Newest).ToHaveCountAsync(0);
    }
}

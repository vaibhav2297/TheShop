using FluentAssertions;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Admin Console dashboard journeys (.specs/admin-console/spec.md §6).</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "admin-console")]
public sealed class AdminConsoleJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    [Fact]
    [Trait("Suite", "Smoke")]
    public async Task AC1_Admin_sees_one_overview_card_per_module_with_a_count()
    {
        var console = new AdminConsolePage(Page);
        await console.GotoAsync();

        await console.ModuleCard(Strings.AdminConsole_Module_Brands).WaitForAsync(new() { Timeout = 15_000 });
        await console.ModuleCard(Strings.AdminConsole_Module_Products).WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC2_Selecting_a_module_card_navigates_to_its_management_page()
    {
        var console = new AdminConsolePage(Page);
        await console.GotoAsync();

        // The link's accessible name is its aria-label ("{Module}: {N} records"), which takes
        // priority over the "Manage {Module}" visible text — substring match, count is dynamic.
        await Page.GetByRole(Microsoft.Playwright.AriaRole.Link,
            new() { Name = $"{Strings.AdminConsole_Module_Brands}:" }).ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/admin/brands"), new() { Timeout = 15_000 });
    }
}

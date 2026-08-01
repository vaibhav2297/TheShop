using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>AC-1: a Customer cannot see or reach any admin screen, including by direct link.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "rbac-hardening")]
[Trait("Suite", "Smoke")]
public sealed class RbacCustomerDeniedTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.CustomerEmail)
{
    [Fact]
    public async Task AC1_Customer_following_a_direct_link_to_admin_sees_no_dashboard_content()
    {
        // AdminConsole is [Authorize(Policy = PolicyNames.AdminDashboard)] — a Customer lacking
        // dashboard.view never reaches the component's own code at all; the router's
        // AuthorizeRouteView.NotAuthorized renders the shared AccessDeniedView instead.
        var console = new AdminConsolePage(Page);
        await console.GotoAsync();

        await Page.GetByText(Strings.AccessDenied_Title).WaitForAsync(new() { Timeout = 15_000 });
        (await console.ModuleCard(Strings.AdminConsole_Module_Brands).CountAsync()).Should().Be(0);
    }
}

/// <summary>AC-6: a seeded Support account has exactly Support's access — view-only everywhere.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "rbac-hardening")]
[Trait("Suite", "Smoke")]
public sealed class RbacSupportViewOnlyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.SupportEmail)
{
    [Fact]
    public async Task AC6_Support_reaches_admin_console_and_sees_manage_brands_read_only()
    {
        var console = new AdminConsolePage(Page);
        await console.GotoAsync();
        // Support was granted *.view on every module (migration 0017) — the console renders
        // a card for each one, unlike Customer who sees none.
        await console.ModuleCard(Strings.AdminConsole_Module_Brands).WaitForAsync(new() { Timeout = 15_000 });

        var manageBrands = new ManageBrandsPage(Page);
        await manageBrands.GotoAsync();

        // brands.view only: the list is reachable, but no "Add Brand" create control is shown.
        (await Page.GetByRole(AriaRole.Link, new() { Name = Strings.AddBrand_Heading }).CountAsync())
            .Should().Be(0);
    }
}

using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// AC-18 (.specs/manage-product/spec.md §6, RULE-1): a user holding no product permission is
/// refused the manage-products listing, including by direct link — no row, filter, or action
/// content reaches the page. Every other permission boundary in this feature (view-only staff
/// seeing no create/edit/delete controls, a real RPC rejecting an unauthorized call) is already
/// proven at the component and Infrastructure-integration tier — see
/// .specs/manage-product/e2e-manifest.json.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "manage-product")]
public sealed class ManageProductsAccessDeniedJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.CustomerEmail)
{
    [Fact]
    public async Task AC18_A_user_without_products_view_cannot_reach_the_list_by_direct_link()
    {
        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();

        await Page.GetByText(Strings.AccessDenied_Title).WaitForAsync(new() { Timeout = 15_000 });
        (await Page.Locator("tbody tr").CountAsync()).Should().Be(0,
            "products.view gates the page; a user holding no product permission must see no listing content at all");
    }
}

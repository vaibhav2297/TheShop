using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// AC-33 (.specs/create-product/spec.md §6): a user without <c>products.view</c> cannot reach the
/// manage-products page at all, including by direct link. The Customer persona holds no product
/// permission of any kind.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "create-product")]
public sealed class CreateProductAccessDeniedJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.CustomerEmail)
{
    [Fact]
    public async Task AC33_A_user_without_the_view_permission_cannot_reach_the_list()
    {
        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();

        await Page.GetByText(Strings.AccessDenied_Title).WaitForAsync(new() { Timeout = 15_000 });
        (await Page.Locator("tbody tr").CountAsync()).Should().Be(0,
            "the page is gated on products.view, so none of its list content is reachable");
    }
}

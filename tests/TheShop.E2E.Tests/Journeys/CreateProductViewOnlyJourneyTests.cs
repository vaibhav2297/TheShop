using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// AC-3 (.specs/create-product/spec.md §6): a staff member holding only <c>products.view</c>
/// gets a read-only product list — no add or edit affordance — and is refused the create
/// capability by direct link. Support is seeded with every module's <c>*.view</c> permission and
/// nothing else (migration 0017), which is exactly this AC's persona.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "create-product")]
public sealed class CreateProductViewOnlyJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.SupportEmail)
{
    [Fact]
    public async Task AC3_View_only_staff_see_no_add_or_edit_controls_and_direct_links_are_denied()
    {
        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();

        (await manageProducts.AddProductLink.CountAsync()).Should().Be(0,
            "products.create is what renders the Add Product control");

        var anyRow = Page.Locator("tbody tr").First;
        await anyRow.WaitForAsync(new() { Timeout = 15_000 });
        (await anyRow.GetByRole(AriaRole.Link).CountAsync()).Should().Be(0,
            "products.edit is what renders a row's edit link");

        await new AddProductPage(Page).GotoAsync();
        await Page.GetByText(Strings.AccessDenied_Title).WaitForAsync(new() { Timeout = 15_000 });
    }
}

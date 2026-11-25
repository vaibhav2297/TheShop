using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// AC-14 (.specs/shop-image/spec.md §6, RULE-4): image presets grant no access. A signed-in customer
/// following a direct link to an image-bearing admin list still sees the existing access-denied view,
/// and a guest is still sent to sign in — in both cases no restricted image reaches the page.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "shop-image")]
public sealed class ShopImageAccessDeniedJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.CustomerEmail)
{
    [Fact]
    public async Task AC14_Restricted_image_lists_keep_their_access_denied_and_sign_in_experiences()
    {
        var brands = new ManageBrandsPage(Page);
        await brands.GotoAsync();
        await Page.GetByText(Strings.AccessDenied_Title).WaitForAsync(new() { Timeout = 15_000 });
        (await Page.GetByRole(AriaRole.Img, new() { Name = "Elf Bar", Exact = true }).CountAsync()).Should().Be(0,
            "brands.view gates the page; a customer must see no brand logo");

        await using var guestContext = await ShopBrowser.NewContextAsync(Playwright.Browser);
        var guestPage = await guestContext.NewPageAsync();
        await guestPage.GotoAsync(WebRoutes.Admin.ManageCategories); // redirect target has no admin layout to wait on
        await guestPage.WaitForURLAsync(url => new Uri(url).AbsolutePath == WebRoutes.Auth.SignIn,
            new() { Timeout = 15_000 });
        (await guestPage.GetByRole(AriaRole.Img, new() { Name = "Disposables", Exact = true }).CountAsync()).Should().Be(0,
            "a guest is sent to sign in before any category image renders");
    }
}

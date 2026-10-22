using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Customer visibility and management denial for .specs/002_variant-filters/spec.md.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "002_variant-filters")]
public sealed class VariantFilterCustomerJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.CustomerEmail)
{
    [Fact]
    public async Task AC15_UnpublishedChoicesStayHiddenAndCustomerCannotOpenManagement()
    {
        await using var data = await VariantFilterData.CreateAsync(); var catalogue = new VariantListingPage(Page);
        await catalogue.OpenAsync(data); await catalogue.ExpandAsync(data.Colour);
        await Assertions.Expect(catalogue.Choice(data.ColourKey, "Secret")).ToHaveCountAsync(0);
        await Assertions.Expect(catalogue.Product(data.Name("Hidden"))).ToHaveCountAsync(0);
        await using var anonymous = await ShopBrowser.NewContextAsync(Playwright.Browser);
        var guest = new VariantListingPage(await anonymous.NewPageAsync()); await guest.OpenAsync(data);
        await Assertions.Expect(guest.Choice(data.ColourKey, "Secret")).ToHaveCountAsync(0);
        await Assertions.Expect(guest.Product(data.Name("Hidden"))).ToHaveCountAsync(0);
        var management = new VariantListingPage(Page, true); await management.GotoAsync();
        await Assertions.Expect(Page.GetByText(Strings.AccessDenied_Title, new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(management.Root.Locator("tbody tr")).ToHaveCountAsync(0);
        await Assertions.Expect(management.Group(data.ColourKey)).ToHaveCountAsync(0);
    }
}

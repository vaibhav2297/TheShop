using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.Web.Common;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Signed-in/customer parity for .specs/001_product-detail/spec.md AC-2.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "001_product-detail")]
public sealed class ProductDetailCustomerJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.CustomerEmail)
{
    [Fact]
    public async Task AC2_GuestsAndCustomersHaveIdenticalPublicViewingAndSelection()
    {
        await using var data = await ProductDetailData.CreateAsync();
        var customer = new ProductDetailPage(Page, data.Id); await customer.OpenAsync(390, 844);
        await customer.ChooseAsync(data.Values[0], data.Values[2]); await customer.PositionAsync(2);
        await Assertions.Expect(customer.Price.Locator(".shop-product-detail-price")).ToHaveTextAsync(CurrencyFormatter.Format(20));
        await using var guestContext = await ShopBrowser.NewContextAsync(Playwright.Browser);
        var guestPage = await guestContext.NewPageAsync(); var guest = new ProductDetailPage(guestPage, data.Id); await guest.OpenAsync(390, 844);
        await guest.ChooseAsync(data.Values[0], data.Values[2]); await guest.PositionAsync(2);
        await Assertions.Expect(guest.Price.Locator(".shop-product-detail-price")).ToHaveTextAsync(CurrencyFormatter.Format(20));
    }
}

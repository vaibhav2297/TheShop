using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Anonymous routing evidence for .specs/004_fresh-drops/spec.md AC-3.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "004_fresh-drops")]
public sealed class ManageFreshDropsAnonymousJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task AC3_GuestRedirectsAndCustomerSeesAccessDenied()
    {
        var editor = new ManageFreshDropsPage(Page);
        await editor.GotoAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/sign-in"));
        await Assertions.Expect(editor.Root).ToHaveCountAsync(0);

        var state = await AuthStateFactory.EnsureSignedInAsync(Playwright.Browser, AuthStateFactory.CustomerEmail);
        var context = await ShopBrowser.NewContextAsync(Playwright.Browser, state);
        try
        {
            var customerPage = await context.NewPageAsync();
            var customerEditor = new ManageFreshDropsPage(customerPage);
            await customerEditor.GotoAsync();
            await Assertions.Expect(customerEditor.Root).ToHaveCountAsync(0);
            await Assertions.Expect(customerPage.GetByText(TheShop.Web.Resources.Strings.AccessDenied_Title)).ToBeVisibleAsync();
        }
        finally { await context.DisposeAsync(); }
    }
}

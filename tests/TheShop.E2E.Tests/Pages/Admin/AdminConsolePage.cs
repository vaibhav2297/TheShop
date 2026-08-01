using Microsoft.Playwright;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Page object for the Admin Console dashboard (/admin).</summary>
public sealed class AdminConsolePage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.Console;

    /// <summary>
    /// Locator for a module's overview card by its visible label (e.g. "Brands"). Targets the
    /// heading role rather than plain text — the storefront nav also has a "Brands" link, and a
    /// plain GetByText match would collide with it.
    /// </summary>
    public ILocator ModuleCard(string moduleLabel) =>
        Page.GetByRole(AriaRole.Heading, new() { Name = moduleLabel, Exact = true });
}

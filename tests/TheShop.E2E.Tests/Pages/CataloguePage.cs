using Microsoft.Playwright;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages;

/// <summary>Page object for the storefront product catalogue (/products).</summary>
public sealed class CataloguePage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Products;

    /// <summary>Locator for a product card's name text, exact match.</summary>
    public ILocator ProductName(string name) => Page.GetByText(name, new() { Exact = true });

    /// <summary>
    /// Expands a collapsed filter group panel by its heading (e.g. Strings.Filter_Brand).
    /// </summary>
    public async Task ExpandFilterGroupAsync(string groupLabel)
    {
        var trigger = Page.Locator(".shop-expander-trigger").Filter(new() { HasText = groupLabel });
        if (await trigger.GetAttributeAsync("aria-expanded") == "false")
            await trigger.ClickAsync();
    }

    /// <summary>Locator for a filter option's checkbox by its visible label — the group must already be expanded.</summary>
    public ILocator FilterOption(string optionLabel) =>
        Page.GetByRole(AriaRole.Checkbox, new() { Name = optionLabel });

    /// <summary>Toggles a filter option's checkbox by its visible label (e.g. a brand name) — the group must already be expanded.</summary>
    public async Task ToggleFilterOptionAsync(string optionLabel) =>
        await FilterOption(optionLabel).ClickAsync();
}

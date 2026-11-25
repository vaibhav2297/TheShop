using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TheShop.Web.Resources;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages;

/// <summary>Listing navigation, dynamic filter controls, and native browser observations for .specs/002_variant-filters/spec.md.</summary>
public sealed class VariantListingPage(IPage page, bool admin = false) : ShopPage(page)
{
    protected override string Route => admin ? WebRoutes.Admin.ManageProducts : WebRoutes.Products;
    public ILocator Root => Page.Locator(admin ? ".shop-products-page" : ".shop-catalogue-page");
    public ILocator Sidebar => Root.Locator("aside");
    public ILocator Drawer => Page.GetByRole(AriaRole.Dialog);
    public ILocator Error => Page.GetByTestId("listing-load-error");
    public ILocator Retry => Page.GetByTestId("listing-retry");
    public ILocator ResultNames => admin ? Root.Locator("tbody tr .shop-products-name > div:last-child") : Root.Locator(".shop-product-card-title");

    internal async Task OpenAsync(VariantFilterData data, params (string Key, string Value)[] parameters)
    {
        await GotoAsync(Url(data, parameters));
        await ReadyAsync();
    }

    internal string Url(VariantFilterData data, params (string Key, string Value)[] parameters) => Route + "?" +
        string.Join("&", new[] { ("brand", data.Brand.ToString()), ("category", data.Category.ToString()), ("sort", "name-asc") }
            .Where(pair => !parameters.Any(p => p.Key == pair.Item1))
            .Concat(parameters).Select(pair => Uri.EscapeDataString(pair.Item1) + "=" + Uri.EscapeDataString(pair.Item2)));

    public async Task ReadyAsync()
    {
        await Assertions.Expect(Root.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(Root.Locator("[aria-busy=true]")).ToHaveCountAsync(0, new() { Timeout = 30_000 });
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
    }

    public ILocator Group(string key, bool drawer = false) => (drawer ? Drawer : Sidebar)
        .GetByTestId("filter-group").And(Page.Locator($"[data-filter-key='{key}']"));

    public ILocator Choice(string key, string label, bool drawer = false) =>
        Group(key, drawer).GetByRole(AriaRole.Checkbox, new() { Name = label, Exact = true });

    public async Task ExpandAsync(string label, bool drawer = false)
    {
        var trigger = (drawer ? Drawer : Sidebar).GetByRole(AriaRole.Button, new() { Name = label, Exact = true });
        await Assertions.Expect(trigger).ToBeVisibleAsync();
        if (await trigger.GetAttributeAsync("aria-expanded") == "false") await trigger.ClickAsync();
    }

    public async Task SelectAsync(string groupKey, string groupLabel, string value, bool selected = true, bool drawer = false)
    {
        await ExpandAsync(groupLabel, drawer);
        var choice = Choice(groupKey, value, drawer);
        await choice.SetCheckedAsync(selected);
        await Assertions.Expect(choice).ToBeCheckedAsync(new() { Checked = selected });
        await ReadyAsync();
    }

    public async Task PageAsync(int pageNumber)
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = string.Format(Strings.Pagination_Page, pageNumber), Exact = true }).ClickAsync();
        await Assertions.Expect(Root.Locator("[aria-current=page]")).ToHaveTextAsync(pageNumber.ToString());
        await ReadyAsync();
    }

    public async Task ClearAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = Strings.Filter_ClearAll, Exact = true }).First.ClickAsync();
        await ReadyAsync();
    }

    public async Task OpenDrawerAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = Strings.Products_Filters, Exact = true }).ClickAsync();
        await Assertions.Expect(Drawer).ToBeVisibleAsync();
    }

    public async Task CloseDrawerAsync()
    {
        await Drawer.GetByTestId("drawer-close").ClickAsync();
        await Assertions.Expect(Drawer).Not.ToBeVisibleAsync();
    }

    public async Task ReloadAsync()
    {
        await Page.ReloadAsync();
        await ReadyAsync();
    }

    public async Task OpenLinkAsync(string url) { await GotoAsync(url); await ReadyAsync(); }

    public async Task BackAsync() { await Page.GoBackAsync(); await ReadyAsync(); }
    public async Task ForwardAsync() { await Page.GoForwardAsync(); await ReadyAsync(); }

    public ILocator Product(string name) => admin
        ? Root.GetByRole(AriaRole.Row).Filter(new() { HasText = name })
        : Root.Locator("article").Filter(new() { HasText = name });

    public async Task ExpectProductsAsync(params string[] names)
    {
        await Assertions.Expect(admin ? Root.Locator("tbody tr") : Root.Locator("article.shop-product-card"))
            .ToHaveCountAsync(names.Length, new() { Timeout = 15_000 });
        foreach (var name in names) await Assertions.Expect(Product(name)).ToHaveCountAsync(1);
    }

    public async Task AssertFocusVisibleAsync(ILocator control)
    {
        await Assertions.Expect(control).ToBeFocusedAsync();
        var focusVisible = await control.EvaluateAsync<bool>("el => { const nodes=[el,...el.parentElement.querySelectorAll('.shop-checkbox-icon')]; return el.matches(':focus-visible') && nodes.some(n=>{const s=getComputedStyle(n); const r=n.getBoundingClientRect(); return r.width>0 && r.height>0 && s.display!=='none' && ((s.outlineStyle!=='none' && parseFloat(s.outlineWidth)>0) || s.boxShadow!=='none');}); }");
        Xunit.Assert.True(focusVisible, "Keyboard focus must have a visible outline or shadow.");
    }
}

using Microsoft.Playwright;

namespace TheShop.E2E.Tests.Pages;

/// <summary>Base page object: navigation with WASM-boot wait, shared snackbar helpers.</summary>
public abstract class ShopPage(IPage page)
{
    protected IPage Page { get; } = page;

    /// <summary>Route of this page, from <c>TheShop.Web.Common.Routes</c> — never a literal.</summary>
    protected abstract string Route { get; }

    public async Task GotoAsync()
    {
        await Page.GotoAsync(Route);
        await Page.Locator(".mud-layout").WaitForAsync(new() { Timeout = 30_000 });
    }

    /// <summary>MudBlazor snackbar text, for toast assertions.</summary>
    public ILocator Snackbar => Page.Locator(".mud-snackbar");
}

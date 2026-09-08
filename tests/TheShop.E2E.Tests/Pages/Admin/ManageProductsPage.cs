using Microsoft.Playwright;
using TheShop.Web.Resources;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Page object for the Manage Products admin list (/admin/products).</summary>
public sealed class ManageProductsPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.ManageProducts;

    /// <summary>Locator for the "Add Product" link — absent for staff without products.create.</summary>
    public ILocator AddProductLink =>
        Page.GetByRole(AriaRole.Link, new() { Name = Strings.AddProduct_Heading });

    /// <summary>Navigates to the add-product form via the "Add Product" link.</summary>
    public async Task GotoAddProductAsync() => await AddProductLink.ClickAsync();

    /// <summary>Locator for a product row's edit link — absent for staff without products.edit.</summary>
    public ILocator EditLink(string productName) =>
        Page.GetByRole(AriaRole.Link, new() { Name = string.Format(Strings.ManageProducts_EditAria, productName) });

    /// <summary>Navigates to a specific product's edit form via its row's edit link.</summary>
    public async Task GotoEditProductAsync(string productName) => await EditLink(productName).ClickAsync();

    /// <summary>
    /// The table row owning a product, matched on an exact name cell rather than substring text —
    /// journeys generate unique names, but a substring match would still be fragile against the
    /// migration-0002 seeded rows sharing common words.
    /// </summary>
    public ILocator Row(string productName) =>
        Page.Locator("tr").Filter(new() { Has = Page.GetByText(productName, new() { Exact = true }) });

    /// <summary>A product row's status chip text (Active/Inactive), read-only from the list.</summary>
    public ILocator StatusChip(string productName, bool active) =>
        Row(productName).GetByText(
            active ? Strings.ManageProducts_StatusActive : Strings.ManageProducts_StatusInactive,
            new() { Exact = true });

    /// <summary>Locator for the next-page pagination button, by its accessible page number.</summary>
    public ILocator PageButton(int pageNumber) =>
        Page.GetByRole(AriaRole.Button, new() { Name = pageNumber.ToString() });

    /// <summary>Moves to a given page via the pagination control.</summary>
    public Task GotoPageAsync(int pageNumber) => PageButton(pageNumber).ClickAsync();

    /// <summary>
    /// Reads a listed product's id straight off its row's edit link (<c>/admin/products/{id}/edit</c>)
    /// — the list offers no other way to learn a product's identifier.
    /// </summary>
    public async Task<Guid> GetProductIdAsync(string productName)
    {
        var href = await EditLink(productName).GetAttributeAsync("href");
        var match = System.Text.RegularExpressions.Regex.Match(
            href ?? string.Empty, @"/admin/products/([0-9a-fA-F-]{36})/edit");
        return Guid.Parse(match.Groups[1].Value);
    }
}

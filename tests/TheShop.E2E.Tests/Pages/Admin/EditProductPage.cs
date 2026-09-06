using Microsoft.Playwright;
using TheShop.Web.Resources;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>
/// Page object for a product's edit address (/admin/products/{id}/edit). Not route-parameterized
/// via <see cref="ShopPage"/> since navigation here always originates from a Manage Products row
/// link or a directly-constructed id (AC-34's stale/renamed link cases).
/// </summary>
public sealed class EditProductPage(IPage page)
{
    private IPage Page { get; } = page;

    public ProductFormPage Form { get; } = new(page);

    /// <summary>Navigates straight to a product id's edit address, bypassing the list.</summary>
    public async Task GotoAsync(Guid productId)
    {
        await Page.GotoAsync(WebRoutes.Admin.EditProduct(productId));
        await Page.Locator(".mud-layout").WaitForAsync(new() { Timeout = 30_000 });
    }

    public ILocator NotFoundTitle => Page.GetByText(Strings.EditProduct_NotFoundTitle, new() { Exact = true });
    public ILocator BackToListLink => Page.GetByRole(AriaRole.Link, new() { Name = Strings.EditProduct_BackToList });
}

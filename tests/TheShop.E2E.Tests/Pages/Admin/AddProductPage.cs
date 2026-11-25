using Microsoft.Playwright;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>
/// Page object for the Add Product form's own address (/admin/products/new) — field interaction
/// itself lives on <see cref="ProductFormPage"/>, shared with Edit.
/// </summary>
public sealed class AddProductPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.AddProduct;

    public ProductFormPage Form { get; } = new(page);
}

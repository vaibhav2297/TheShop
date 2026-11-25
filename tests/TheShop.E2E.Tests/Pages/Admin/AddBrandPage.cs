using Microsoft.Playwright;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Page object for the Add Brand form (/admin/brands/new).</summary>
public sealed class AddBrandPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.AddBrand;

    /// <summary>Fills the brand name and saves, using only the required field (.specs/add-brand/spec.md AC-6).</summary>
    public async Task CreateAsync(string name)
    {
        await Page.GetByTestId("add-brand-name").FillAsync(name);
        await Page.GetByTestId("add-brand-save").ClickAsync();
    }
}

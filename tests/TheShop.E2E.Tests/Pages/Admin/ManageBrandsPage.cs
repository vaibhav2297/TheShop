using Microsoft.Playwright;
using TheShop.Web.Resources;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Page object for the Manage Brands admin list (/admin/brands).</summary>
public sealed class ManageBrandsPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.ManageBrands;

    /// <summary>Navigates to the add-brand form via the "Add Brand" link.</summary>
    public async Task GotoAddBrandAsync() =>
        await Page.GetByRole(AriaRole.Link, new() { Name = Strings.AddBrand_Heading }).ClickAsync();

    /// <summary>Navigates to a specific brand's edit form via its row's edit link.</summary>
    public async Task GotoEditBrandAsync(string brandName) =>
        await Page.GetByRole(AriaRole.Link, new() { Name = string.Format(Strings.ManageBrands_EditAria, brandName) }).ClickAsync();

    /// <summary>Locator for a brand's name text in the list, exact match.</summary>
    public ILocator BrandRow(string brandName) => Page.GetByText(brandName, new() { Exact = true });

    /// <summary>Brand-logo treatment frames rendered by the list.</summary>
    public ILocator LogoFrames => Page.Locator(".shop-image-brand-logo");

    /// <summary>Searches the brand list.</summary>
    public async Task SearchAsync(string term) =>
        await Page.GetByPlaceholder(Strings.ManageBrands_SearchPlaceholder).FillAsync(term);
}

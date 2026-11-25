using Microsoft.Playwright;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Page object for the Add Category form (/admin/categories/new).</summary>
public sealed class AddCategoryPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.AddCategory;

    /// <summary>Fills the category name and saves, using only the required field (.specs/manage-categories/spec.md AC-7).</summary>
    public async Task CreateAsync(string name)
    {
        await Page.GetByTestId("add-category-name").FillAsync(name);
        await Page.GetByTestId("add-category-save").ClickAsync();
    }
}

using Microsoft.Playwright;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>
/// Page object for the Edit Category form (/admin/categories/{id}/edit). Not route-parameterized
/// via <see cref="ShopPage"/> since navigation here always originates from a Manage Categories row
/// link.
/// </summary>
public sealed class EditCategoryPage(IPage page)
{
    private IPage Page { get; } = page;

    /// <summary>Replaces the category name and saves.</summary>
    public async Task RenameAsync(string newName)
    {
        await Page.GetByTestId("edit-category-name").FillAsync(newName);
        await Page.GetByTestId("edit-category-save").ClickAsync();
    }
}

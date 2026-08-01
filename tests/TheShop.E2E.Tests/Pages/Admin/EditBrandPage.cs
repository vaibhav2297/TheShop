using Microsoft.Playwright;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>
/// Page object for the Edit Brand form (/admin/brands/{id}/edit). Not route-parameterized via
/// <see cref="ShopPage"/> since navigation here always originates from a Manage Brands row link.
/// </summary>
public sealed class EditBrandPage(IPage page)
{
    private IPage Page { get; } = page;

    /// <summary>Replaces the brand name and saves.</summary>
    public async Task RenameAsync(string newName)
    {
        await Page.GetByTestId("edit-brand-name").FillAsync(newName);
        await Page.GetByTestId("edit-brand-save").ClickAsync();
    }
}

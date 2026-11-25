using Microsoft.Playwright;
using TheShop.Web.Resources;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Browser controls for .specs/006_homepage-category/spec.md.</summary>
public sealed class ManageHomepageCategoryPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.ManageHomepageCategory;

    public ILocator Root => Page.GetByTestId("homepage-category-editor");
    public ILocator Title => Page.GetByTestId("homepage-category-title");
    public ILocator ButtonLabel => Page.GetByTestId("homepage-category-button-label");
    public ILocator ButtonLink => Page.GetByTestId("homepage-category-button-link");
    public ILocator Selected => Page.GetByTestId("homepage-category-selected");
    public ILocator Rows => Selected.Locator("tbody tr");
    public ILocator OpenPicker => Page.GetByTestId("homepage-category-open-picker");
    public ILocator Picker => Page.GetByTestId("homepage-category-picker");
    public ILocator ConfirmPicker => Page.GetByTestId("homepage-category-confirm-picker");
    public ILocator Save => Page.GetByTestId("homepage-category-save");
    public ILocator Cancel => Root.GetByRole(AriaRole.Link, new() { Name = Strings.Cancel });
    public ILocator Status(string name) => Root.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
    public ILocator Choice(string name) => Picker.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    public async Task ChooseAsync(params string[] names)
    {
        await OpenPicker.ClickAsync();
        await Picker.WaitForAsync();
        foreach (var name in names) await Choice(name).ClickAsync();
        await ConfirmPicker.ClickAsync();
    }
}

using Microsoft.Playwright;
using TheShop.Web.Resources;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Browser controls for .specs/008_homepage-brand/spec.md.</summary>
public sealed class ManageHomepageBrandPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.ManageHomepageBrand;

    public ILocator Root => Page.GetByTestId("homepage-brand-editor");
    public ILocator Title => Page.GetByTestId("homepage-brand-title");
    public ILocator ButtonLabel => Page.GetByTestId("homepage-brand-button-label");
    public ILocator ButtonLink => Page.GetByTestId("homepage-brand-button-link");
    public ILocator Selected => Page.GetByTestId("homepage-brand-selected");
    public ILocator Rows => Selected.Locator("tbody tr");
    public ILocator OpenPicker => Page.GetByTestId("homepage-brand-open-picker");
    public ILocator Picker => Page.GetByTestId("homepage-brand-picker");
    public ILocator Search => Page.GetByTestId("homepage-brand-search");
    public ILocator NoResults => Page.GetByTestId("homepage-brand-no-results");
    public ILocator ConfirmPicker => Page.GetByTestId("homepage-brand-confirm-picker");
    public ILocator Save => Page.GetByTestId("homepage-brand-save");
    public ILocator Cancel => Root.GetByRole(AriaRole.Link, new() { Name = Strings.Cancel });
    public ILocator PickerCancel => Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = Strings.Cancel });
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

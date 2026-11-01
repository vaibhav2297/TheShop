using Microsoft.Playwright;
using TheShop.Web.Resources;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Browser controls for .specs/004_fresh-drops/spec.md.</summary>
public sealed class ManageFreshDropsPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.ManageFreshDrops;

    public ILocator Root => Page.GetByTestId("fresh-drops-editor");
    public ILocator Title => Page.GetByTestId("fresh-drops-title");
    public ILocator ButtonLabel => Page.GetByTestId("fresh-drops-button-label");
    public ILocator ButtonLink => Page.GetByTestId("fresh-drops-button-link");
    public ILocator Newest => Page.GetByTestId("fresh-drops-newest");
    public ILocator Manual => Page.GetByTestId("fresh-drops-manual");
    public ILocator AutomaticList => Page.GetByTestId("fresh-drops-automatic-list");
    public ILocator ManualList => Page.GetByTestId("fresh-drops-manual-list");
    public ILocator Save => Page.GetByTestId("fresh-drops-save");
    public ILocator Cancel => Page.GetByRole(AriaRole.Link, new() { Name = Strings.Cancel });
    public ILocator OpenPicker => Page.GetByTestId("fresh-drops-open-picker");
    public ILocator Dialog => Page.GetByTestId("fresh-drops-product-dialog");
    public ILocator Search => Page.GetByTestId("fresh-drops-search");
    public ILocator ConfirmPicker => Page.GetByTestId("fresh-drops-confirm-picker");
    public ILocator Status(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    public async Task OpenManualPickerAsync()
    {
        await Manual.ClickAsync();
        await OpenPicker.ClickAsync();
        await Dialog.WaitForAsync();
    }
}

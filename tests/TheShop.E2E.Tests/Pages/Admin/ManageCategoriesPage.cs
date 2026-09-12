using Microsoft.Playwright;
using TheShop.Web.Common.Sorting;
using TheShop.Web.Resources;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Page object for the Manage Categories admin list (/admin/categories).</summary>
public sealed class ManageCategoriesPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.ManageCategories;

    /// <summary>
    /// Navigates to the list sorted oldest-first. The five categories seeded by migration 0002 are
    /// the oldest rows in the table, so this order puts all of them on page one no matter how many
    /// categories earlier journeys have added.
    /// </summary>
    public Task GotoOldestFirstAsync() =>
        GotoAsync($"{WebRoutes.Admin.ManageCategories}?sort={SortSlugs.Oldest}");

    /// <summary>Locator for the "Add Category" link — absent for staff without categories.create.</summary>
    public ILocator AddCategoryLink =>
        Page.GetByRole(AriaRole.Link, new() { Name = Strings.AddCategory_Heading });

    /// <summary>Navigates to the add-category form via the "Add Category" link.</summary>
    public async Task GotoAddCategoryAsync() => await AddCategoryLink.ClickAsync();

    /// <summary>Locator for a category row's edit link — absent for staff without categories.edit.</summary>
    public ILocator EditLink(string categoryName) =>
        Page.GetByRole(AriaRole.Link, new() { Name = string.Format(Strings.ManageCategories_EditAria, categoryName) });

    /// <summary>Navigates to a specific category's edit form via its row's edit link.</summary>
    public async Task GotoEditCategoryAsync(string categoryName) => await EditLink(categoryName).ClickAsync();

    /// <summary>
    /// The table row owning a category, matched on an exact name cell rather than substring text —
    /// journeys deliberately create categories whose names contain a seeded category's name, so a
    /// <c>HasText</c> filter would match several rows at once.
    /// </summary>
    public ILocator Row(string categoryName) =>
        Page.Locator("tr").Filter(new() { Has = Page.GetByText(categoryName, new() { Exact = true }) });

    /// <summary>The multi-selection checkbox of a category's row.</summary>
    public ILocator RowCheckbox(string categoryName) => Row(categoryName).GetByRole(AriaRole.Checkbox);

    /// <summary>Thumbnail treatment frames rendered by category rows.</summary>
    public ILocator ThumbnailFrames => Page.Locator(".shop-image-thumbnail");

    /// <summary>
    /// A category row's status chip. Clicking it flips the status — immediately when activating,
    /// behind a confirmation when deactivating (spec AC-16). Located by the chip's own label so
    /// this works without reaching for MudBlazor's internal chip classes.
    /// </summary>
    public ILocator StatusChip(string categoryName, bool active) =>
        Row(categoryName).GetByText(
            active ? Strings.AddCategory_StatusActive : Strings.AddCategory_StatusInactive,
            new() { Exact = true });

    /// <summary>The delete control on a category's row — absent for staff without categories.delete.</summary>
    public ILocator DeleteButton(string categoryName) =>
        Page.GetByRole(AriaRole.Button, new() { Name = string.Format(Strings.ManageCategories_DeleteAria, categoryName) });

    /// <summary>The in-use caption a row gains when its deletion was refused (spec FR-15).</summary>
    public ILocator InUseIndicator(string categoryName, int productCount) =>
        Row(categoryName).GetByText(string.Format(Strings.ManageCategories_InUseIndicator, productCount));

    /// <summary>Searches the category list.</summary>
    public async Task SearchAsync(string term) =>
        await Page.GetByPlaceholder(Strings.ManageCategories_SearchPlaceholder).FillAsync(term);

    /// <summary>
    /// Navigates to the list deep-linked to a search term. Preferred over typing into the search
    /// box whenever a journey only needs a category on screen: the term is applied by the page's
    /// first query rather than by a debounced re-query, so there is no window in which the row on
    /// screen still belongs to the unfiltered render. Acting inside that window is a real race —
    /// the target category is often already listed before the search runs, so a "wait for the row"
    /// settle can be satisfied by the very render that is about to be replaced.
    /// </summary>
    public Task GotoSearchingForAsync(string term) =>
        GotoAsync($"{WebRoutes.Admin.ManageCategories}?search={Uri.EscapeDataString(term)}");

    /// <summary>
    /// The viewport-fixed bulk-action bar, shown while rows are selected. Scoped by the project's
    /// own <c>shop-bulk-action-bar</c> class — its Active/Inactive/Delete labels collide with the
    /// row status chips and the row delete buttons, so its actions must be looked up within it.
    /// </summary>
    public ILocator BulkActionBar => Page.Locator(".shop-bulk-action-bar");

    /// <summary>The bulk bar's selected-count label for a given count.</summary>
    public ILocator BulkSelectedCount(int count) =>
        BulkActionBar.GetByText(string.Format(Strings.BulkActions_SelectedCount, count));

    /// <summary>The bulk Activate action.</summary>
    public ILocator BulkActivateButton =>
        BulkActionBar.GetByRole(AriaRole.Button, new() { Name = Strings.ManageCategories_BulkSetActive });

    /// <summary>The bulk Deactivate action.</summary>
    public ILocator BulkDeactivateButton =>
        BulkActionBar.GetByRole(AriaRole.Button, new() { Name = Strings.ManageCategories_BulkSetInactive });

    /// <summary>The bulk Delete action.</summary>
    public ILocator BulkDeleteButton =>
        BulkActionBar.GetByRole(AriaRole.Button, new() { Name = Strings.ManageCategories_BulkDelete });
}

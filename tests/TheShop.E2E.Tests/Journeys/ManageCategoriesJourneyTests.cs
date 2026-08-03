using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// The catalogue rows seeded by migration 0002 that these journeys lean on. Every one of the five
/// seeded categories holds products, which is what makes them usable as the "in use" side of the
/// deletion rules (RULE-6, RULE-15) without an E2E journey having to create products first.
/// </summary>
internal static class SeededCategories
{
    /// <summary>A seeded category holding five products (migration 0002).</summary>
    public const string InUse = "Disposables";

    /// <summary>The product count of <see cref="InUse"/>, as seeded by migration 0002.</summary>
    public const int InUseProductCount = 5;

    /// <summary>A second seeded category holding products, for all-blocked bulk deletion.</summary>
    public const string SecondInUse = "Pod Systems";
}

/// <summary>
/// Manage Categories admin journeys (.specs/manage-categories/spec.md §6). Shared-database
/// discipline: every created category uses a unique generated name so reruns never collide; there
/// is no cleanup step since these are harmless, clearly-marked "e2e-category-" rows on a stack that
/// gets a full `supabase db reset` between development sessions.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "manage-categories")]
public sealed class ManageCategoriesJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    [Fact]
    public async Task AC6_Admin_creates_a_category_and_sees_it_listed()
    {
        var categoryName = $"e2e-category-{Guid.NewGuid():N}";

        var manageCategories = new ManageCategoriesPage(Page);
        await manageCategories.GotoAsync();
        await manageCategories.GotoAddCategoryAsync();

        var addCategory = new AddCategoryPage(Page);
        await addCategory.CreateAsync(categoryName);

        // Handler navigates back to Routes.Admin.ManageCategories on success.
        await WaitForReturnToListAsync(WebRoutes.Admin.AddCategory);
        await GotoListShowingAsync(manageCategories, categoryName);
    }

    [Fact]
    public async Task AC8_Admin_edits_a_category_and_it_persists()
    {
        var originalName = $"e2e-category-{Guid.NewGuid():N}";
        var renamedTo = $"e2e-category-{Guid.NewGuid():N}";

        var manageCategories = new ManageCategoriesPage(Page);
        await CreateCategoryAsync(manageCategories, originalName);

        await manageCategories.GotoEditCategoryAsync(originalName);
        var editCategory = new EditCategoryPage(Page);
        await editCategory.RenameAsync(renamedTo);

        await WaitForReturnToListAsync("/edit");
        await GotoListShowingAsync(manageCategories, renamedTo);
    }

    /// <summary>
    /// AC-15: deactivating a category withdraws it from the customer-facing category filter while
    /// leaving it listed for staff, and reactivating it puts it back. This is the one clause of the
    /// feature whose effect crosses from the admin console to the storefront, so it can only be
    /// proven against the real <c>get_catalogue_filters()</c> RPC.
    /// </summary>
    [Fact]
    public async Task AC15_Deactivating_a_category_withdraws_it_from_the_customer_category_filter()
    {
        var categoryName = $"e2e-category-{Guid.NewGuid():N}";

        var manageCategories = new ManageCategoriesPage(Page);
        await CreateCategoryAsync(manageCategories, categoryName);

        // Created Active (RULE-5), so customers are offered it from the start.
        var catalogue = new CataloguePage(Page);
        await ExpectOfferedToCustomersAsync(catalogue, categoryName, offered: true);

        await GotoListShowingAsync(manageCategories, categoryName);
        await DeactivateFromListAsync(manageCategories, categoryName);

        await ExpectOfferedToCustomersAsync(catalogue, categoryName, offered: false);

        await GotoListShowingAsync(manageCategories, categoryName);
        await manageCategories.StatusChip(categoryName, active: false).ClickAsync();
        await manageCategories.StatusChip(categoryName, active: true).WaitForAsync(new() { Timeout = 15_000 });

        await ExpectOfferedToCustomersAsync(catalogue, categoryName, offered: true);
    }

    /// <summary>
    /// AC-16: flipping a status straight from the list activates immediately but asks before
    /// deactivating — RULE-12 gates only the direction that hides a category from customers.
    /// </summary>
    [Fact]
    public async Task AC16_Activating_from_the_list_is_immediate_while_deactivating_asks_first()
    {
        var categoryName = $"e2e-category-{Guid.NewGuid():N}";

        var manageCategories = new ManageCategoriesPage(Page);
        await CreateCategoryAsync(manageCategories, categoryName);

        // Categories are created Active, so reach the AC's Inactive starting state first.
        await DeactivateFromListAsync(manageCategories, categoryName);

        var confirm = new ConfirmDialogPage(Page);

        // Inactive -> Active: no prompt at all, just the confirmation toast.
        await manageCategories.StatusChip(categoryName, active: false).ClickAsync();
        await manageCategories.StatusChip(categoryName, active: true).WaitForAsync(new() { Timeout = 15_000 });
        (await confirm.Surface.CountAsync()).Should().Be(0,
            "activating a category reaches no customer-facing surface, so it is not gated behind a confirmation");
        await manageCategories.Snackbar
            .Filter(new() { HasText = string.Format(Strings.ManageCategories_ActivatedSuccess, 1) })
            .First.WaitForAsync(new() { Timeout = 15_000 });

        // Active -> Inactive: asks first, naming the category it is about to hide.
        await manageCategories.StatusChip(categoryName, active: true).ClickAsync();
        await confirm.WaitForOpenAsync();
        await confirm.Title(Strings.ManageCategories_DeactivateConfirmTitle)
            .WaitForAsync(new() { Timeout = 15_000 });
        await confirm.Body(string.Format(Strings.ManageCategories_DeactivateConfirmBody, categoryName))
            .WaitForAsync(new() { Timeout = 15_000 });

        await confirm.ConfirmAsync(Strings.ManageCategories_BulkDeactivate);
        await manageCategories.StatusChip(categoryName, active: false).WaitForAsync(new() { Timeout = 15_000 });
    }

    /// <summary>
    /// AC-17: deleting a category no product belongs to is confirmed through a prompt that names it
    /// and states the permanence (RULE-7), and the row is then gone from the list.
    /// </summary>
    /// <remarks>
    /// The AC's remaining clause — that the deleted category's image is no longer stored — is not
    /// asserted here: <c>category-images</c> is a private bucket, so proving the object's absence
    /// needs a service-role storage probe rather than anything the browser can observe. That clause
    /// stays a Tier 2 check in <c>/theshop.verify</c>.
    /// </remarks>
    [Fact]
    public async Task AC17_Deleting_an_unused_category_is_confirmed_then_removes_it()
    {
        var categoryName = $"e2e-category-{Guid.NewGuid():N}";

        var manageCategories = new ManageCategoriesPage(Page);
        await CreateCategoryAsync(manageCategories, categoryName);

        await manageCategories.DeleteButton(categoryName).ClickAsync();

        var confirm = new ConfirmDialogPage(Page);
        await confirm.WaitForOpenAsync();
        await confirm.Title(Strings.ManageCategories_DeleteConfirmTitle)
            .WaitForAsync(new() { Timeout = 15_000 });
        await confirm.Body(string.Format(Strings.ManageCategories_DeleteConfirmBody, categoryName))
            .WaitForAsync(new() { Timeout = 15_000 });

        await confirm.ConfirmAsync(Strings.ManageCategories_BulkDelete);
        await confirm.WaitForClosedAsync();

        await manageCategories.Snackbar
            .Filter(new() { HasText = Strings.ManageCategories_DeletedSuccess })
            .First.WaitForAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(manageCategories.Row(categoryName))
            .ToHaveCountAsync(0, new() { Timeout = 15_000 });
    }

    /// <summary>AC-18: dismissing the delete prompt deletes nothing and leaves the list unchanged.</summary>
    [Fact]
    public async Task AC18_Cancelling_the_delete_prompt_leaves_the_category_in_place()
    {
        var categoryName = $"e2e-category-{Guid.NewGuid():N}";

        var manageCategories = new ManageCategoriesPage(Page);
        await CreateCategoryAsync(manageCategories, categoryName);

        await manageCategories.DeleteButton(categoryName).ClickAsync();

        var confirm = new ConfirmDialogPage(Page);
        await confirm.WaitForOpenAsync();
        await confirm.CancelAsync();
        await confirm.WaitForClosedAsync();

        await Assertions.Expect(manageCategories.Row(categoryName))
            .ToHaveCountAsync(1, new() { Timeout = 15_000 });
    }

    /// <summary>
    /// AC-19: deleting a category that holds products is refused with the count of products in it
    /// (FR-15, RULE-6), and the category is left in the list flagged as in use. Proven against a
    /// seeded category rather than a created one — the product count comes from the real
    /// <c>category_product_counts</c> view.
    /// </summary>
    [Fact]
    public async Task AC19_Deleting_a_category_that_holds_products_is_refused_with_its_product_count()
    {
        var manageCategories = new ManageCategoriesPage(Page);
        await GotoListShowingAsync(manageCategories, SeededCategories.InUse);

        await manageCategories.DeleteButton(SeededCategories.InUse).ClickAsync();

        var confirm = new ConfirmDialogPage(Page);
        await confirm.WaitForOpenAsync();
        await confirm.ConfirmAsync(Strings.ManageCategories_BulkDelete);
        await confirm.WaitForClosedAsync();

        await manageCategories.Snackbar
            .Filter(new()
            {
                HasText = string.Format(
                    Strings.Category_InUse, SeededCategories.InUse, SeededCategories.InUseProductCount),
            })
            .First.WaitForAsync(new() { Timeout = 15_000 });

        await Assertions.Expect(manageCategories.Row(SeededCategories.InUse))
            .ToHaveCountAsync(1, new() { Timeout = 15_000 });
        await manageCategories.InUseIndicator(SeededCategories.InUse, SeededCategories.InUseProductCount)
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    /// <summary>
    /// AC-29: a bulk deletion mixing empty and in-use categories deletes the empty ones, keeps the
    /// in-use ones, reports both counts, and leaves the kept ones selected (FR-24, FR-25, RULE-15).
    /// </summary>
    /// <remarks>
    /// The AC is written with five categories of which two are in use; this proves the same rule
    /// with three of which one is in use, because selection is page-scoped (RULE-14) and the
    /// created categories must therefore share a page with a seeded in-use one. Naming them after
    /// that seeded category is what lets a single search list all three together.
    /// </remarks>
    [Fact]
    public async Task AC29_Bulk_delete_removes_the_empty_categories_and_keeps_the_ones_in_use()
    {
        var firstEmpty = $"e2e-{SeededCategories.InUse}-{Guid.NewGuid():N}";
        var secondEmpty = $"e2e-{SeededCategories.InUse}-{Guid.NewGuid():N}";

        var manageCategories = new ManageCategoriesPage(Page);
        await CreateCategoryAsync(manageCategories, firstEmpty);
        await CreateCategoryAsync(manageCategories, secondEmpty);

        await manageCategories.GotoSearchingForAsync(SeededCategories.InUse);

        // Selected row by row rather than through the page's select-all, so the selection is these
        // three categories exactly however many other rows the search happens to match.
        foreach (var name in new[] { firstEmpty, secondEmpty, SeededCategories.InUse })
        {
            await Assertions.Expect(manageCategories.Row(name)).ToHaveCountAsync(1, new() { Timeout = 15_000 });
            await manageCategories.RowCheckbox(name).ClickAsync();
        }

        await manageCategories.BulkSelectedCount(3).WaitForAsync(new() { Timeout = 15_000 });

        await manageCategories.BulkDeleteButton.ClickAsync();

        var confirm = new ConfirmDialogPage(Page);
        await confirm.WaitForOpenAsync();
        await confirm.Title(string.Format(Strings.ManageCategories_BulkDeleteConfirmTitle, 3))
            .WaitForAsync(new() { Timeout = 15_000 });
        await confirm.ConfirmAsync(Strings.ManageCategories_BulkDelete);
        await confirm.WaitForClosedAsync();

        await manageCategories.Snackbar
            .Filter(new() { HasText = string.Format(Strings.Category_BulkDeletePartial, 2, 1) })
            .First.WaitForAsync(new() { Timeout = 15_000 });

        await Assertions.Expect(manageCategories.Row(firstEmpty))
            .ToHaveCountAsync(0, new() { Timeout = 15_000 });
        await Assertions.Expect(manageCategories.Row(secondEmpty))
            .ToHaveCountAsync(0, new() { Timeout = 15_000 });
        await Assertions.Expect(manageCategories.Row(SeededCategories.InUse))
            .ToHaveCountAsync(1, new() { Timeout = 15_000 });
        await manageCategories.InUseIndicator(SeededCategories.InUse, SeededCategories.InUseProductCount)
            .WaitForAsync(new() { Timeout = 15_000 });

        // RULE-15: the kept category stays selected, so the bar is still up — now counting one.
        await manageCategories.BulkSelectedCount(1).WaitForAsync(new() { Timeout = 15_000 });
    }

    /// <summary>
    /// AC-30: a bulk deletion in which every selected category holds products deletes nothing and
    /// says so, suggesting deactivation instead (RULE-15).
    /// </summary>
    [Fact]
    public async Task AC30_Bulk_delete_of_only_in_use_categories_deletes_nothing()
    {
        var manageCategories = new ManageCategoriesPage(Page);
        await manageCategories.GotoOldestFirstAsync();

        foreach (var name in new[] { SeededCategories.InUse, SeededCategories.SecondInUse })
        {
            await Assertions.Expect(manageCategories.Row(name)).ToHaveCountAsync(1, new() { Timeout = 15_000 });
            await manageCategories.RowCheckbox(name).ClickAsync();
        }

        await manageCategories.BulkSelectedCount(2).WaitForAsync(new() { Timeout = 15_000 });
        await manageCategories.BulkDeleteButton.ClickAsync();

        var confirm = new ConfirmDialogPage(Page);
        await confirm.WaitForOpenAsync();
        await confirm.ConfirmAsync(Strings.ManageCategories_BulkDelete);
        await confirm.WaitForClosedAsync();

        await manageCategories.Snackbar
            .Filter(new() { HasText = Strings.Category_BulkDeleteAllBlocked })
            .First.WaitForAsync(new() { Timeout = 15_000 });

        foreach (var name in new[] { SeededCategories.InUse, SeededCategories.SecondInUse })
            await Assertions.Expect(manageCategories.Row(name)).ToHaveCountAsync(1, new() { Timeout = 15_000 });
    }

    /// <summary>Creates a category through the add form and leaves the list showing its row.</summary>
    private async Task CreateCategoryAsync(ManageCategoriesPage manageCategories, string categoryName)
    {
        await manageCategories.GotoAsync();
        await manageCategories.GotoAddCategoryAsync();

        await new AddCategoryPage(Page).CreateAsync(categoryName);

        await WaitForReturnToListAsync(WebRoutes.Admin.AddCategory);
        await GotoListShowingAsync(manageCategories, categoryName);
    }

    /// <summary>
    /// Waits for a form to hand back to the category list, identified by the list route without the
    /// form's own segment still in the URL.
    /// </summary>
    private Task WaitForReturnToListAsync(string formSegment) =>
        Page.WaitForURLAsync(
            url => url.Contains(WebRoutes.Admin.ManageCategories, StringComparison.Ordinal)
                && !url.Contains(formSegment, StringComparison.Ordinal),
            new() { Timeout = 15_000 });

    /// <summary>
    /// Opens the list deep-linked to one category and waits for its row to be listed. Journeys
    /// confirm a category's presence this way rather than scanning the default list: that list is
    /// name-ordered and paged at ten, so a generated name lands on an unpredictable page once the
    /// table has accumulated rows.
    /// </summary>
    private static async Task GotoListShowingAsync(ManageCategoriesPage manageCategories, string categoryName)
    {
        await manageCategories.GotoSearchingForAsync(categoryName);
        await Assertions.Expect(manageCategories.Row(categoryName))
            .ToHaveCountAsync(1, new() { Timeout = 15_000 });
    }

    /// <summary>Deactivates a listed category through its status chip, clearing the RULE-12 prompt.</summary>
    private async Task DeactivateFromListAsync(ManageCategoriesPage manageCategories, string categoryName)
    {
        await manageCategories.StatusChip(categoryName, active: true).ClickAsync();

        var confirm = new ConfirmDialogPage(Page);
        await confirm.WaitForOpenAsync();
        await confirm.ConfirmAsync(Strings.ManageCategories_BulkDeactivate);
        await manageCategories.StatusChip(categoryName, active: false).WaitForAsync(new() { Timeout = 15_000 });
    }

    /// <summary>
    /// Asserts whether the storefront's Category filter offers a category, from a fresh load of the
    /// catalogue so the <c>get_catalogue_filters()</c> facets are re-fetched rather than reused.
    /// </summary>
    private static async Task ExpectOfferedToCustomersAsync(
        CataloguePage catalogue, string categoryName, bool offered)
    {
        await catalogue.GotoAsync();
        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Category);

        if (offered)
        {
            await catalogue.FilterOption(categoryName).WaitForAsync(new() { Timeout = 15_000 });
            return;
        }

        await Assertions.Expect(catalogue.FilterOption(categoryName))
            .ToHaveCountAsync(0, new() { Timeout = 15_000 });
    }
}

/// <summary>
/// AC-20: a staff member holding only <c>categories.view</c> gets a read-only list — no create,
/// edit, status-flip, delete, or bulk control — and is refused the create capability by direct
/// link. Support is seeded with every module's <c>*.view</c> permission and nothing else
/// (migration 0017), which is exactly this AC's persona.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "manage-categories")]
public sealed class ManageCategoriesViewOnlyJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.SupportEmail)
{
    [Fact]
    public async Task AC20_View_only_staff_get_no_write_controls_and_no_add_form()
    {
        var manageCategories = new ManageCategoriesPage(Page);
        await manageCategories.GotoOldestFirstAsync();
        await Assertions.Expect(manageCategories.Row(SeededCategories.InUse))
            .ToHaveCountAsync(1, new() { Timeout = 15_000 });

        (await manageCategories.AddCategoryLink.CountAsync()).Should().Be(0,
            "categories.create is what renders the Add Category control");
        (await manageCategories.EditLink(SeededCategories.InUse).CountAsync()).Should().Be(0,
            "categories.edit is what renders a row's edit link");
        (await manageCategories.DeleteButton(SeededCategories.InUse).CountAsync()).Should().Be(0,
            "categories.delete is what renders a row's delete control");
        (await manageCategories.Row(SeededCategories.InUse).GetByRole(AriaRole.Button).CountAsync())
            .Should().Be(0,
                "a view-only row shows its status as plain text, leaving no actionable control on the row");

        // Selection itself is not permission-gated, so the bar comes up — but with no action in it.
        await manageCategories.RowCheckbox(SeededCategories.InUse).ClickAsync();
        await manageCategories.BulkSelectedCount(1).WaitForAsync(new() { Timeout = 15_000 });
        (await manageCategories.BulkActionBar.GetByRole(AriaRole.Button).CountAsync()).Should().Be(1,
            "the only control a view-only staff member gets in the bulk bar is the one that dismisses it");

        // FR-18 / RULE-8: the create capability is refused by direct link, not merely hidden.
        await new AddCategoryPage(Page).GotoAsync();
        await Page.GetByText(Strings.AccessDenied_Title).WaitForAsync(new() { Timeout = 15_000 });
    }
}

/// <summary>
/// AC-21: a user without <c>categories.view</c> cannot reach the manage-categories page at all,
/// including by direct link. The Customer persona holds no category permission of any kind.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "manage-categories")]
public sealed class ManageCategoriesAccessDeniedJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.CustomerEmail)
{
    [Fact]
    public async Task AC21_A_user_without_the_view_permission_cannot_reach_the_list()
    {
        var manageCategories = new ManageCategoriesPage(Page);
        await manageCategories.GotoAsync();

        await Page.GetByText(Strings.AccessDenied_Title).WaitForAsync(new() { Timeout = 15_000 });
        (await manageCategories.Row(SeededCategories.InUse).CountAsync()).Should().Be(0,
            "the page is gated on categories.view, so none of its list content is reachable");
    }
}

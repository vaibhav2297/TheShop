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
/// Manage Product admin journeys (.specs/manage-product/spec.md §6) covering the three acceptance
/// criteria that need a real browser: completing the existing Add/Edit flows and seeing the result
/// in subsequent matching listing results (AC-8, AC-9), and a status change's cross-feature effect
/// on the public storefront catalogue (AC-12). Every other acceptance criterion is proven at the
/// unit/component tier — see .specs/manage-product/e2e-manifest.json. Shared-database discipline:
/// every product this file creates uses a unique generated name so reruns never collide; there is
/// no cleanup step since these are harmless, clearly-marked "e2e-product-" rows on a stack that
/// gets a full <c>supabase db reset</c> between development sessions. Reuses the seeded
/// <c>SeededCatalogue</c> category/brand declared by <c>CreateProductJourneyTests</c>.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "manage-product")]
public sealed class ManageProductsJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    [Fact]
    [Trait("Feature", "reusable-image-treatments")]
    public async Task Admin_lists_render_confirmed_image_treatments()
    {
        var products = new ManageProductsPage(Page);
        await products.GotoAsync();
        await AssertContainTreatmentAsync(products.ThumbnailFrames.First, "shop-image-thumbnail", square: true);

        var categories = new ManageCategoriesPage(Page);
        await categories.GotoAsync();
        await AssertContainTreatmentAsync(categories.ThumbnailFrames.First, "shop-image-thumbnail", square: true);

        var brands = new ManageBrandsPage(Page);
        await brands.GotoAsync();
        await AssertContainTreatmentAsync(brands.LogoFrames.First, "shop-image-brand-logo", square: false);
    }

    [Fact]
    public async Task AC8_Creating_a_product_through_the_add_flow_appears_in_subsequent_matching_results()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        await CreateDraftProductAsync(name);

        var manageProducts = new ManageProductsPage(Page);
        // FR-8: proven through a name search rather than default sort/page position, which is
        // what "subsequent matching listing results" actually asks for.
        await manageProducts.SearchAsync(name);
        await manageProducts.Row(name).WaitForAsync(new() { Timeout = 15_000 });
    }

    private static async Task AssertContainTreatmentAsync(ILocator frame, string expectedClass, bool square)
    {
        await frame.WaitForAsync(new() { Timeout = 15_000 });
        (await frame.GetAttributeAsync("class")).Should().Contain(expectedClass);
        (await frame.Locator("img").EvaluateAsync<string>("element => getComputedStyle(element).objectFit"))
            .Should().Be("contain");

        if (!square)
            return;

        var bounds = await frame.BoundingBoxAsync();
        bounds.Should().NotBeNull();
        bounds!.Width.Should().BeApproximately(bounds.Height, 1);
    }

    [Fact]
    public async Task AC9_Editing_a_product_through_the_edit_flow_shows_saved_changes_in_subsequent_matching_results()
    {
        var originalName = $"e2e-product-{Guid.NewGuid():N}";
        var renamedTo = $"e2e-product-{Guid.NewGuid():N}";
        await CreateDraftProductAsync(originalName);

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.SearchAsync(originalName);
        await manageProducts.Row(originalName).WaitForAsync(new() { Timeout = 15_000 });
        await manageProducts.GotoEditProductAsync(originalName);

        var edit = new EditProductPage(Page);
        await Assertions.Expect(edit.Form.NameField).ToHaveValueAsync(originalName, new() { Timeout = 15_000 });
        await edit.Form.FillNameAsync(renamedTo);
        await edit.Form.SaveAsync();

        await Page.WaitForURLAsync(
            url => url.Contains(WebRoutes.Admin.ManageProducts, StringComparison.Ordinal)
                && !url.Contains("/edit", StringComparison.Ordinal),
            new() { Timeout = 15_000 });

        await manageProducts.SearchAsync(renamedTo);
        await manageProducts.Row(renamedTo).WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC12_Deactivating_a_published_product_removes_it_from_the_catalogue_while_staff_still_see_it_and_reactivating_restores_it()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SetPriceAsync(15.00m);
        await add.Form.UploadImagesAsync(ProductImageFixtures.Png("e2e-ac12.png"));
        await add.Form.ImageRows.First.WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.SetPublishedAsync();
        await add.Form.SaveAsync();
        await Page.WaitForURLAsync(
            url => url.Contains(WebRoutes.Admin.ManageProducts, StringComparison.Ordinal)
                && !url.Contains(WebRoutes.Admin.AddProduct, StringComparison.Ordinal),
            new() { Timeout = 15_000 });

        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();
        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Category);
        await catalogue.ToggleFilterOptionAsync(SeededCatalogue.Category);
        await catalogue.ProductName(name).WaitForAsync(new() { Timeout = 15_000 });

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();
        await manageProducts.SearchAsync(name);
        await manageProducts.Row(name).WaitForAsync(new() { Timeout = 15_000 });
        await manageProducts.StatusChip(name, active: true).ClickAsync();

        var confirm = new ConfirmDialogPage(Page);
        await confirm.WaitForOpenAsync();
        await confirm.ConfirmAsync(Strings.ManageProducts_BulkDeactivate);
        await confirm.WaitForClosedAsync();
        await manageProducts.StatusChip(name, active: false).WaitForAsync(new() { Timeout = 15_000 });

        // Deactivated: leaves the public catalogue (FR-10), but the admin list keeps the full row
        // — nothing about the product's own details is discarded by a status change.
        await catalogue.GotoAsync();
        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Category);
        await catalogue.ToggleFilterOptionAsync(SeededCatalogue.Category);
        await Assertions.Expect(catalogue.ProductName(name)).ToHaveCountAsync(0, new() { Timeout = 15_000 });

        await manageProducts.GotoAsync();
        await manageProducts.SearchAsync(name);
        await manageProducts.Row(name).WaitForAsync(new() { Timeout = 15_000 });

        // Reactivation applies without confirmation (FR-9) and restores catalogue eligibility (FR-10).
        await manageProducts.StatusChip(name, active: false).ClickAsync();
        await manageProducts.StatusChip(name, active: true).WaitForAsync(new() { Timeout = 15_000 });

        await catalogue.GotoAsync();
        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Category);
        await catalogue.ToggleFilterOptionAsync(SeededCatalogue.Category);
        await catalogue.ProductName(name).WaitForAsync(new() { Timeout = 15_000 });
    }

    // ---- Shared helpers ------------------------------------------------------------------------

    /// <summary>Creates a minimal unpublished product through the Add flow, returning to the list.</summary>
    private async Task CreateDraftProductAsync(string name)
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SetPriceAsync(19.99m);
        await add.Form.SaveAsync();
        await Page.WaitForURLAsync(
            url => url.Contains(WebRoutes.Admin.ManageProducts, StringComparison.Ordinal)
                && !url.Contains(WebRoutes.Admin.AddProduct, StringComparison.Ordinal),
            new() { Timeout = 15_000 });
    }
}

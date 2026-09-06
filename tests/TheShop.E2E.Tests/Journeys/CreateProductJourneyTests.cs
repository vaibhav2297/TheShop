using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// Minimal, decodable single-pixel image payloads used to drive <c>ShopImageUpload</c> through
/// Playwright without touching disk — <c>SetInputFilesAsync</c> accepts an in-memory buffer, so
/// no fixture file is needed under a folder this command is not permitted to edit.
/// </summary>
internal static class ProductImageFixtures
{
    // A valid 1x1 transparent PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    public static FilePayload Png(string fileName) => new()
    {
        Name = fileName,
        MimeType = "image/png",
        Buffer = TinyPng,
    };

    /// <summary>A file whose declared type is not among the accepted image types (RULE-7).</summary>
    public static FilePayload WrongType(string fileName) => new()
    {
        Name = fileName,
        MimeType = "text/plain",
        Buffer = "not an image"u8.ToArray(),
    };

    /// <summary>A file over the 2 MB limit (RULE-7) — content doesn't need to decode, only its size matters.</summary>
    public static FilePayload TooLarge(string fileName) => new()
    {
        Name = fileName,
        MimeType = "image/png",
        Buffer = new byte[2 * 1024 * 1024 + 1],
    };
}

/// <summary>
/// The catalogue rows this feature's journeys lean on for an Active category and brand, seeded by
/// migration 0002 (.specs/create-product/spec.md RULE-3).
/// </summary>
internal static class SeededCatalogue
{
    public const string Category = "Accessories";
    public const string Brand = "Naked 100";
}

/// <summary>
/// Create Product admin journeys (.specs/create-product/spec.md §6). Shared-database discipline:
/// every created product uses a unique generated name (and therefore a unique generated SKU) so
/// reruns never collide; there is no cleanup step since these are harmless, clearly-marked
/// "e2e-product-" rows on a stack that gets a full <c>supabase db reset</c> between sessions.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "create-product")]
public sealed class CreateProductJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    [Fact]
    public async Task AC1_Admin_sees_published_and_unpublished_products_with_pagination_and_no_search_controls()
    {
        var draftName = $"e2e-product-{Guid.NewGuid():N}";
        await CreateSimpleProductAsync(draftName, price: 19.99m, published: false);

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();

        // Newest-first (FR-2): the just-created draft is the first row on page one.
        await Assertions.Expect(manageProducts.Row(draftName)).ToHaveCountAsync(1, new() { Timeout = 15_000 });
        await manageProducts.StatusChip(draftName, active: false).WaitForAsync(new() { Timeout = 15_000 });

        // The 18 migration-0002 seed rows plus this draft exceed one page of ten, so pagination renders.
        await manageProducts.PageButton(2).WaitForAsync(new() { Timeout = 15_000 });

        (await Page.Locator("input[type=search], input[placeholder]").CountAsync()).Should().Be(0,
            "FR-2 fixes the list to a plain paginated view with no search, filter, sort, or bulk control");
    }

    [Fact]
    public async Task AC2_Moving_to_the_next_page_shows_the_next_set_in_the_same_order()
    {
        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();
        await Page.Locator("tbody tr").First.WaitForAsync(new() { Timeout = 15_000 });

        var pageOneNames = await Page.Locator("tbody tr td:nth-child(1)").AllTextContentsAsync();
        pageOneNames.Should().HaveCount(10, "the list is fixed at ten rows per page (FR-2)");

        await manageProducts.GotoPageAsync(2);
        // Auto-retrying assertion for the paged re-fetch, rather than a fixed sleep: the first
        // row's own leaving proves the new page has landed.
        await Assertions.Expect(Page.Locator("tbody tr").First)
            .Not.ToContainTextAsync(pageOneNames[0], new() { Timeout = 15_000 });

        var pageTwoNames = await Page.Locator("tbody tr td:nth-child(1)").AllTextContentsAsync();
        pageTwoNames.Should().NotBeEmpty();
        pageTwoNames.Should().NotIntersectWith(pageOneNames,
            "the second page must show the next set of products, not repeat the first page's rows");
    }

    [Fact]
    public async Task AC4_Admin_creates_a_published_product_and_it_appears_in_the_catalogue()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";

        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.FillDescriptionAsync("A long-lasting disposable vape with smooth airflow.");
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SetPriceAsync(24.99m);
        await add.Form.UploadImagesAsync(
            ProductImageFixtures.Png("e2e-image-1.png"),
            ProductImageFixtures.Png("e2e-image-2.png"));
        await add.Form.ImageRows.Nth(1).WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.SetPublishedAsync();
        await add.Form.SaveAsync();

        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);
        var manageProducts = new ManageProductsPage(Page);
        await Assertions.Expect(manageProducts.Row(name)).ToHaveCountAsync(1, new() { Timeout = 15_000 });
        await manageProducts.StatusChip(name, active: true).WaitForAsync(new() { Timeout = 15_000 });

        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();
        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Category);
        await catalogue.ToggleFilterOptionAsync(SeededCatalogue.Category);
        await catalogue.ProductName(name).WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC6_Edit_form_opens_prefilled_with_everything_saved()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var optionType = $"Flavour-{Guid.NewGuid():N}"[..12];

        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.UploadImagesAsync(ProductImageFixtures.Png("e2e-image-1.png"));
        await add.Form.ImageRows.First.WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(0, optionType);
        await add.Form.FillOptionValueAsync(0, "Mango");
        var label = "Mango";
        await add.Form.VariantRow(label).WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.SetVariantPriceAsync(label, 24.99m);
        await add.Form.SaveAsync();
        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();
        await manageProducts.GotoEditProductAsync(name);

        var edit = new EditProductPage(Page);
        await Assertions.Expect(edit.Form.NameField).ToHaveValueAsync(name, new() { Timeout = 15_000 });
        await edit.Form.ImageRows.First.WaitForAsync(new() { Timeout = 15_000 });
        // An existing image's row shows the object key rather than its original upload filename,
        // so the primary badge is asserted on the first row directly instead of by that filename.
        await edit.Form.ImageRows.First.GetByText(Strings.AddProduct_ImagePrimary, new() { Exact = true })
            .WaitForAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(edit.Form.OptionTypeNameField(0)).ToHaveValueAsync(optionType, new() { Timeout = 15_000 });
        await Assertions.Expect(edit.Form.VariantPriceInput(label)).ToHaveValueAsync("24.99", new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC7_Gallery_images_can_be_ordered_and_removed_and_a_removed_image_stops_being_offered()
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();

        await add.Form.UploadImagesAsync(
            ProductImageFixtures.Png("e2e-first.png"),
            ProductImageFixtures.Png("e2e-second.png"),
            ProductImageFixtures.Png("e2e-third.png"));
        await add.Form.ImageRows.Nth(2).WaitForAsync(new() { Timeout = 15_000 });

        // Upload order is the only ordering control this implementation offers — position decides
        // the primary image (spec FR-11 also asks for an explicit reorder/choose-primary action;
        // see the assertion below).
        await add.Form.PrimaryBadge("e2e-first.png").WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.RemoveImageAsync("e2e-first.png");
        await Assertions.Expect(add.Form.ImageRow("e2e-first.png")).ToHaveCountAsync(0, new() { Timeout = 15_000 });
        // RULE-7: removing the primary image promotes the next remaining one.
        await add.Form.PrimaryBadge("e2e-second.png").WaitForAsync(new() { Timeout = 15_000 });

        // FR-11 asks for a dedicated reorder action and an explicit "choose primary" action,
        // distinct from removal. Neither exists in this implementation (ShopImageUpload's own
        // doc comment: "there is no reorder" — position is fixed at upload time).
        var reorderOrChoosePrimaryControls = Page.GetByRole(AriaRole.Button, new()
        {
            NameRegex = new Regex("(reorder|move|primary)", RegexOptions.IgnoreCase),
        });
        (await reorderOrChoosePrimaryControls.CountAsync()).Should().BeGreaterThan(0,
            "FR-11 requires the staff member to be able to reorder images and choose the primary " +
            "image directly; this build only derives both from upload order via ShopImageUpload, " +
            "with no reorder or explicit make-primary control");
    }

    [Fact]
    public async Task AC8_An_invalid_image_is_refused_while_a_large_batch_of_valid_images_is_accepted()
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();

        await add.Form.UploadImagesAsync(ProductImageFixtures.WrongType("e2e-bad-type.txt"));
        await add.Form.ImageRow("e2e-bad-type.txt")
            .GetByText(Strings.Product_ImageInvalidType, new() { Exact = true })
            .WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.UploadImagesAsync(ProductImageFixtures.TooLarge("e2e-too-large.png"));
        await add.Form.ImageRow("e2e-too-large.png")
            .GetByText(Strings.Product_ImageTooLarge, new() { Exact = true })
            .WaitForAsync(new() { Timeout = 15_000 });

        var manyImages = Enumerable.Range(1, 12)
            .Select(i => ProductImageFixtures.Png($"e2e-bulk-{i}.png"))
            .ToArray();
        await add.Form.UploadImagesAsync(manyImages);
        await add.Form.ImageRow("e2e-bulk-12.png").WaitForAsync(new() { Timeout = 15_000 });

        (await add.Form.ImageRows.CountAsync()).Should().Be(14,
            "no image count is ever refused (RULE-7): the two earlier rows plus all twelve accepted uploads");
    }

    [Fact]
    public async Task AC9_Two_option_types_generate_exactly_four_labelled_variants_with_skus()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);

        // Before any option type exists, the product carries its own price field.
        await add.Form.PriceField.WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(0, "Flavour");
        await add.Form.FillOptionValueAsync(0, "Mango");
        await add.Form.AddOptionValueAsync(0);
        await add.Form.FillOptionValueAsync(1, "Mint");

        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(1, "Nicotine");
        await add.Form.FillOptionValueAsync(2, "20mg");
        await add.Form.AddOptionValueAsync(1);
        await add.Form.FillOptionValueAsync(3, "50mg");

        await add.Form.VariantCount(4).WaitForAsync(new() { Timeout = 15_000 });
        foreach (var label in new[] { "Mango / 20mg", "Mango / 50mg", "Mint / 20mg", "Mint / 50mg" })
        {
            var row = add.Form.VariantRow(label);
            await row.WaitForAsync(new() { Timeout = 15_000 });
            (await add.Form.VariantSku(label).InnerTextAsync()).Should().NotBeNullOrWhiteSpace(
                "every generated variant carries an automatically generated SKU (FR-15)");
        }

        // Once variants exist, the product-level price field is withdrawn (FR-8).
        (await add.Form.PriceField.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Covers AC-10 (price, sale price, pin, unavailable render on a variant row) and AC-10a
    /// (pinning with the shared-scope choice covers every variant sharing that option value,
    /// and a later individual re-pin touches only its own row) in one flow — the SDD gate's
    /// AC-id extraction only recognizes purely-numeric ids, so AC-10a is folded in here rather
    /// than declared as its own classification entry.
    /// </summary>
    [Fact]
    public async Task AC10_Variant_rows_show_price_sale_price_pinned_image_and_unavailable_status()
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.UploadImagesAsync(
            ProductImageFixtures.Png("e2e-a.png"),
            ProductImageFixtures.Png("e2e-b.png"));
        await add.Form.ImageRows.Nth(1).WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(0, "Flavour");
        await add.Form.FillOptionValueAsync(0, "Mango");
        await add.Form.AddOptionValueAsync(0);
        await add.Form.FillOptionValueAsync(1, "Mint");

        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(1, "Nicotine");
        await add.Form.FillOptionValueAsync(2, "20mg");
        await add.Form.AddOptionValueAsync(1);
        await add.Form.FillOptionValueAsync(3, "50mg");
        await add.Form.VariantCount(4).WaitForAsync(new() { Timeout = 15_000 });

        // AC-10: price, sale price, pin, and unavailable all render on their own row.
        await add.Form.SetVariantPriceAsync("Mango / 20mg", 19.99m);
        await add.Form.SetVariantSalePriceAsync("Mango / 20mg", 14.99m);
        await Assertions.Expect(add.Form.VariantPriceInput("Mango / 20mg")).ToHaveValueAsync("19.99", new() { Timeout = 15_000 });
        await Assertions.Expect(add.Form.VariantSalePriceInput("Mango / 20mg")).ToHaveValueAsync("14.99", new() { Timeout = 15_000 });

        await add.Form.ToggleVariantAvailabilityAsync("Mint / 20mg", currentlyAvailable: true);
        await add.Form.VariantAvailabilityChip("Mint / 20mg", available: false).WaitForAsync(new() { Timeout = 15_000 });

        // AC-10a: pinning with the shared scope covers every variant sharing that option value...
        await add.Form.OpenPinDialogAsync("Mango / 20mg");
        await add.Form.SelectPinDialogImageAsync(0);
        await add.Form.ApplyToAllSharingCheckbox(2, "Flavour = Mango").ClickAsync();
        await add.Form.ConfirmPinDialogAsync();

        await add.Form.VariantChangeImageButton("Mango / 20mg").WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.VariantChangeImageButton("Mango / 50mg").WaitForAsync(new() { Timeout = 15_000 });
        (await add.Form.VariantPinButton("Mint / 20mg").CountAsync()).Should().Be(1,
            "the Mint rows do not share Flavour = Mango, so applying the pin to that scope must not touch them");

        // ...and a later individual re-pin of one of those rows touches only that row.
        await add.Form.OpenPinDialogAsync("Mango / 50mg");
        await add.Form.SelectPinDialogImageAsync(1);
        await add.Form.ConfirmPinDialogAsync();

        await add.Form.VariantChangeImageButton("Mango / 20mg").WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.VariantChangeImageButton("Mango / 50mg").WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC11_Product_and_variant_skus_regenerate_live_with_no_editable_sku_control()
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();

        (await add.Form.SkuField.GetAttributeAsync("readonly")).Should().NotBeNull(
            "FR-15: the SKU is generated automatically and cannot be typed or edited directly");

        await add.Form.FillNameAsync("Mango Blast");
        await Assertions.Expect(add.Form.SkuField).ToHaveValueAsync("MANGO-BLAST", new() { Timeout = 15_000 });

        await add.Form.FillNameAsync("Berry Blast");
        await Assertions.Expect(add.Form.SkuField).ToHaveValueAsync("BERRY-BLAST", new() { Timeout = 15_000 });

        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(0, "Flavour");
        await add.Form.FillOptionValueAsync(0, "Mango");
        await add.Form.VariantRow("Mango").WaitForAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(add.Form.VariantSku("Mango")).ToHaveTextAsync("BERRY-BLAST-MANGO", new() { Timeout = 15_000 });

        // Renaming the option value regenerates its variant's SKU live, without a save (FR-15).
        await add.Form.FillOptionValueAsync(0, "Cherry");
        await add.Form.VariantRow("Cherry").WaitForAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(add.Form.VariantSku("Cherry")).ToHaveTextAsync("BERRY-BLAST-CHERRY", new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC13_Removing_an_option_value_confirms_the_discard_count_and_leaves_the_gallery_untouched()
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.UploadImagesAsync(ProductImageFixtures.Png("e2e-a.png"));
        await add.Form.ImageRows.First.WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(0, "Flavour");
        await add.Form.FillOptionValueAsync(0, "Mango");
        await add.Form.AddOptionValueAsync(0);
        await add.Form.FillOptionValueAsync(1, "Mint");
        await add.Form.VariantCount(2).WaitForAsync(new() { Timeout = 15_000 });

        // AC-13's precondition is "fully configured variants" — a row with no price entered
        // counts as unconfigured (RULE-11), and removing only unconfigured rows skips the
        // confirmation entirely, so both rows are priced here to reach the real confirm flow.
        await add.Form.SetVariantPriceAsync("Mango", 19.99m);
        await add.Form.SetVariantPriceAsync("Mint", 9.99m);

        await add.Form.RemoveOptionValueAsync(1); // "Mint"
        var confirm = new ConfirmDialogPage(Page);
        await confirm.WaitForOpenAsync();
        await confirm.Body(string.Format(Strings.ProductVariants_RemoveOptionConfirmBody, 1))
            .WaitForAsync(new() { Timeout = 15_000 });
        await confirm.CancelAsync();
        await confirm.WaitForClosedAsync();
        await add.Form.VariantRow("Mint").WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.RemoveOptionValueAsync(1);
        await confirm.WaitForOpenAsync();
        await confirm.ConfirmAsync(Strings.ProductVariants_RemoveOptionConfirm);
        await confirm.WaitForClosedAsync();

        await Assertions.Expect(add.Form.VariantRow("Mint")).ToHaveCountAsync(0, new() { Timeout = 15_000 });
        await add.Form.VariantRow("Mango").WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.ImageRow("e2e-a.png").WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC14_Removing_the_last_option_type_restores_the_product_level_price_field()
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(0, "Flavour");
        await add.Form.FillOptionValueAsync(0, "Mango");
        await add.Form.VariantRow("Mango").WaitForAsync(new() { Timeout = 15_000 });
        (await add.Form.PriceField.CountAsync()).Should().Be(0);

        // AC-14's precondition is "configured variants" (RULE-11) — an unpriced row is not
        // configured and removing only it skips confirmation entirely.
        await add.Form.SetVariantPriceAsync("Mango", 19.99m);

        await add.Form.RemoveOptionTypeAsync(0);
        var confirm = new ConfirmDialogPage(Page);
        await confirm.WaitForOpenAsync();
        await confirm.ConfirmAsync(Strings.ProductVariants_RemoveOptionConfirm);
        await confirm.WaitForClosedAsync();

        await add.Form.PriceField.WaitForAsync(new() { Timeout = 15_000 });
        (await add.Form.SalePriceField.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AC17_The_form_states_customers_pay_the_lowest_variant_price_once_variants_exist()
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(0, "Flavour");
        await add.Form.FillOptionValueAsync(0, "Mango");
        await add.Form.AddOptionValueAsync(0);
        await add.Form.FillOptionValueAsync(1, "Mint");
        await add.Form.VariantCount(2).WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.PriceRangeNote.WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.SetVariantPriceAsync("Mango", 34.99m);
        await add.Form.SetVariantPriceAsync("Mint", 24.99m);

        await add.Form.PriceRangeNoteWithValue(CurrencyFormatter.Format(24.99m))
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC19_A_minimal_draft_saves_as_unpublished_and_reopens_exactly_as_left()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SaveAsync();

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.Snackbar
            .Filter(new() { HasText = string.Format(Strings.Product_Created, name) })
            .First.WaitForAsync(new() { Timeout = 15_000 });
        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);
        await manageProducts.StatusChip(name, active: false).WaitForAsync(new() { Timeout = 15_000 });

        await manageProducts.GotoEditProductAsync(name);
        var edit = new EditProductPage(Page);
        await Assertions.Expect(edit.Form.NameField).ToHaveValueAsync(name, new() { Timeout = 15_000 });
        (await edit.Form.PriceField.InputValueAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AC20_Publishing_an_incomplete_product_is_refused_listing_what_is_missing()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);

        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(0, "Flavour");
        await add.Form.FillOptionValueAsync(0, "Mango");
        await add.Form.AddOptionValueAsync(0);
        await add.Form.FillOptionValueAsync(1, "Mint");
        await add.Form.VariantCount(2).WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.SetVariantPriceAsync("Mango", 19.99m); // "Mint" is left unpriced

        await add.Form.SetPublishedAsync();
        await add.Form.MissingPriceAlert(1, 2).WaitForAsync(new() { Timeout = 15_000 });
        // aria-invalid rather than a MudBlazor CSS class (which lands on a wrapper div, not the
        // <input> itself) — accessible-attribute assertions don't depend on internal markup shape.
        await Assertions.Expect(add.Form.VariantPriceInput("Mint")).ToHaveAttributeAsync(
            "aria-invalid", "true", new() { Timeout = 15_000 });

        // The save button itself is disabled while a variant is missing its price under Published.
        await Assertions.Expect(add.Form.SaveButton).ToBeDisabledAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC25_A_sale_price_at_or_above_the_price_is_refused_and_a_valid_one_shows_the_discount()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SetPriceAsync(20.00m);
        await add.Form.SetSalePriceAsync(20.00m);
        await add.Form.UploadImagesAsync(ProductImageFixtures.Png("e2e-a.png"));
        await add.Form.ImageRows.First.WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.SetPublishedAsync();
        await add.Form.SaveAsync();

        await Page.GetByText(Strings.Product_SalePriceTooHigh, new() { Exact = true })
            .WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.SetSalePriceAsync(14.99m);
        await add.Form.SaveAsync();
        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);

        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();
        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Category);
        await catalogue.ToggleFilterOptionAsync(SeededCatalogue.Category);
        await catalogue.ProductName(name).WaitForAsync(new() { Timeout = 15_000 });
        await Page.GetByText(CurrencyFormatter.Format(14.99m), new() { Exact = true })
            .WaitForAsync(new() { Timeout = 15_000 });
        await Page.GetByText(CurrencyFormatter.Format(20.00m), new() { Exact = true })
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC26_No_stock_quantity_control_exists_on_the_product_or_variant_rows()
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();

        var stockControls = Page.GetByLabel(new Regex("stock", RegexOptions.IgnoreCase));
        (await stockControls.CountAsync()).Should().BeGreaterThan(0,
            "RULE-6/RULE-8/RULE-14/FR-8 require a stock-quantity field on the product (and one per " +
            "variant once option types exist), rejecting a negative or fractional value; this build " +
            "renders no such field at all — stock_quantity was dropped from products and " +
            "product_variants by migration 0026_remove_product_stock.sql (see .specs/create-product/status.md)");
    }

    [Fact]
    public async Task AC29_A_published_product_whose_every_variant_is_unavailable_shows_out_of_stock()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.UploadImagesAsync(ProductImageFixtures.Png("e2e-a.png"));
        await add.Form.ImageRows.First.WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.AddOptionTypeAsync();
        await add.Form.FillOptionTypeNameAsync(0, "Flavour");
        await add.Form.FillOptionValueAsync(0, "Mango");
        await add.Form.VariantRow("Mango").WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.SetVariantPriceAsync("Mango", 19.99m);
        await add.Form.ToggleVariantAvailabilityAsync("Mango", currentlyAvailable: true);
        await add.Form.VariantAvailabilityChip("Mango", available: false).WaitForAsync(new() { Timeout = 15_000 });

        await add.Form.SetPublishedAsync();
        await add.Form.SaveAsync();
        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);

        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();
        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Category);
        await catalogue.ToggleFilterOptionAsync(SeededCatalogue.Category);
        await catalogue.ProductName(name).WaitForAsync(new() { Timeout = 15_000 });
        await Page.GetByText(Strings.OutOfStock, new() { Exact = true }).First
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC30_Unpublishing_hides_a_product_from_customers_while_keeping_it_listed_for_staff()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        await CreateSimpleProductAsync(name, price: 15.00m, published: true);

        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();
        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Category);
        await catalogue.ToggleFilterOptionAsync(SeededCatalogue.Category);
        await catalogue.ProductName(name).WaitForAsync(new() { Timeout = 15_000 });

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();
        await manageProducts.GotoEditProductAsync(name);
        var edit = new EditProductPage(Page);
        await edit.Form.SetUnpublishedAsync();
        await edit.Form.SaveAsync();
        await manageProducts.Snackbar
            .Filter(new() { HasText = string.Format(Strings.Product_Updated, name) })
            .First.WaitForAsync(new() { Timeout = 15_000 });

        await manageProducts.GotoAsync();
        await manageProducts.StatusChip(name, active: false).WaitForAsync(new() { Timeout = 15_000 });

        await catalogue.GotoAsync();
        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Category);
        await catalogue.ToggleFilterOptionAsync(SeededCatalogue.Category);
        await Assertions.Expect(catalogue.ProductName(name)).ToHaveCountAsync(0, new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC31_Leaving_the_form_with_unsaved_changes_warns_before_discarding_them()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);

        await add.Form.CancelButton.ClickAsync();

        var confirm = new ConfirmDialogPage(Page);
        await confirm.WaitForOpenAsync();
        await confirm.Title(Strings.ProductForm_UnsavedChangesTitle).WaitForAsync(new() { Timeout = 15_000 });

        await confirm.CancelAsync();
        await confirm.WaitForClosedAsync();
        await Assertions.Expect(add.Form.NameField).ToHaveValueAsync(name, new() { Timeout = 15_000 });

        await add.Form.CancelButton.ClickAsync();
        await confirm.WaitForOpenAsync();
        await confirm.ConfirmAsync(Strings.ProductForm_LeaveAnyway);
        await Page.WaitForURLAsync(
            url => url.Contains(WebRoutes.Admin.ManageProducts, StringComparison.Ordinal)
                && !url.Contains(WebRoutes.Admin.AddProduct, StringComparison.Ordinal),
            new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC34_A_stale_edit_link_shows_not_found_while_a_renamed_products_link_still_opens_it()
    {
        var originalName = $"e2e-product-{Guid.NewGuid():N}";
        var renamedTo = $"e2e-product-{Guid.NewGuid():N}";
        var productId = await CreateSimpleProductAsync(originalName, price: 10.00m, published: false);

        var edit = new EditProductPage(Page);
        await edit.GotoAsync(Guid.NewGuid());
        await edit.NotFoundTitle.WaitForAsync(new() { Timeout = 15_000 });
        await edit.BackToListLink.WaitForAsync(new() { Timeout = 15_000 });

        await edit.GotoAsync(productId);
        await edit.Form.FillNameAsync(renamedTo);
        await edit.Form.SaveAsync();
        await WaitForReturnToListAsync("/edit");

        await edit.GotoAsync(productId);
        await Assertions.Expect(edit.Form.NameField).ToHaveValueAsync(renamedTo, new() { Timeout = 15_000 });
    }

    // ---- Shared helpers ------------------------------------------------------------------------

    private async Task<Guid> CreateSimpleProductAsync(string name, decimal price, bool published)
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SetPriceAsync(price);
        await add.Form.UploadImagesAsync(ProductImageFixtures.Png("e2e-a.png"));
        await add.Form.ImageRows.First.WaitForAsync(new() { Timeout = 15_000 });

        if (published)
            await add.Form.SetPublishedAsync();

        await add.Form.SaveAsync();
        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.Row(name).WaitForAsync(new() { Timeout = 15_000 });
        return await manageProducts.GetProductIdAsync(name);
    }

    /// <summary>
    /// Waits for a form to hand back to the product list, identified by the list route without
    /// the form's own segment still in the URL — the same pattern manage-categories uses.
    /// </summary>
    private Task WaitForReturnToListAsync(string formSegment) =>
        Page.WaitForURLAsync(
            url => url.Contains(WebRoutes.Admin.ManageProducts, StringComparison.Ordinal)
                && !url.Contains(formSegment, StringComparison.Ordinal),
            new() { Timeout = 15_000 });
}

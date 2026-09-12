using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// Product Content admin journeys (.specs/product-description/spec.md §6): the rich-text
/// description and the ordered specification rows added to the product form's new Content card.
/// Every AC bucketed <c>e2e</c> in <c>.specs/product-description/e2e-manifest.json</c> is one
/// method here; the remaining ACs are already proven below the browser (see that manifest's
/// <c>unit</c> entries, corroborated by <c>.specs/product-description/test-manifest.json</c>'s
/// stubbed-repository handler/validator/mapper coverage). AC-11 is the one exception worth
/// flagging: it is bucketed here rather than <c>unit</c> because migration
/// <c>0030_product_description_bounds</c> (plan §10, TASK-023) was never authored — the real local
/// Supabase project this app runs against carries no <c>products_description_size</c> /
/// <c>products_description_markup_allowed</c> CHECK, so only a call against that real database (not
/// <c>SupabaseProductDescriptionSchemaTests</c>'s own simulated Testcontainers schema) proves
/// anything about the deployed system. Shared-database discipline: every product
/// this journey creates uses a unique generated name, so reruns never collide; there is no cleanup
/// step since these are harmless "e2e-product-" rows on a stack that gets a full
/// <c>supabase db reset</c> between sessions. Reuses <c>create-product</c>'s seeded catalogue rows
/// (<see cref="CreateProductJourneyTests"/>'s <c>SeededCatalogue</c>) rather than duplicating them.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "product-description")]
public sealed class ProductDescriptionJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    [Fact]
    public async Task AC1_Creating_with_every_supported_format_plus_specification_rows_reopens_with_the_same_content()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SetPriceAsync(19.99m); // unrelated to this AC; the form requires a price to enable Save

        await add.Form.DescriptionEditor.WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.DescriptionEditor.ClickAsync();

        // FR-1's whole format list, one segment per block: heading, plain paragraph, bold,
        // italic, ordered list, and a bulleted item carrying a link.
        await add.Form.ApplyHeadingAsync(2);
        await Page.Keyboard.TypeAsync("Heading line");
        await Page.Keyboard.PressAsync("Enter"); // Quill drops the header format for the new line by default.

        await Page.Keyboard.TypeAsync("Plain paragraph text.");
        await Page.Keyboard.PressAsync("Enter");

        await add.Form.ApplyBoldAsync();
        await Page.Keyboard.TypeAsync("Bold segment");
        await add.Form.ApplyBoldAsync();
        await Page.Keyboard.PressAsync("Enter");

        await add.Form.ApplyItalicAsync();
        await Page.Keyboard.TypeAsync("Italic segment");
        await add.Form.ApplyItalicAsync();
        await Page.Keyboard.PressAsync("Enter");

        await add.Form.ApplyOrderedListAsync();
        await Page.Keyboard.TypeAsync("Ordered item");
        await Page.Keyboard.PressAsync("Enter"); // list is a block format and persists to the new line; no toggle-off needed, matching the bullet-list block below

        await add.Form.ApplyBulletListAsync();
        await Page.Keyboard.TypeAsync("Bullet item with a link");
        await Page.Keyboard.PressAsync("Home");
        await Page.Keyboard.PressAsync("Shift+End");
        await add.Form.ApplyLinkAsync("https://example.com/vape-care");

        await add.Form.AddSpecificationRowAsync();
        await add.Form.FillSpecificationNameAsync(0, "Material");
        await add.Form.FillSpecificationValueAsync(0, "Stainless steel");
        await add.Form.AddSpecificationRowAsync();
        await add.Form.FillSpecificationNameAsync(1, "Capacity");
        await add.Form.FillSpecificationValueAsync(1, "750 ml");

        await add.Form.SaveAsync();
        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();
        await manageProducts.GotoEditProductAsync(name);

        var edit = new EditProductPage(Page);
        await Assertions.Expect(edit.Form.NameField).ToHaveValueAsync(name, new() { Timeout = 15_000 });
        var editor = edit.Form.DescriptionEditor;
        await editor.WaitForAsync(new() { Timeout = 15_000 });

        await editor.Locator("h2").Filter(new() { HasText = "Heading line" })
            .WaitForAsync(new() { Timeout = 15_000 });
        await editor.Locator("p").Filter(new() { HasText = "Plain paragraph text." })
            .WaitForAsync(new() { Timeout = 15_000 });
        await editor.Locator("strong").Filter(new() { HasText = "Bold segment" })
            .WaitForAsync(new() { Timeout = 15_000 });
        await editor.Locator("em").Filter(new() { HasText = "Italic segment" })
            .WaitForAsync(new() { Timeout = 15_000 });
        await editor.Locator("li[data-list='ordered']").Filter(new() { HasText = "Ordered item" })
            .WaitForAsync(new() { Timeout = 15_000 });
        // Quill 2.x renders every list — ordered or bullet — as an <ol>, distinguishing them only
        // by data-list on the <li> (CSS then draws the bullet glyph); there is never a real <ul>.
        await editor.Locator("li[data-list='bullet'] a[href='https://example.com/vape-care']")
            .Filter(new() { HasText = "Bullet item with a link" })
            .WaitForAsync(new() { Timeout = 15_000 });

        await Assertions.Expect(edit.Form.SpecificationNameField(0)).ToHaveValueAsync("Material", new() { Timeout = 15_000 });
        await Assertions.Expect(edit.Form.SpecificationValueField(0)).ToHaveValueAsync("Stainless steel", new() { Timeout = 15_000 });
        await Assertions.Expect(edit.Form.SpecificationNameField(1)).ToHaveValueAsync("Capacity", new() { Timeout = 15_000 });
        await Assertions.Expect(edit.Form.SpecificationValueField(1)).ToHaveValueAsync("750 ml", new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC2_Editing_description_and_specification_rows_reopens_with_the_edits_and_leaves_unrelated_details_unchanged()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SetPriceAsync(24.99m);

        await add.Form.DescriptionEditor.WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.DescriptionEditor.ClickAsync();
        await Page.Keyboard.TypeAsync("Original description text.");

        await add.Form.AddSpecificationRowAsync();
        await add.Form.FillSpecificationNameAsync(0, "Material");
        await add.Form.FillSpecificationValueAsync(0, "Steel");

        await add.Form.SaveAsync();
        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();
        await manageProducts.GotoEditProductAsync(name);
        var edit = new EditProductPage(Page);
        await Assertions.Expect(edit.Form.NameField).ToHaveValueAsync(name, new() { Timeout = 15_000 });
        await edit.Form.DescriptionEditor.Locator("p").Filter(new() { HasText = "Original description text." })
            .WaitForAsync(new() { Timeout = 15_000 });

        // Edit: replace the description, edit the existing row's value, add a second row.
        await edit.Form.DescriptionEditor.ClickAsync();
        await Page.Keyboard.PressAsync("Control+A");
        await Page.Keyboard.PressAsync("Delete");
        await Page.Keyboard.TypeAsync("Updated description text.");

        await edit.Form.FillSpecificationValueAsync(0, "Stainless steel");
        await edit.Form.AddSpecificationRowAsync();
        await edit.Form.FillSpecificationNameAsync(1, "Capacity");
        await edit.Form.FillSpecificationValueAsync(1, "750 ml");

        await edit.Form.SaveAsync();
        await manageProducts.Snackbar
            .Filter(new() { HasText = string.Format(Strings.Product_Updated, name) })
            .First.WaitForAsync(new() { Timeout = 15_000 });

        await manageProducts.GotoAsync();
        await manageProducts.GotoEditProductAsync(name);
        var reopened = new EditProductPage(Page);
        await Assertions.Expect(reopened.Form.NameField).ToHaveValueAsync(name, new() { Timeout = 15_000 });
        await reopened.Form.DescriptionEditor.Locator("p").Filter(new() { HasText = "Updated description text." })
            .WaitForAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(reopened.Form.SpecificationNameField(0)).ToHaveValueAsync("Material", new() { Timeout = 15_000 });
        await Assertions.Expect(reopened.Form.SpecificationValueField(0)).ToHaveValueAsync("Stainless steel", new() { Timeout = 15_000 });
        await Assertions.Expect(reopened.Form.SpecificationNameField(1)).ToHaveValueAsync("Capacity", new() { Timeout = 15_000 });
        await Assertions.Expect(reopened.Form.SpecificationValueField(1)).ToHaveValueAsync("750 ml", new() { Timeout = 15_000 });
        // Unrelated saved detail (price) survives a content-only edit unchanged (FR-5).
        await Assertions.Expect(reopened.Form.PriceField).ToHaveValueAsync("24.99", new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC6_Removing_all_description_text_and_every_specification_row_persists_as_empty_after_reopening()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SetPriceAsync(19.99m); // unrelated to this AC; the form requires a price to enable Save

        await add.Form.DescriptionEditor.WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.DescriptionEditor.ClickAsync();
        await Page.Keyboard.TypeAsync("Text to remove.");
        await add.Form.AddSpecificationRowAsync();
        await add.Form.FillSpecificationNameAsync(0, "Material");
        await add.Form.FillSpecificationValueAsync(0, "Steel");

        await add.Form.SaveAsync();
        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();
        await manageProducts.GotoEditProductAsync(name);
        var edit = new EditProductPage(Page);
        await Assertions.Expect(edit.Form.NameField).ToHaveValueAsync(name, new() { Timeout = 15_000 });

        await edit.Form.DescriptionEditor.ClickAsync();
        await Page.Keyboard.PressAsync("Control+A");
        await Page.Keyboard.PressAsync("Delete");
        await edit.Form.RemoveSpecificationRowAsync(0);

        await edit.Form.SaveAsync();
        await manageProducts.Snackbar
            .Filter(new() { HasText = string.Format(Strings.Product_Updated, name) })
            .First.WaitForAsync(new() { Timeout = 15_000 });

        await manageProducts.GotoAsync();
        await manageProducts.GotoEditProductAsync(name);
        var reopened = new EditProductPage(Page);
        await Assertions.Expect(reopened.Form.NameField).ToHaveValueAsync(name, new() { Timeout = 15_000 });
        await reopened.Form.DescriptionEditor.WaitForAsync(new() { Timeout = 15_000 });
        // Quill always renders at least one line, so its innerText for an empty document is "\n" (also
        // what quill.getText() returns) rather than "" — Trim() before comparing, mirroring the Decision 5
        // counting rule the Domain applies to reach the same 0-length verdict.
        (await reopened.Form.DescriptionEditor.InnerTextAsync()).Trim().Should().BeEmpty(
            "RULE-2: empty description is valid and must persist as empty after reopening");
        await reopened.Form.SpecificationEmptyStateTitle.WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC10_Pasting_styled_text_keeps_supported_emphasis_drops_unsupported_styling_and_shows_the_notice()
    {
        const string pastedHtml =
            "<p><strong>Bold kept</strong> and <span style=\"color:red;font-family:'Comic Sans MS'\">styled text dropped</span></p>";

        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.DescriptionEditor.WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.DescriptionEditor.ClickAsync();

        await add.Form.PasteIntoDescriptionAsync(pastedHtml);

        await add.Form.DescriptionFormattingRemovedNotice.WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.DescriptionEditor.Locator("strong").Filter(new() { HasText = "Bold kept" })
            .WaitForAsync(new() { Timeout = 15_000 });
        (await add.Form.DescriptionEditor.InnerHTMLAsync()).Should().NotContain("style=",
            "FR-7/RULE-5: unsupported inline styling must be dropped; only readable text and supported formatting remain");
        await add.Form.DescriptionEditor.GetByText("styled text dropped").WaitForAsync(new() { Timeout = 15_000 });

        var name = $"e2e-product-{Guid.NewGuid():N}";
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SetPriceAsync(19.99m); // unrelated to this AC; the form requires a price to enable Save
        await add.Form.SaveAsync();
        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();
        await manageProducts.GotoEditProductAsync(name);
        var edit = new EditProductPage(Page);
        await Assertions.Expect(edit.Form.NameField).ToHaveValueAsync(name, new() { Timeout = 15_000 });
        await edit.Form.DescriptionEditor.Locator("strong").Filter(new() { HasText = "Bold kept" })
            .WaitForAsync(new() { Timeout = 15_000 });
        (await edit.Form.DescriptionEditor.InnerHTMLAsync()).Should().NotContain("style=");
    }

    /// <summary>
    /// Plan Decision 4: Domain, Application, and Infrastructure all run client-side under Blazor
    /// WebAssembly, so no C# check is a trust boundary — only the <c>products_description_size</c> /
    /// <c>products_description_markup_allowed</c> database <c>CHECK</c>s are. TASK-022 asks for
    /// exactly this proof: call <c>save_product</c> directly over the network, bypassing
    /// <c>ProductForm</c> and every C# validator, and confirm the database itself rejects markup
    /// outside the Decision 3 grammar. Bucketed <c>e2e</c> (not <c>unit</c>) because
    /// <c>SupabaseProductDescriptionSchemaTests</c> proves this SQL is *correct* against a schema it
    /// builds itself in an isolated Testcontainers Postgres — not that the real local Supabase
    /// project this app actually runs against has that <c>CHECK</c> applied.
    /// </summary>
    [Fact]
    public async Task AC11_Calling_save_product_directly_with_markup_outside_the_grammar_is_rejected_and_leaves_no_row_changed()
    {
        var name = $"e2e-product-{Guid.NewGuid():N}";
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.FillNameAsync(name);
        await add.Form.SelectCategoryAsync(SeededCatalogue.Category);
        await add.Form.SelectBrandAsync(SeededCatalogue.Brand);
        await add.Form.SetPriceAsync(19.99m); // unrelated to this AC; the form requires a price to enable Save
        await add.Form.DescriptionEditor.WaitForAsync(new() { Timeout = 15_000 });
        await add.Form.DescriptionEditor.ClickAsync();
        await Page.Keyboard.TypeAsync("Safe description.");
        await add.Form.SaveAsync();
        await WaitForReturnToListAsync(WebRoutes.Admin.AddProduct);

        var manageProducts = new ManageProductsPage(Page);
        await manageProducts.GotoAsync();
        var productId = await manageProducts.GetProductIdAsync(name);

        var apiUrl = E2EEnvironment.Get("API_URL");
        var anonKey = E2EEnvironment.Get("ANON_KEY");
        var accessToken = await GetAccessTokenAsync();

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("apikey", anonKey);
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        var beforeJson = await http.GetStringAsync(
            $"{apiUrl}/rest/v1/products?id=eq.{productId}&select=*");
        var beforeRow = JsonNode.Parse(beforeJson)!.AsArray()[0]!.AsObject();
        var originalDescription = beforeRow["description"]!.GetValue<string>();

        var product = beforeRow.DeepClone()!.AsObject();
        product["expected_updated_at"] = product["updated_at"]!.GetValue<string>();
        // FR-7/RULE-5: an attribute an allowed tag never carries — this is exactly the shape
        // Quill's own clipboard matchers already refuse client-side; only the database CHECK
        // stands between this payload and a stored row once the client is bypassed entirely.
        product["description"] = "<img src=x onerror=\"alert(document.cookie)\">";

        // PostgREST's RPC endpoint maps top-level body keys to the function's named parameters;
        // save_product(payload JSONB) takes exactly one, named "payload", so the argument object
        // must be nested under that key, not spread across the body's own top level.
        var payload = new JsonObject { ["payload"] = new JsonObject { ["mode"] = "update", ["product"] = product } };
        using var content = new StringContent(payload.ToJsonString());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var rpcResponse = await http.PostAsync($"{apiUrl}/rest/v1/rpc/save_product", content);

        var afterJson = await http.GetStringAsync(
            $"{apiUrl}/rest/v1/products?id=eq.{productId}&select=description");
        var afterDescription = JsonNode.Parse(afterJson)!.AsArray()[0]!["description"]!.GetValue<string>();

        rpcResponse.IsSuccessStatusCode.Should().BeFalse(
            "RULE-5/AC-11: the database CHECK is the real trust boundary (plan Decision 4) — a " +
            "direct save_product call carrying markup outside the Decision 3 grammar must be " +
            "rejected even with every client-side check bypassed");
        afterDescription.Should().Be(originalDescription,
            "a rejected save must leave the stored row unchanged (RULE-6) — supplied code must never become storable");
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var raw = await Page.EvaluateAsync<string>("localStorage.getItem('shop.auth.session')");
        using var doc = JsonDocument.Parse(raw ?? throw new InvalidOperationException(
            "No 'shop.auth.session' entry in localStorage — the persona is not signed in yet."));
        return doc.RootElement.GetProperty("AccessToken").GetString()!;
    }

    [Fact]
    public async Task AC13_Formatting_and_specification_controls_are_keyboard_operable_and_row_removal_moves_focus_and_announces()
    {
        var add = new AddProductPage(Page);
        await add.GotoAsync();
        await add.Form.DescriptionEditor.WaitForAsync(new() { Timeout = 15_000 });

        // Keyboard-only: focus the editor and apply bold via its documented shortcut, never a
        // mouse click on the toolbar.
        await add.Form.DescriptionEditor.FocusAsync();
        await Assertions.Expect(add.Form.DescriptionEditor).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await Page.Keyboard.PressAsync("Control+B");
        await Page.Keyboard.TypeAsync("Keyboard bold");
        await add.Form.DescriptionEditor.Locator("strong").Filter(new() { HasText = "Keyboard bold" })
            .WaitForAsync(new() { Timeout = 15_000 });

        // Add one row via keyboard, then remove it; focus must land back on "Add Specification"
        // (plan Decision 11 — the removed row was last) and the removal must be announced.
        await add.Form.AddSpecificationButton.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await add.Form.SpecificationNameField(0).FocusAsync();
        await Page.Keyboard.TypeAsync("Material");
        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.TypeAsync("Steel");

        await add.Form.SpecificationRemoveButton(0).FocusAsync();
        await Assertions.Expect(add.Form.SpecificationRemoveButton(0)).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await Page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(add.Form.AddSpecificationButton).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await Page.GetByText(Strings.ProductSpecifications_RowRemovedAnnouncement, new() { Exact = true })
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    /// <summary>
    /// Waits for a form to hand back to the product list, identified by the list route without
    /// the form's own segment still in the URL — the same pattern <c>CreateProductJourneyTests</c> uses.
    /// </summary>
    private Task WaitForReturnToListAsync(string formSegment) =>
        Page.WaitForURLAsync(
            url => url.Contains(WebRoutes.Admin.ManageProducts, StringComparison.Ordinal)
                && !url.Contains(formSegment, StringComparison.Ordinal),
            new() { Timeout = 15_000 });
}

using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

[Trait("Category", "E2E")]
[Trait("Feature", "native-ui")]
public sealed class NativeAdminProductLayoutJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    private const string UserId = "db8d270f-b94e-4a29-bda1-cf95646d1470";
    private const string ProductId = "db8d270f-b94e-4a29-bda1-cf95646d1471";
    private const string CategoryId = "db8d270f-b94e-4a29-bda1-cf95646d1472";
    private const string BrandId = "db8d270f-b94e-4a29-bda1-cf95646d1473";
    private const string Email = "product-probe@example.test";
    private readonly List<string> _unexpected = [];
    private bool _empty;

    [Theory]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    [InlineData(834, true)]
    [InlineData(390, true)]
    public async Task Products_ListFormPickerAndEdit_PreserveNativeResponsiveWorkflows(int width, bool withoutVendorCss)
    {
        var releaseList = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEdit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var browserErrors = new List<string>();
        Page.PageError += (_, error) => browserErrors.Add(error);
        await InterceptAsync(releaseList.Task, releaseEdit.Task);
        try
        {
            await Page.SetViewportSizeAsync(width, 1000);
            await SignInAsync();
            var table = Page.Locator(".shop-products-table");
            await Assertions.Expect(table.Locator(".shop-table-loading-row")).ToHaveCountAsync(10);
            await PrepareAppearanceAsync(withoutVendorCss);
            await SaveAsync($"products-list-loading-{width}-{withoutVendorCss}");
            releaseList.TrySetResult();
            await Assertions.Expect(table.Locator(".shop-table-loading-row")).ToHaveCountAsync(0);
            var surface = Page.Locator(".shop-products-page");
            await Assertions.Expect(surface.Locator("h1")).ToHaveCSSAsync("font-size", "60px");
            await Assertions.Expect(surface.Locator("[class*='mud-']")).ToHaveCountAsync(0);
            await Assertions.Expect(surface.Locator(".shop-expander-trigger[aria-expanded=true]")).ToHaveCountAsync(0);
            if (width >= 1024)
            {
                var headers = await table.Locator("th").AllAsync();
                var statusHeader = (await headers[^2].BoundingBoxAsync())!;
                var actionHeader = (await headers[^1].BoundingBoxAsync())!;
                statusHeader.Width.Should().BeApproximately(120, 1);
                (statusHeader.X + statusHeader.Width).Should().BeLessThanOrEqualTo(actionHeader.X + 1);
                await surface.Locator("aside").GetByRole(AriaRole.Button, new() { Name = Strings.Filter_Price, Exact = true }).ClickAsync();
                var rangeFields = await surface.Locator("aside .shop-range-inputs .shop-field").AllAsync();
                (await rangeFields[0].BoundingBoxAsync())!.Y.Should().BeApproximately((await rangeFields[1].BoundingBoxAsync())!.Y, 1);
            }
            if (width < 1024)
            {
                await Page.GetByRole(AriaRole.Button, new() { Name = Strings.Products_Filters, Exact = true }).ClickAsync();
                var drawer = Page.Locator("dialog[open]");
                await Assertions.Expect(drawer).ToBeVisibleAsync();
                (await drawer.BoundingBoxAsync())!.Width.Should().BeApproximately(width < 768 ? width : 400, 1);
                await Assertions.Expect(drawer.Locator(".shop-products-filter")).ToHaveCSSAsync("padding", "16px");
                await Assertions.Expect(drawer.Locator(".shop-expander-trigger[aria-expanded=false]")).ToHaveCountAsync(4);
                await drawer.GetByRole(AriaRole.Button, new() { Name = Strings.Filter_Status, Exact = true }).ClickAsync();
                await drawer.GetByRole(AriaRole.Checkbox, new() { Name = Strings.ManageProducts_StatusInactive, Exact = true }).CheckAsync();
                await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("status=inactive"));
                await SaveAsync($"products-filter-{width}");
                await drawer.GetByRole(AriaRole.Button, new() { Name = Strings.Filter_ClearAll, Exact = true }).ClickAsync();
                await Page.Keyboard.PressAsync("Escape");
                await Assertions.Expect(drawer).ToHaveCountAsync(0);
            }
            await table.Locator("tbody input[type=checkbox]").First.CheckAsync();
            await Assertions.Expect(Page.Locator(".shop-bulk-action-bar")).ToBeVisibleAsync();
            await SaveAsync($"products-list-{width}-{withoutVendorCss}");
            await Page.GetByRole(AriaRole.Button, new() { Name = Strings.Close_BulkActionBar, Exact = true }).ClickAsync();
            await Page.Locator("input[type=search]").FillAsync("Vape");
            await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("search=Vape"));
            await Page.GetByRole(AriaRole.Button, new() { Name = Strings.Pagination_Next, Exact = true }).ClickAsync();
            await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("page=2"));
            await AssertNoOverflowAsync();

            await Page.GetByRole(AriaRole.Link, new() { Name = Strings.AddProduct_Heading, Exact = true }).ClickAsync();
            await Page.GetByTestId("product-name").WaitForAsync();
            await Assertions.Expect(surface.Locator("h1")).ToHaveTextAsync(Strings.ProductForm_CreateHeading);
            await Assertions.Expect(surface.Locator(".shop-expander-title > h2.shop-product-section-title")).ToHaveCountAsync(3);
            await SaveAsync($"products-create-empty-{width}-{withoutVendorCss}");
            await Page.GetByTestId("product-save").ClickAsync();
            await Assertions.Expect(Page.GetByTestId("product-name")).ToHaveAttributeAsync("aria-invalid", "true");
            await Assertions.Expect(surface.Locator(".validation-summary-errors")).ToHaveCountAsync(0);
            await Assertions.Expect(Page.GetByTestId("notification-dismiss")).ToHaveCountAsync(0);
            await Page.GetByTestId("product-name").FillAsync("Vape Pen 500");
            await Assertions.Expect(Page.GetByTestId("product-sku")).Not.ToHaveValueAsync("");
            await Assertions.Expect(Page.GetByTestId("product-name")).Not.ToHaveAttributeAsync("aria-invalid", "true");
            await SelectAsync(Strings.AddProduct_BrandLabel, "Puffco");
            await SelectAsync(Strings.AddProduct_CategoryLabel, "Vape Pen");
            await Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductSpecifications_AddRow, Exact = true }).ClickAsync();
            await Page.Locator(".shop-product-specifications").GetByLabel(Strings.ProductSpecifications_NameLabel, new() { Exact = true }).FillAsync("Material");
            await Page.Locator(".shop-product-specifications").GetByLabel(Strings.ProductSpecifications_ValueLabel, new() { Exact = true }).FillAsync("Steel");
            await Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_AddVariant, Exact = true }).ClickAsync();
            await Page.GetByLabel(Strings.ProductVariants_OptionNameLabel, new() { Exact = true }).FillAsync("Color");
            await Page.Locator(".shop-product-variants").GetByLabel(Strings.ProductVariants_OptionValuesLabel, new() { Exact = true }).FillAsync("Red");
            await Assertions.Expect(Page.Locator(".shop-product-variant-table tbody tr")).ToHaveCountAsync(1);
            await Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_PinImage, Exact = true }).ClickAsync();
            var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = Strings.VariantImage_Title });
            await Assertions.Expect(Page.GetByTestId("variant-image-cancel")).ToBeFocusedAsync();
            await Assertions.Expect(dialog.Locator(".shop-file-dropzone-trigger")).ToBeVisibleAsync();
            await SaveAsync($"products-picker-empty-{width}");
            var chooser = Page.WaitForFileChooserAsync();
            await dialog.Locator(".shop-file-dropzone-trigger").FocusAsync();
            await Page.Keyboard.PressAsync("Enter");
            await (await chooser).SetFilesAsync(new[] { Png("picker-one.png"), Png("picker-two.png") });
            var options = dialog.Locator(".shop-image-tile-surface");
            await Assertions.Expect(options).ToHaveCountAsync(2);
            await Assertions.Expect(options.First).ToHaveAttributeAsync("aria-pressed", "true");
            (await dialog.Locator(".shop-variant-image-option").First.BoundingBoxAsync())!.Width.Should().BeApproximately(132, 1);
            await Assertions.Expect(dialog.Locator("input[type=file]")).ToHaveValueAsync("");
            await Page.GetByTestId("variant-image-cancel").ClickAsync();
            var upload = surface.Locator(".shop-image-upload");
            await Assertions.Expect(upload.Locator(".shop-image-upload-item")).ToHaveCountAsync(2);
            await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_PinImage, Exact = true })).ToBeVisibleAsync();

            await Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_PinImage, Exact = true }).ClickAsync();
            await options.First.ClickAsync();
            await SaveAsync($"products-picker-populated-{width}");
            await Page.GetByTestId("variant-image-save").ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_ChangeImage, Exact = true })).ToBeVisibleAsync();
            await upload.Locator(".shop-image-upload-item").First.HoverAsync();
            await upload.Locator(".shop-image-tile-remove").First.ClickAsync();
            await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_PinImage, Exact = true })).ToBeVisibleAsync();
            await SaveAsync($"products-create-populated-{width}-{withoutVendorCss}");
            await SaveComponentAsync(surface.Locator(".shop-product-general"), $"products-general-{width}-{withoutVendorCss}");
            await SaveComponentAsync(surface.Locator(".shop-product-option").First, $"products-options-{width}-{withoutVendorCss}");
            await AssertNoOverflowAsync();
            if (width == 1440 && withoutVendorCss)
            {
                for (var index = 1; index <= 24; index++)
                {
                    await Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_AddValue, Exact = true }).ClickAsync();
                    await Page.Locator(".shop-product-option-values input").Last.FillAsync($"Color{index}");
                }
                var virtualScroll = Page.Locator(".shop-table-scroll-virtualized");
                await virtualScroll.ScrollIntoViewIfNeededAsync();
                await virtualScroll.EvaluateAsync("el => el.scrollTop=el.scrollHeight");
                var lastPrice = Page.GetByRole(AriaRole.Textbox, new() { Name = "Color24 " + Strings.ProductVariants_ColumnPrice, Exact = true });
                await lastPrice.FillAsync("28.75");
                await lastPrice.PressAsync("Tab");
                await virtualScroll.EvaluateAsync("el => el.scrollTop=0");
                await Assertions.Expect(lastPrice).ToHaveCountAsync(0);
                await virtualScroll.EvaluateAsync("el => el.scrollTop=el.scrollHeight");
                await Assertions.Expect(lastPrice).ToHaveValueAsync("$28.75");
                await lastPrice.FocusAsync();
                await Assertions.Expect(lastPrice).ToHaveValueAsync("28.75");
                await lastPrice.FillAsync("bad");
                await lastPrice.PressAsync("Tab");
                await virtualScroll.EvaluateAsync("el => el.scrollTop=0");
                await Assertions.Expect(lastPrice).ToHaveCountAsync(0);
                await virtualScroll.EvaluateAsync("el => el.scrollTop=el.scrollHeight");
                await Assertions.Expect(lastPrice).ToHaveValueAsync("bad");
                await Assertions.Expect(lastPrice).ToHaveAttributeAsync("aria-invalid", "true");
                await SaveAsync("products-virtualized-draft");
            }
            if (width == 390)
            {
                foreach (var resizedWidth in new[] { 320, 767, 768, 1023, 1024 })
                {
                    await Page.SetViewportSizeAsync(resizedWidth, 1000);
                    await AssertNoOverflowAsync();
                }
                await Page.SetViewportSizeAsync(width, 1000);
                await Page.EvaluateAsync("document.documentElement.style.fontSize='200%'");
                await AssertNoOverflowAsync();
                await SaveAsync("products-create-large-text");
                await Page.EvaluateAsync("document.documentElement.style.fontSize=''");
            }
            await surface.GetByRole(AriaRole.Link, new() { Name = Strings.Cancel, Exact = true }).ClickAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductForm_LeaveAnyway, Exact = true }).ClickAsync();
            await table.WaitForAsync();
            await Page.Locator(".shop-table-scroll").EvaluateAsync("el => el.scrollLeft=el.scrollWidth");
            await Page.GetByRole(AriaRole.Link, new() { Name = string.Format(Strings.ManageProducts_EditAria, "Vape Pen 1"), Exact = true }).ClickAsync();
            await Assertions.Expect(surface.Locator(".shop-product-form-skeleton")).ToBeVisibleAsync();
            await SaveAsync($"products-edit-loading-{width}");
            releaseEdit.TrySetResult();
            await Assertions.Expect(Page.GetByTestId("product-name")).ToHaveValueAsync("Vape Pen 500");
            await Assertions.Expect(surface.Locator(".shop-product-specification-list input")).ToHaveCountAsync(4);
            await Assertions.Expect(surface.Locator(".shop-image-upload-item")).ToHaveCountAsync(2);
            await SaveAsync($"products-edit-{width}-{withoutVendorCss}");
            await AssertNoOverflowAsync();
            await surface.GetByRole(AriaRole.Link, new() { Name = Strings.Cancel, Exact = true }).ClickAsync();
            await table.WaitForAsync();
            _empty = true;
            await Page.Locator(".shop-table-scroll").EvaluateAsync("el => el.scrollLeft=el.scrollWidth");
            await Page.GetByRole(AriaRole.Link, new() { Name = string.Format(Strings.ManageProducts_EditAria, "Vape Pen 1"), Exact = true }).ClickAsync();
            await Assertions.Expect(surface.Locator("h2")).ToHaveTextAsync(Strings.EditProduct_NotFoundTitle);
            await surface.GetByRole(AriaRole.Link, new() { Name = Strings.EditProduct_BackToList, Exact = true }).ClickAsync();
            await Assertions.Expect(surface.Locator("h2")).ToHaveTextAsync(Strings.ManageProducts_EmptyTitle);
            await SaveAsync($"products-list-empty-{width}");
            await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
            _unexpected.Should().BeEmpty();
            browserErrors.Should().BeEmpty();
        }
        finally { releaseList.TrySetResult(); releaseEdit.TrySetResult(); }
    }

    private Task InterceptAsync(Task releaseList, Task releaseEdit) => Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
    {
        var uri = new Uri(route.Request.Url);
        var path = uri.AbsolutePath;
        string? body = null;
        if (path.Contains("/storage/v1/") && route.Request.Method == "GET")
        {
            await route.FulfillAsync(new() { ContentType = "image/svg+xml", Body = "<svg xmlns='http://www.w3.org/2000/svg' width='132' height='132'><rect width='132' height='132' fill='#e8e8e8'/><rect x='60' y='16' width='12' height='100' rx='3' fill='#252525'/></svg>" });
            return;
        }
        if (path.EndsWith("/auth/v1/otp") && route.Request.Method == "POST") body = "{}";
        else if (path.EndsWith("/rpc/customer_exists")) body = "true";
        else if (path.EndsWith("/auth/v1/verify")) body = FakeSession();
        else if (path.EndsWith("/rest/v1/customers") && route.Request.Method == "GET")
            body = JsonSerializer.Serialize(new[] { new { id = UserId, email = Email, first_name = "Product", last_name = "Probe", date_of_birth = "1990-01-01", created_at = "2026-01-01T00:00:00Z" } });
        else if (path.EndsWith("/rpc/admin_products_page"))
        {
            await releaseList;
            body = JsonSerializer.Serialize(_empty ? [] : Enumerable.Range(1, 10).Select(i => new { id = i == 1 ? ProductId : $"ab09dac2-bc69-4e50-8f1a-49c98a963b{i:00}", name = $"Vape Pen {i}", sku = $"VAPE-{i}", brand_id = BrandId, brand_name = "Puffco", category_id = CategoryId, category_name = "Vape Pen", currency = "CAD", is_published = i % 2 == 0, variant_count = 4, min_price = 20, max_price = 25, total_count = 20 }).ToArray());
        }
        else if (path.EndsWith("/rpc/get_admin_product_filters"))
            body = JsonSerializer.Serialize(new { brands = new[] { new { id = BrandId, name = "Puffco", count = 10 } }, categories = new[] { new { id = CategoryId, name = "Vape Pen", count = 10 } }, price_min = 20, price_max = 150 });
        else if (path.EndsWith("/rest/v1/brands") && route.Request.Method == "GET")
            body = JsonSerializer.Serialize(new[] { new { id = BrandId, name = "Puffco", is_active = true } });
        else if (path.EndsWith("/rest/v1/categories") && route.Request.Method == "GET")
            body = JsonSerializer.Serialize(new[] { new { id = CategoryId, name = "Vape Pen", is_active = true } });
        else if (path.EndsWith("/rest/v1/products") && route.Request.Method == "GET")
        {
            await releaseEdit;
            body = _empty ? "null" : JsonSerializer.Serialize(new { id = ProductId, name = "Vape Pen 500", description = "<p>Saved description</p>", sku = "VAPE-PEN-500", original_price = 20, sale_price = 18.99, currency = "CAD", category_id = CategoryId, brand_id = BrandId, is_published = true, updated_at = "2026-01-01T00:00:00Z", created_at = "2026-01-01T00:00:00Z", categories = new { id = CategoryId, name = "Vape Pen" }, brands = new { id = BrandId, name = "Puffco" } });
        }
        else if (path.EndsWith("/rest/v1/product_images") && route.Request.Method == "GET")
            body = JsonSerializer.Serialize(Enumerable.Range(0, 2).Select(i => new { id = $"ab09dac2-bc69-4e50-8f1a-49c98a963a{i:00}", product_id = ProductId, object_key = "fixture/pen.svg", position = i, is_primary = i == 0 }));
        else if (path.EndsWith("/rest/v1/product_specifications") && route.Request.Method == "GET")
            body = JsonSerializer.Serialize(Enumerable.Range(0, 2).Select(i => new { id = $"ab09dac2-bc69-4e50-8f1a-49c98a963c{i:00}", product_id = ProductId, name = i == 0 ? "Material" : "Weight", value = i == 0 ? "Steel" : "159 g", position = i }));
        else if ((path.EndsWith("/rest/v1/product_option_types") || path.EndsWith("/rest/v1/product_variants")) && route.Request.Method == "GET") body = "[]";
        if (body is null) { _unexpected.Add(route.Request.Method + " " + path); await route.AbortAsync(); }
        else await route.FulfillAsync(new() { ContentType = "application/json", Body = body });
    });

    private async Task SignInAsync()
    {
        await Page.GotoAsync(WebRoutes.Auth.SignIn + "?returnUrl=" + Uri.EscapeDataString(WebRoutes.Admin.ManageProducts));
        await Page.GetByTestId("signin-email").FillAsync(Email);
        await Page.GetByTestId("signin-submit").ClickAsync();
        await Page.GetByTestId("otp-input").Locator("input").First.FocusAsync();
        await Page.Keyboard.TypeAsync("123456", new() { Delay = 100 });
        await Assertions.Expect(Page.GetByTestId("otp-submit")).ToBeEnabledAsync();
        await Page.GetByTestId("otp-submit").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForURLAsync(E2EEnvironment.AppBaseUrl + WebRoutes.Admin.ManageProducts);
        var dismiss = Page.GetByRole(AriaRole.Button, new() { Name = Strings.Notification_Dismiss, Exact = true });
        while (await dismiss.CountAsync() > 0) await dismiss.First.ClickAsync();
    }

    private async Task SelectAsync(string label, string option)
    {
        await Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = option, Exact = true }).ClickAsync();
    }
    private Task PrepareAppearanceAsync(bool withoutVendorCss) => Page.EvaluateAsync("""
        async withoutVendorCss => {
            if (withoutVendorCss) for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                if (/MudBlazor|CodeBeam/i.test(link.href)) link.disabled=true;
            await document.fonts.load('800 60px "Barlow Condensed"');
            await document.fonts.load('500 20px "Space Grotesk"');
            await document.fonts.ready;
        }
        """, withoutVendorCss);
    private async Task AssertNoOverflowAsync()
    {
        var details = await Page.EvaluateAsync<string>("""
            () => JSON.stringify({doc:document.documentElement.scrollWidth,viewport:innerWidth,
                elements:[...document.querySelectorAll('body *')].filter(e=>e.getBoundingClientRect().right>innerWidth+1)
                .slice(0,12).map(e=>({cls:e.className,w:e.getBoundingClientRect().width,right:e.getBoundingClientRect().right}))})
            """);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue(details);
    }
    private async Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), FullPage = true });
        if (name.Contains("picker", StringComparison.Ordinal))
            await Page.Locator("dialog[open]").ScreenshotAsync(new() { Path = Path.Combine(directory, name + "-dialog.png") });
    }
    private Task SaveComponentAsync(ILocator target, string name) => target.ScreenshotAsync(new()
    {
        Path = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence", name + ".png"),
        Animations = ScreenshotAnimations.Disabled
    });
    private static FilePayload Png(string name) => new() { Name = name, MimeType = "image/png", Buffer = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=") };
    private static string FakeSession()
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        static string Encode(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = Encode(new { alg = "HS256", typ = "JWT" }) + "." + Encode(new { sub = UserId, email = Email, exp = expiry, aud = "authenticated", role = "authenticated", perms = new[] { "products.view", "products.create", "products.edit", "products.delete", "brands.view", "categories.view" }, app_roles = new[] { "Admin" } }) + ".dGVzdA";
        return JsonSerializer.Serialize(new { access_token = token, refresh_token = "test-only-not-a-real-token", token_type = "bearer", expires_in = 3600, expires_at = expiry, user = new { id = UserId, email = Email, aud = "authenticated", role = "authenticated" } });
    }
}

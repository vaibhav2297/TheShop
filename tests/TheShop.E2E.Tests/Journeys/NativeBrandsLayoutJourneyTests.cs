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
public sealed class NativeBrandsLayoutJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    [InlineData(834, true)]
    [InlineData(390, true)]
    public async Task Brands_LayoutsAndInteractions_WorkWithoutVendorCss(int width, bool withoutVendorCss)
    {
        const string userId = "db8d270f-b94e-4a29-bda1-cf95646d1456";
        const string email = "table-probe@example.test";
        var expiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        static string Encode(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = Encode(new { alg = "HS256", typ = "JWT" }) + "." + Encode(new
        {
            sub = userId,
            email,
            exp = expiry,
            aud = "authenticated",
            role = "authenticated",
            perms = new[] { "brands.view", "brands.edit", "brands.delete", "brands.create" },
            app_roles = new[] { "Admin" }
        }) + ".dGVzdA";
        var session = JsonSerializer.Serialize(new
        {
            access_token = token,
            refresh_token = "test-only-not-a-real-token",
            token_type = "bearer",
            expires_in = 3600,
            expires_at = expiry,
            user = new { id = userId, email, aud = "authenticated", role = "authenticated" }
        });
        var unexpected = new List<string>();
        var empty = false;
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (path.Contains("/storage/v1/") && route.Request.Method == "GET")
            {
                await route.FulfillAsync(new() { ContentType = "image/svg+xml", Body = "<svg xmlns='http://www.w3.org/2000/svg' width='144' height='144'><rect width='144' height='144' fill='#e8e8e8'/><rect x='65' y='20' width='14' height='104' rx='3' fill='#252525'/></svg>" });
                return;
            }
            string? body = null;
            var headers = new Dictionary<string, string>();
            if (path.EndsWith("/auth/v1/otp") && route.Request.Method == "POST") body = "{}";
            else if (path.EndsWith("/rpc/customer_exists") && route.Request.Method == "POST") body = "true";
            else if (path.EndsWith("/auth/v1/verify") && route.Request.Method == "POST") body = session;
            else if (path.EndsWith("/rest/v1/customers") && route.Request.Method == "GET")
                body = JsonSerializer.Serialize(new[] { new { id = userId, email, first_name = "Table", last_name = "Probe", date_of_birth = "1990-01-01", created_at = "2026-01-01T00:00:00Z" } });
            else if (path.EndsWith("/rest/v1/brands") && route.Request.Method == "GET")
            {
                headers["Content-Range"] = empty ? "*/0" : "0-9/20";
                headers["Access-Control-Expose-Headers"] = "Content-Range";
                body = JsonSerializer.Serialize(empty ? [] : Enumerable.Range(1, 10).Select(i => new
                {
                    id = $"ac09dac2-bc69-4e50-8f1a-49c98a963b{i:00}",
                    name = $"Table brand {i}",
                    description = "Brand description",
                    logo_path = "fixture/logo.svg",
                    is_active = i % 2 == 0,
                    created_at = "2026-01-01T00:00:00Z"
                }).ToArray());
            }
            else if (path.EndsWith("/rpc/brand_product_counts") && route.Request.Method == "POST") body = "[]";
            if (body is null)
            {
                unexpected.Add(route.Request.Method + " " + path);
                await route.AbortAsync();
            }
            else await route.FulfillAsync(new() { ContentType = "application/json", Body = body, Headers = headers });
        });
        await Page.SetViewportSizeAsync(width, 1000);
        await Page.GotoAsync(WebRoutes.Auth.SignIn + "?returnUrl=" + Uri.EscapeDataString(WebRoutes.Admin.ManageBrands));
        await Page.GetByTestId("signin-email").FillAsync(email);
        await Page.GetByTestId("signin-submit").ClickAsync();
        while (await Page.Locator(".shop-notification button").CountAsync() > 0)
            await Page.Locator(".shop-notification button").First.ClickAsync();
        await Page.GetByTestId("otp-input").Locator("input").First.FocusAsync();
        await Page.Keyboard.TypeAsync("123456", new() { Delay = 100 });
        await Assertions.Expect(Page.GetByTestId("otp-submit")).ToBeEnabledAsync();
        await Page.GetByTestId("otp-submit").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        var table = Page.Locator(".shop-table");
        await table.WaitForAsync(new() { Timeout = 30_000 });
        while (await Page.Locator(".shop-notification button").CountAsync() > 0)
            await Page.Locator(".shop-notification button").First.ClickAsync();

        await Page.EvaluateAsync("""
            async withoutVendorCss => {
                if (withoutVendorCss)
                    for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                        if (/MudBlazor|CodeBeam/i.test(link.href)) link.disabled = true;
                document.body.style.margin = '0';
                await document.fonts.ready;
            }
            """, withoutVendorCss);
        var surface = Page.Locator(".shop-brands-page");
        await Assertions.Expect(surface.Locator("[class*='mud-']")).ToHaveCountAsync(0);
        await Assertions.Expect(surface.Locator("h1")).ToHaveCSSAsync("font-size", "60px");
        await Assertions.Expect(surface).ToHaveCSSAsync("padding-left", width == 390 ? "16px" : "32px");
        if (width < 1024)
        {
            await Page.GetByRole(AriaRole.Button, new() { Name = Strings.Brands_Filters, Exact = true }).ClickAsync();
            var drawer = Page.Locator("dialog[open]");
            await Assertions.Expect(drawer).ToBeVisibleAsync();
            await Page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(drawer).ToHaveCountAsync(0);
        }
        await table.Locator("tbody input[type=checkbox]").First.CheckAsync();
        await Assertions.Expect(Page.Locator(".shop-bulk-action-bar")).ToBeVisibleAsync();
        await SaveAsync($"brands-list-{width}-{withoutVendorCss}");
        await AssertNoOverflowAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = Strings.Close_BulkActionBar, Exact = true }).ClickAsync();
        await Page.Locator("input[type=search]").FillAsync("Table");
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("search=Table"));
        await Page.GetByRole(AriaRole.Button, new() { Name = Strings.Pagination_Next, Exact = true }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("page=2"));

        await Page.GetByRole(AriaRole.Link, new() { Name = Strings.AddBrand_Heading, Exact = true }).ClickAsync();
        await Page.GetByTestId("add-brand-name").WaitForAsync();
        await Assertions.Expect(surface.Locator("[class*='mud-']")).ToHaveCountAsync(0);
        var upload = (await surface.Locator(".shop-image-upload").BoundingBoxAsync())!;
        var fields = (await surface.Locator(".shop-brands-fields").BoundingBoxAsync())!;
        if (width >= 1024) fields.Y.Should().BeApproximately(upload.Y, 1);
        else fields.Y.Should().BeGreaterThan(upload.Y + upload.Height);
        await SaveAsync($"brands-create-{width}-{withoutVendorCss}");
        await Page.GetByTestId("add-brand-save").ClickAsync();
        await Assertions.Expect(surface.Locator(".shop-field-error").First).ToContainTextAsync(Strings.Brand_NameRequired);
        await Page.GetByTestId("add-brand-name").FillAsync("Edited name");
        await Assertions.Expect(surface.Locator(".shop-field-error").First).ToBeEmptyAsync();
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
            await SaveAsync("brands-create-large-text");
            await Page.EvaluateAsync("document.documentElement.style.fontSize=''");
        }
        await surface.GetByRole(AriaRole.Button, new() { Name = Strings.Cancel, Exact = true }).ClickAsync();
        await table.WaitForAsync();
        await Page.Locator(".shop-table-scroll").EvaluateAsync("el => el.scrollLeft = el.scrollWidth");
        await Page.GetByRole(AriaRole.Link, new() { Name = string.Format(Strings.ManageBrands_EditAria, "Table brand 1"), Exact = true }).ClickAsync();
        await Page.GetByTestId("edit-brand-name").WaitForAsync();
        await Assertions.Expect(surface.Locator(".shop-image-upload-grid > div")).ToHaveCountAsync(2);
        (await surface.Locator(".shop-image-upload-grid > div").First.BoundingBoxAsync())!.Width.Should().BeApproximately(144, 1);
        await SaveAsync($"brands-edit-{width}-{withoutVendorCss}");
        await Assertions.Expect(surface.Locator("[class*='mud-']")).ToHaveCountAsync(0);
        await AssertNoOverflowAsync();
        empty = true;
        await surface.GetByRole(AriaRole.Button, new() { Name = Strings.Cancel, Exact = true }).ClickAsync();
        await Assertions.Expect(surface.Locator("h2")).ToHaveTextAsync(Strings.ManageBrands_EmptyTitle);
        await Assertions.Expect(surface.Locator("input[type=search]")).ToBeHiddenAsync();
        await SaveAsync($"brands-empty-{width}-{withoutVendorCss}");
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        unexpected.Should().BeEmpty();
    }

    private async Task AssertNoOverflowAsync()
    {
        var overflowing = await Page.EvaluateAsync<string>("""
            () => JSON.stringify({doc:document.documentElement.scrollWidth, viewport:innerWidth,
                elements:[...document.querySelectorAll('body *')].filter(e => {
                    const r=e.getBoundingClientRect(); return r.width && r.right > innerWidth;
                }).slice(0, 15).map(e=>({tag:e.tagName,cls:e.className,width:e.getBoundingClientRect().width,right:e.getBoundingClientRect().right}))})
            """);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue(overflowing);
    }

    private async Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), FullPage = true });
    }
}

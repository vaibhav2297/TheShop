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
public sealed class NativeSkeletonJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    private const string Tertiary = "rgb(232, 232, 232)";

    [Theory]
    [InlineData(390)]
    [InlineData(1440)]
    public async Task Catalogue_WhileQueriesArePending_ShowsStaticCardAndFilterSkeletons(int width)
    {
        var release = new TaskCompletionSource();
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            // Data reads stay pending so the skeletons remain; everything else fails fast so the app boots.
            if (new Uri(route.Request.Url).AbsolutePath.Contains("/rest/v1/"))
                await release.Task;
            await route.AbortAsync();
        });
        try
        {
            await Page.SetViewportSizeAsync(width, 900);
            await Page.GotoAsync(WebRoutes.Products);
            var cards = Page.Locator(".shop-product-card-skeleton");
            await Assertions.Expect(cards).ToHaveCountAsync(12, new() { Timeout = 30_000 });
            await Page.EvaluateAsync("async () => { await document.fonts.ready; }");

            var card = cards.First;
            await Assertions.Expect(card).ToHaveAttributeAsync("aria-hidden", "true");
            var media = card.Locator(".shop-product-card-media");
            var box = (await media.BoundingBoxAsync())!;
            box.Width.Should().BeApproximately(box.Height, 1f, "the skeleton reuses the card's square media frame");
            await Assertions.Expect(media).ToHaveCSSAsync("background-color", Tertiary);

            var content = (await card.Locator(".shop-product-card-content").BoundingBoxAsync())!;
            foreach (var (name, ratio, fontSize) in new[] { ("brand", 0.4, 16), ("title", 0.8, 20), ("price", 0.3, 20) })
            {
                var line = card.Locator($".shop-product-card-skeleton-{name}");
                (await line.BoundingBoxAsync())!.Width.Should().BeApproximately((float)(content.Width * ratio), 1f);
                await Assertions.Expect(line).ToHaveCSSAsync("background-size", $"100% {fontSize}px");
                (await line.BoundingBoxAsync())!.Height.Should().BeGreaterThan(fontSize);
            }

            var groups = Page.Locator(".shop-filter-skeleton-group");
            await Assertions.Expect(groups).ToHaveCountAsync(5);
            (await groups.First.BoundingBoxAsync())!.Height.Should().BeApproximately(72f, 1f);
            await Assertions.Expect(groups.First.Locator(".shop-skeleton")).ToHaveCSSAsync("font-size", "16px");

            (await Page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('.shop-skeleton')].map(e => getComputedStyle(e).animationName).filter(n => n !== 'none')"))
                .Should().BeEmpty("the approved design is static");
            (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
            await SaveAsync($"skeleton-catalogue-{width}");

            await Page.EmulateMediaAsync(new() { ForcedColors = ForcedColors.Active });
            await Assertions.Expect(media).ToHaveCSSAsync("outline-style", "solid");
            await SaveAsync($"skeleton-catalogue-forced-{width}");
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Fact]
    public async Task Admin_LoadingStatesKeepTheTableHeaderAndMirrorCardAndFormGeometry()
    {
        const string userId = "db8d270f-b94e-4a29-bda1-cf95646d1457";
        const string email = "skeleton-probe@example.test";
        var brandsGate = new TaskCompletionSource();
        var holdRest = new TaskCompletionSource();
        var session = FakeSession(userId, email, "brands.view", "brands.edit", "brands.delete", "brands.create", "dashboard.view");
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            var method = route.Request.Method;
            string? body = null;
            var headers = new Dictionary<string, string>();
            if (path.EndsWith("/auth/v1/otp") && method == "POST") body = "{}";
            else if (path.EndsWith("/rpc/customer_exists") && method == "POST") body = "true";
            else if (path.EndsWith("/auth/v1/verify") && method == "POST") body = session;
            else if (path.EndsWith("/rest/v1/customers") && method == "GET")
                body = JsonSerializer.Serialize(new[] { new { id = userId, email, first_name = "Skeleton", last_name = "Probe", date_of_birth = "1990-01-01", created_at = "2026-01-01T00:00:00Z" } });
            else if (path.EndsWith("/rest/v1/brands") && method == "GET" && !route.Request.Url.Contains("id=eq."))
            {
                await brandsGate.Task;
                headers["Content-Range"] = "0-1/20";
                headers["Access-Control-Expose-Headers"] = "Content-Range";
                body = """
                    [{"id":"ac09dac2-bc69-4e50-8f1a-49c98a963bc1","name":"Skeleton brand A","description":"Loaded row","is_active":true,"created_at":"2026-01-01T00:00:00Z"},
                     {"id":"ac09dac2-bc69-4e50-8f1a-49c98a963bc2","name":"Skeleton brand B","description":"Loaded row","is_active":false,"created_at":"2026-01-01T00:00:00Z"}]
                    """;
            }
            else if (path.EndsWith("/rpc/brand_product_counts") && method == "POST") body = "[]";

            if (body is not null)
            {
                await route.FulfillAsync(new() { ContentType = "application/json", Body = body, Headers = headers });
                return;
            }

            // Dashboard and brand-detail data reads stay pending so their skeletons remain on screen.
            if (path.Contains("/rest/v1/"))
                await holdRest.Task;
            await route.AbortAsync();
        });

        try
        {
            await Page.SetViewportSizeAsync(1440, 1000);
            await Page.GotoAsync(WebRoutes.Auth.SignIn + "?returnUrl=" + Uri.EscapeDataString(WebRoutes.Admin.ManageBrands));
            await Page.GetByTestId("signin-email").FillAsync(email);
            await Page.GetByTestId("signin-submit").ClickAsync();
            await Page.GetByTestId("otp-input").Locator("input").First.FocusAsync();
            await Page.Keyboard.TypeAsync("123456");
            await Page.GetByTestId("otp-submit").ClickAsync();

            // Manage Brands: real header, ten hidden skeleton rows, busy table and one status message.
            var table = Page.Locator(".shop-table");
            var loadingRows = table.Locator("tr.shop-table-loading-row");
            await Assertions.Expect(loadingRows).ToHaveCountAsync(10, new() { Timeout = 30_000 });
            await Assertions.Expect(table).ToHaveAttributeAsync("aria-busy", "true");
            await Assertions.Expect(table.Locator("thead th").Nth(1)).ToHaveTextAsync(Strings.ManageBrands_ColumnName);
            await Assertions.Expect(loadingRows.First).ToHaveAttributeAsync("aria-hidden", "true");
            await Assertions.Expect(loadingRows.First.Locator("td")).ToHaveCountAsync(5);
            // The bar follows whatever text size the real cells inherit.
            var cellFont = await loadingRows.First.Locator("td").Nth(1).EvaluateAsync<string>("el => getComputedStyle(el).fontSize");
            await Assertions.Expect(loadingRows.First.Locator(".shop-skeleton").Nth(1)).ToHaveCSSAsync("background-size", "100% " + cellFont);
            await Assertions.Expect(Page.Locator(".shop-table-scroll [role=status]")).ToHaveTextAsync(Strings.Loading);
            await Assertions.Expect(table.GetByRole(AriaRole.Checkbox, new() { Name = Strings.Table_SelectPage, Exact = true })).ToBeDisabledAsync();
            await Assertions.Expect(Page.Locator(".shop-pagination")).ToHaveCountAsync(0);
            var headerBefore = (await table.Locator("thead").BoundingBoxAsync())!;
            await SaveAsync("skeleton-table-loading-live");

            brandsGate.TrySetResult();
            await Assertions.Expect(table.GetByText("Skeleton brand A", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(loadingRows).ToHaveCountAsync(0);
            await Assertions.Expect(table).Not.ToHaveAttributeAsync("aria-busy", "true");
            await Assertions.Expect(Page.Locator(".shop-table-scroll [role=status]")).ToHaveTextAsync(string.Empty);
            (await table.Locator("thead").BoundingBoxAsync())!.Y.Should().BeApproximately(headerBefore.Y, 1f, "the header stays in place");
            await SaveAsync("skeleton-table-loaded-live");

            // Admin console: five outlined card skeletons in the real grid.
            await Page.EvaluateAsync("route => Blazor.navigateTo(route)", WebRoutes.Admin.Console);
            var cards = Page.Locator(".shop-admin-module-card-skeleton");
            await Assertions.Expect(cards).ToHaveCountAsync(5, new() { Timeout = 30_000 });
            await Assertions.Expect(cards.First).ToHaveCSSAsync("padding", "24px");
            await Assertions.Expect(cards.First).ToHaveCSSAsync("box-shadow", "rgb(224, 224, 224) 0px 0px 0px 1px inset");
            await Assertions.Expect(cards.First.Locator(".shop-admin-module-card-skeleton-action")).ToHaveCSSAsync("height", "36px");
            await Assertions.Expect(cards.First.Locator(".shop-admin-module-card-skeleton-count")).ToHaveCSSAsync("background-size", "100% 60px");
            await SaveAsync("skeleton-admin-console-live");

            // Edit brand: heading line, field-height block and panel with the page padding.
            await Page.EvaluateAsync("route => Blazor.navigateTo(route)", "/admin/brands/ac09dac2-bc69-4e50-8f1a-49c98a963bc1/edit");
            var form = Page.Locator(".shop-admin-form-skeleton");
            await Assertions.Expect(form).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(form).ToHaveCSSAsync("padding", "32px");
            await Assertions.Expect(form).ToHaveCSSAsync("row-gap", "24px");
            await Assertions.Expect(form.Locator(".shop-admin-form-skeleton-field")).ToHaveCSSAsync("height", "61px");
            await Assertions.Expect(form.Locator(".shop-admin-form-skeleton-panel")).ToHaveCSSAsync("height", "240px");
            await SaveAsync("skeleton-edit-form-live");

            (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();
            await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        }
        finally
        {
            brandsGate.TrySetResult();
            holdRest.TrySetResult();
        }
    }

    private static string FakeSession(string userId, string email, params string[] permissions)
    {
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
            perms = permissions,
            app_roles = new[] { "Admin" }
        }) + ".dGVzdA";
        return JsonSerializer.Serialize(new
        {
            access_token = token,
            refresh_token = "test-only-not-a-real-token",
            token_type = "bearer",
            expires_in = 3600,
            expires_at = expiry,
            user = new { id = userId, email, aud = "authenticated", role = "authenticated" }
        });
    }

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}

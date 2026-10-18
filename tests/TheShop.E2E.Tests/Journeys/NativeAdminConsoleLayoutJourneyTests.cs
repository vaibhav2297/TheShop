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
public sealed class NativeAdminConsoleLayoutJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    private const string UserId = "db8d270f-b94e-4a29-bda1-cf95646d1460";
    private const string Email = "console-probe@example.test";
    private readonly List<string> _unexpected = [];
    private readonly List<string> _counted = [];

    [Theory]
    [InlineData(1440, false)]
    [InlineData(1440, true)]
    [InlineData(834, true)]
    [InlineData(390, true)]
    public async Task Console_LoadingAndCards_MatchResponsiveLayoutWithoutVendorCss(int width, bool withoutVendorCss)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await InterceptAsync(["dashboard.view", "products.view", "categories.view", "brands.view", "admin_users.view", "roles.view"], release.Task);
        try
        {
            await Page.SetViewportSizeAsync(width, 1000);
            await SignInAsync();
            var surface = Page.Locator(".shop-admin-console");
            var grid = surface.Locator(".shop-admin-console-grid");
            var loading = grid.Locator(".shop-admin-module-card-skeleton");
            await Assertions.Expect(loading).ToHaveCountAsync(5);
            await PrepareAppearanceAsync(withoutVendorCss);
            await Assertions.Expect(surface.Locator("h1")).ToHaveCSSAsync("font-size", "60px");
            await Assertions.Expect(surface).ToHaveCSSAsync("padding-left", width < 768 ? "16px" : "32px");
            await Assertions.Expect(grid).ToHaveAttributeAsync("aria-busy", "true");
            await Assertions.Expect(surface.Locator("[role=status]")).ToHaveTextAsync(Strings.Loading);
            await Assertions.Expect(loading.First).ToHaveAttributeAsync("aria-hidden", "true");
            await Assertions.Expect(loading.First.Locator(".shop-admin-module-card-skeleton-label")).ToHaveCSSAsync("background-size", "100% 16px");
            await Assertions.Expect(loading.First.Locator(".shop-admin-module-card-skeleton-count")).ToHaveCSSAsync("height", "56px");
            await Assertions.Expect(loading.First.Locator(".shop-admin-module-card-skeleton-action")).ToHaveCSSAsync("height", "38px");
            var before = (await loading.First.BoundingBoxAsync())!;
            before.Height.Should().BeGreaterThanOrEqualTo(width < 768 ? 224 : 248);
            await SaveAsync($"admin-console-loading-{width}-{withoutVendorCss}");

            release.TrySetResult();
            var cards = grid.Locator("article.shop-admin-module-card");
            await Assertions.Expect(cards).ToHaveCountAsync(5);
            await Assertions.Expect(loading).ToHaveCountAsync(0);
            await Assertions.Expect(grid).ToHaveAttributeAsync("aria-busy", "false");
            await Assertions.Expect(surface.Locator("p[role=status]")).ToBeEmptyAsync();
            await Assertions.Expect(surface.Locator("[class*='mud-']")).ToHaveCountAsync(0);
            await Assertions.Expect(cards.First.Locator("h2")).ToHaveTextAsync(Strings.AdminConsole_Module_Products);
            await Assertions.Expect(cards.First.Locator("h2")).ToHaveCSSAsync("font-size", "24px");
            await Assertions.Expect(cards.First.Locator(".shop-admin-module-card-count")).ToHaveTextAsync("78");
            await Assertions.Expect(cards.Last.Locator(".shop-admin-module-card-count")).ToHaveTextAsync("0");
            var after = (await cards.First.BoundingBoxAsync())!;
            after.Y.Should().BeApproximately(before.Y, 1);
            after.Height.Should().BeApproximately(before.Height, 1);
            var columns = width >= 1024 ? 3 : width >= 768 ? 2 : 1;
            var gridBox = (await grid.BoundingBoxAsync())!;
            after.Width.Should().BeApproximately((gridBox.Width - (columns - 1) * 16) / columns, 1);
            if (columns > 1)
                (await cards.Nth(1).BoundingBoxAsync())!.Y.Should().BeApproximately(after.Y, 1);
            (await cards.Nth(columns).BoundingBoxAsync())!.Y.Should().BeApproximately(after.Y + after.Height + 16, 1);
            await cards.First.Locator("a").FocusAsync();
            await Assertions.Expect(cards.First.Locator("a")).ToBeFocusedAsync();
            await Page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(cards.Nth(1).Locator("a")).ToBeFocusedAsync();
            await SaveAsync($"admin-console-loaded-{width}-{withoutVendorCss}");
            await AssertNoOverflowAsync();

            if (width == 390)
            {
                foreach (var resized in new[] { 320, 767, 768, 1023, 1024 })
                {
                    await Page.SetViewportSizeAsync(resized, 1000);
                    await AssertNoOverflowAsync();
                }
                await Page.SetViewportSizeAsync(width, 1000);
                await Page.EvaluateAsync("document.documentElement.style.fontSize='200%'");
                await AssertNoOverflowAsync();
                await SaveAsync("admin-console-large-text");
                await Page.EvaluateAsync("document.documentElement.style.fontSize=''");
            }

            var manageBrands = cards.Locator($"a[href='{WebRoutes.Admin.ManageBrands}']");
            await manageBrands.ClickAsync();
            await Assertions.Expect(Page).ToHaveURLAsync(E2EEnvironment.AppBaseUrl + WebRoutes.Admin.ManageBrands);
            await Assertions.Expect(Page.Locator(".shop-brands-page h1")).ToHaveTextAsync(Strings.ManageBrands_Heading);
            _counted.Should().Equal("products", "categories", "brands", "users", "roles");
            _unexpected.Should().BeEmpty();
            await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Theory]
    [InlineData("limited", 1440)]
    [InlineData("empty", 390)]
    [InlineData("denied", 1440)]
    [InlineData("denied", 390)]
    public async Task Console_PermissionAndUnavailableCountStates_KeepNativeRecovery(string mode, int width)
    {
        string[] permissions = mode switch
        {
            "limited" => ["dashboard.view", "brands.view"],
            "empty" => ["dashboard.view"],
            _ => ["categories.view"]
        };
        await InterceptAsync(permissions, Task.CompletedTask, unavailable: mode == "limited");
        await Page.SetViewportSizeAsync(width, 1000);
        await SignInAsync();
        var surface = Page.Locator(mode == "denied" ? ".shop-access-denied" : ".shop-admin-console");
        await Assertions.Expect(surface).ToBeVisibleAsync();
        await PrepareAppearanceAsync(true);
        if (mode == "limited")
        {
            var cards = surface.Locator("article");
            await Assertions.Expect(cards).ToHaveCountAsync(1);
            await Assertions.Expect(cards.Locator("h2")).ToHaveTextAsync(Strings.AdminConsole_Module_Brands);
            await Assertions.Expect(cards.Locator(".shop-admin-module-card-count")).ToHaveTextAsync(Strings.AdminConsole_CountUnavailable);
            _counted.Should().Equal("brands");
        }
        else if (mode == "empty")
        {
            await Assertions.Expect(surface.Locator(".shop-admin-console-empty")).ToContainTextAsync(Strings.AdminConsole_NotHaveModulePermission);
            await Assertions.Expect(surface.Locator("article")).ToHaveCountAsync(0);
            _counted.Should().BeEmpty();
        }
        else
        {
            await Assertions.Expect(surface.Locator("h1")).ToHaveTextAsync(Strings.AccessDenied_Title);
            await Assertions.Expect(surface.Locator("p")).ToHaveTextAsync(Strings.AccessDenied_Message);
            await Assertions.Expect(surface.Locator("h1")).ToHaveCSSAsync("font-size", "34px");
            await Assertions.Expect(surface.Locator("a.shop-button")).ToHaveAttributeAsync("href", WebRoutes.Home);
            await Assertions.Expect(Page).ToHaveURLAsync(E2EEnvironment.AppBaseUrl + WebRoutes.Admin.Console);
            _counted.Should().BeEmpty();
        }
        await Assertions.Expect(surface.Locator("[class*='mud-']")).ToHaveCountAsync(0);
        await AssertNoOverflowAsync();
        await SaveAsync($"admin-console-{mode}-{width}");
        _unexpected.Should().BeEmpty();
        if (mode == "denied")
        {
            await surface.GetByRole(AriaRole.Link, new() { Name = Strings.AccessDenied_BackToHome, Exact = true }).ClickAsync();
            await Assertions.Expect(Page).ToHaveURLAsync(E2EEnvironment.AppBaseUrl + WebRoutes.Home);
        }
    }

    private Task InterceptAsync(string[] permissions, Task releaseCounts, bool unavailable = false)
    {
        var session = FakeSession(permissions);
        return Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            var method = route.Request.Method;
            string? body = null;
            var headers = new Dictionary<string, string>();
            if (path.EndsWith("/auth/v1/otp") && method == "POST") body = "{}";
            else if (path.EndsWith("/rpc/customer_exists") && method == "POST") body = "true";
            else if (path.EndsWith("/auth/v1/verify") && method == "POST") body = session;
            else if (path.EndsWith("/rest/v1/customers") && method == "GET")
                body = JsonSerializer.Serialize(new[] { new { id = UserId, email = Email, first_name = "Console", last_name = "Probe", date_of_birth = "1990-01-01", created_at = "2026-01-01T00:00:00Z" } });
            else if (path.EndsWith("/rpc/admin_module_count") && method == "POST")
            {
                await releaseCounts;
                using var data = JsonDocument.Parse(route.Request.PostData!);
                var module = data.RootElement.GetProperty("p_module").GetString()!;
                _counted.Add(module);
                if (unavailable)
                {
                    await route.FulfillAsync(new() { Status = 500, ContentType = "application/json", Body = "{\"message\":\"Count unavailable\"}" });
                    return;
                }
                body = module switch { "products" => "78", "categories" => "121", "brands" => "24", "users" => "205", "roles" => "0", _ => null };
            }
            else if (path.EndsWith("/rest/v1/brands") && method == "GET")
            {
                headers["Content-Range"] = "*/0";
                headers["Access-Control-Expose-Headers"] = "Content-Range";
                body = "[]";
            }
            else if (path.EndsWith("/rpc/brand_product_counts") && method == "POST") body = "[]";
            if (body is null)
            {
                _unexpected.Add(method + " " + path);
                await route.AbortAsync();
            }
            else await route.FulfillAsync(new() { ContentType = "application/json", Body = body, Headers = headers });
        });
    }

    private async Task SignInAsync()
    {
        await Page.GotoAsync(WebRoutes.Auth.SignIn + "?returnUrl=" + Uri.EscapeDataString(WebRoutes.Admin.Console));
        await Page.GetByTestId("signin-email").FillAsync(Email);
        await Page.GetByTestId("signin-submit").ClickAsync();
        await Page.GetByTestId("otp-input").Locator("input").First.FocusAsync();
        await Page.Keyboard.TypeAsync("123456", new() { Delay = 100 });
        await Assertions.Expect(Page.GetByTestId("otp-submit")).ToBeEnabledAsync();
        await Page.GetByTestId("otp-submit").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Page.WaitForURLAsync(E2EEnvironment.AppBaseUrl + WebRoutes.Admin.Console);
        var dismiss = Page.GetByRole(AriaRole.Button, new() { Name = Strings.Notification_Dismiss, Exact = true });
        while (await dismiss.CountAsync() > 0) await dismiss.First.ClickAsync();
    }

    private Task PrepareAppearanceAsync(bool withoutVendorCss) => Page.EvaluateAsync("""
        async withoutVendorCss => {
            if (withoutVendorCss)
                for (const link of document.querySelectorAll('link[rel=stylesheet]'))
                    if (/MudBlazor|CodeBeam/i.test(link.href)) throw new Error('Unexpected legacy stylesheet');
            document.body.style.margin = '0';
            await document.fonts.load('800 60px "Barlow Condensed"');
            await document.fonts.load('500 20px "Space Grotesk"');
            await document.fonts.ready;
        }
        """, withoutVendorCss);

    private async Task AssertNoOverflowAsync() =>
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")).Should().BeTrue();

    private Task SaveAsync(string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        return Page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), FullPage = true });
    }

    private static string FakeSession(string[] permissions)
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        static string Encode(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = Encode(new { alg = "HS256", typ = "JWT" }) + "." + Encode(new
        {
            sub = UserId,
            email = Email,
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
            user = new { id = UserId, email = Email, aud = "authenticated", role = "authenticated" }
        });
    }
}

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
public sealed class ProfileDrawerJourneyTests(PlaywrightFixture playwright)
    : E2ETestBase(playwright)
{
    [Theory]
    [InlineData(390, true)]
    [InlineData(1440, true)]
    [InlineData(390, false)]
    [InlineData(1440, false)]
    public async Task AccountAction_OpensLayoutDrawer_PreservesPermissionsNavigationAndLogout(int width, bool admin)
    {
        var errors = new List<string>();
        Page.PageError += (_, error) => errors.Add(error);
        const string userId = "db8d270f-b94e-4a29-bda1-cf95646d1456";
        const string email = "drawer-probe@example.test";
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
            perms = admin ? new[] { "dashboard.view" } : Array.Empty<string>()
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
        var logoutRequests = 0;
        var releaseLogout = new TaskCompletionSource();
        await Context.RouteAsync(E2EEnvironment.Get("API_URL").TrimEnd('/') + "/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            string? body = null;
            if (path.EndsWith("/auth/v1/otp") && route.Request.Method == "POST") body = "{}";
            else if (path.EndsWith("/rpc/customer_exists") && route.Request.Method == "POST") body = "true";
            else if (path.EndsWith("/auth/v1/verify") && route.Request.Method == "POST") body = session;
            else if (path.EndsWith("/auth/v1/token") && route.Request.Method == "POST") body = session;
            else if (path.EndsWith("/auth/v1/user") && route.Request.Method == "GET")
                body = JsonSerializer.Serialize(new { id = userId, email, aud = "authenticated", role = "authenticated" });
            else if (path.EndsWith("/auth/v1/logout") && route.Request.Method == "POST") { logoutRequests++; await releaseLogout.Task; body = "{}"; }
            else if (path.EndsWith("/rest/v1/customers") && route.Request.Method == "GET")
                body = JsonSerializer.Serialize(new[] { new { id = userId, email, first_name = "John", last_name = "Doe", date_of_birth = "1990-01-01", created_at = "2026-01-01T00:00:00Z" } });
            if (body is null) { unexpected.Add(route.Request.Method + " " + path); await route.AbortAsync(); }
            else await route.FulfillAsync(new() { ContentType = "application/json", Body = body });
        });
        await Page.SetViewportSizeAsync(width, 1024);
        await Page.GotoAsync(WebRoutes.Auth.SignIn);
        await Page.GetByTestId("signin-email").FillAsync(email, new() { Timeout = 30_000 });
        await Page.GetByTestId("signin-submit").ClickAsync();
        await Page.GetByTestId("otp-input").Locator("input").First.FocusAsync();
        await Page.Keyboard.TypeAsync("123456");
        while (await Page.GetByTestId("notification-dismiss").CountAsync() > 0)
            await Page.GetByTestId("notification-dismiss").First.ClickAsync();
        await Page.GetByTestId("otp-submit").ClickAsync();
        var account = Page.GetByRole(AriaRole.Button, new() { Name = Strings.Nav_Account, Exact = true });
        await account.WaitForAsync(new() { Timeout = 30_000 });
        while (await Page.GetByTestId("notification-dismiss").CountAsync() > 0)
            await Page.GetByTestId("notification-dismiss").First.ClickAsync();
        await account.ClickAsync(new() { Timeout = 30_000 });
        var drawer = Page.Locator(".shop-profile-drawer");
        await Assertions.Expect(drawer).ToHaveCSSAsync("transform", "matrix(1, 0, 0, 1, 0, 0)");
        await Assertions.Expect(drawer).ToHaveAccessibleNameAsync(Strings.Nav_Account);
        await Assertions.Expect(account).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(drawer.Locator($"a[href='{WebRoutes.Admin.Console}']")).ToHaveCountAsync(admin ? 1 : 0);
        await Assertions.Expect(drawer.Locator(".shop-profile-drawer-email")).ToBeVisibleAsync();
        var directory = Path.Combine(AppContext.BaseDirectory, "native-ui-evidence");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"profile-drawer-{width}-{admin}.png") });
        await Page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(drawer).ToBeHiddenAsync();
        await Assertions.Expect(account).ToBeFocusedAsync();
        await account.ClickAsync();
        await drawer.GetByRole(AriaRole.Link, new() { Name = Strings.Nav_MyProfile, Exact = true }).ClickAsync();
        await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath == WebRoutes.Profile);
        await Assertions.Expect(Page.Locator("dialog:modal")).ToHaveCountAsync(0);
        await Assertions.Expect(account).ToHaveAttributeAsync("aria-expanded", "false");
        await account.ClickAsync();
        await drawer.GetByRole(AriaRole.Button, new() { Name = Strings.Logout, Exact = true }).ClickAsync();
        // Held logout keeps BusyKeys.Global active: the shared overlay blocks the page until it completes.
        var overlay = Page.GetByTestId("loading-overlay");
        await Assertions.Expect(overlay).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = Strings.Loading })).Not.ToHaveCountAsync(0);
        releaseLogout.SetResult();
        await Page.WaitForFunctionAsync("() => !localStorage.getItem('shop.auth.session')");
        await Assertions.Expect(Page.Locator("dialog:modal")).ToHaveCountAsync(0);
        await Assertions.Expect(Page.GetByRole(AriaRole.Link, new() { Name = Strings.Nav_Account, Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(overlay).ToHaveCountAsync(0);
        logoutRequests.Should().Be(1);
        unexpected.Should().BeEmpty();
        errors.Should().BeEmpty();
    }
}

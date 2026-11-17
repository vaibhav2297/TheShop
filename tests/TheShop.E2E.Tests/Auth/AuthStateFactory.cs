using System.Text.Json;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Auth;

/// <summary>
/// Signs each test persona in through the real OTP UI once per run and caches the resulting
/// Playwright storage state under <c>.auth-states/</c>. All authenticated journeys start from
/// these files instead of repeating the login flow.
/// </summary>
public static class AuthStateFactory
{
    public const string AdminEmail = "e2e-admin@theshop.test";
    public const string SupportEmail = "e2e-support@theshop.test";
    public const string CustomerEmail = "e2e-customer@theshop.test";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly HashSet<string> SignedInThisRun = [];

    public static string StatePath(string email) =>
        Path.Combine(E2EEnvironment.RepoRoot, "tests", "TheShop.E2E.Tests", ".auth-states",
            email.Split('@')[0] + ".json");

    /// <summary>Signs the given persona in via the real OTP UI (once per run) and returns the cached storage-state path.</summary>
    public static async Task<string> EnsureSignedInAsync(IBrowser browser, string email)
    {
        var path = StatePath(email);
        await Gate.WaitAsync();
        try
        {
            if (SignedInThisRun.Contains(email) && File.Exists(path) && HasUsableSession(path)) return path;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var context = await ShopBrowser.NewContextAsync(browser);
            var page = await context.NewPageAsync();

            await page.GotoAsync(WebRoutes.Auth.SignIn);
            await page.Locator("main.shop-auth-layout")
                .WaitForAsync(new() { Timeout = 30_000 }); // WASM boot

            // The email test ID targets the native input; otp-input targets its digit container.
            await page.GetByTestId("signin-email").FillAsync(email);
            await page.GetByTestId("signin-submit").ClickAsync();

            var otp = await OtpInbox.WaitForOtpAsync(email);

            // OtpInput auto-advances between boxes: focus the first input, type the code.
            await page.GetByTestId("otp-input").Locator("input").First.ClickAsync();
            await page.Keyboard.TypeAsync(otp, new() { Delay = 50 });
            await page.GetByTestId("otp-submit").ClickAsync();

            // SignInVerify navigates to Routes.Home on success (no ReturnUrl in this flow).
            await page.WaitForURLAsync(url => new Uri(url).AbsolutePath == WebRoutes.Home,
                new() { Timeout = 30_000 });
            await Assertions.Expect(page.Locator("main[data-shop-scroll-root]")).ToBeVisibleAsync();

            await context.StorageStateAsync(new() { Path = path });
            await context.DisposeAsync();
            SignedInThisRun.Add(email);
            return path;
        }
        finally { Gate.Release(); }
    }

    internal static bool HasUsableSession(string path)
    {
        try
        {
            using var state = JsonDocument.Parse(File.ReadAllText(path));
            var stored = state.RootElement.GetProperty("origins").EnumerateArray()
                .SelectMany(origin => origin.GetProperty("localStorage").EnumerateArray())
                .FirstOrDefault(item => item.GetProperty("name").GetString() == "shop.auth.session");
            if (stored.ValueKind == JsonValueKind.Undefined) return false;
            using var session = JsonDocument.Parse(stored.GetProperty("value").GetString()!);
            var token = session.RootElement.GetProperty("AccessToken").GetString()?.Split('.');
            if (token is not { Length: 3 }) return false;
            var payload = token[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var claims = JsonDocument.Parse(Convert.FromBase64String(payload));
            return claims.RootElement.GetProperty("exp").GetInt64() > DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds();
        }
        catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }
}

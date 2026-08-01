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
            if (File.Exists(path)) return path; // minted earlier in this run
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var context = await ShopBrowser.NewContextAsync(browser);
            var page = await context.NewPageAsync();

            await page.GotoAsync(WebRoutes.Auth.SignIn);
            await page.Locator(".mud-layout, .mud-container, input").First
                .WaitForAsync(new() { Timeout = 30_000 }); // WASM boot

            // Unlike otp-input (a container), UserAttributes on MudTextField lands directly
            // on the <input> itself here — verified by inspecting the rendered DOM.
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
            await Assertions.Expect(page.Locator(".mud-layout")).ToBeVisibleAsync();

            await context.StorageStateAsync(new() { Path = path });
            await context.DisposeAsync();
            return path;
        }
        finally { Gate.Release(); }
    }
}

using System.Text.Json;
using Microsoft.Playwright;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// Creates browser contexts pre-wired for The Shop: every request for
/// <c>appsettings*.json</c> is fulfilled with local-Supabase values, so the app under test
/// talks to the E2E stack without any production config change.
/// </summary>
public static class ShopBrowser
{
    /// <summary>Creates a context pointed at the local app with Supabase config intercepted.</summary>
    public static async Task<IBrowserContext> NewContextAsync(IBrowser browser, string? storageStatePath = null)
    {
        var context = await browser.NewContextAsync(new()
        {
            BaseURL = E2EEnvironment.AppBaseUrl,
            StorageStatePath = storageStatePath is not null && File.Exists(storageStatePath)
                ? storageStatePath
                : null,
        });

        var e2eConfig = JsonSerializer.Serialize(new
        {
            Supabase = new
            {
                Url = E2EEnvironment.Get("API_URL"),
                PublishableKey = E2EEnvironment.Get("ANON_KEY"),
            },
        });

        await context.RouteAsync("**/appsettings*.json", route =>
            route.FulfillAsync(new() { ContentType = "application/json", Body = e2eConfig }));

        return context;
    }
}

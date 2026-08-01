using Microsoft.Playwright;
using Xunit;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// One Playwright driver and one Chromium instance per test collection. Contexts (and their
/// route interception + storage state) are created per test via <see cref="ShopBrowser"/>.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    /// <summary>The shared Chromium instance for this test collection.</summary>
    public IBrowser Browser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        if (!E2EEnvironment.IsAvailable) return;

        _playwright = await Playwright.CreateAsync();
        _playwright.Selectors.SetTestIdAttribute("data-testid");
        Browser = await _playwright.Chromium.LaunchAsync(new()
        {
            // Set E2E_HEADED=1 locally to watch the run.
            //Headless = Environment.GetEnvironmentVariable("E2E_HEADED") is not null,
            Headless = E2EEnvironment.Headless
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
    }
}

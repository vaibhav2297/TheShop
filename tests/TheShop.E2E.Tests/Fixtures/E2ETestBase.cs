using Microsoft.Playwright;
using Xunit;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// Base class for all E2E journeys: skips when <c>.e2e-env</c> is absent, creates a fresh
/// browser context per test with app-config interception installed, and saves a Playwright
/// trace on failure under <c>bin/.../playwright-traces/</c>.
/// </summary>
[Collection(E2ECollection.Name)]
public abstract class E2ETestBase(PlaywrightFixture playwright) : IAsyncLifetime
{
    /// <summary>The Chromium instance shared by the E2E test collection.</summary>
    protected PlaywrightFixture Playwright { get; } = playwright;

    /// <summary>The browser context created for the current test.</summary>
    protected IBrowserContext Context { get; private set; } = null!;

    /// <summary>The page created for the current test.</summary>
    protected IPage Page { get; private set; } = null!;

    /// <summary>Storage-state file to preload (set by authenticated journeys); null = anonymous.</summary>
    protected virtual string? StorageStatePath => null;

    public virtual async ValueTask InitializeAsync()
    {
        Assert.SkipUnless(E2EEnvironment.IsAvailable,
            "E2E environment not configured — create tests/TheShop.E2E.Tests/.e2e-env (see .e2e-env.example).");

        Context = await ShopBrowser.NewContextAsync(Playwright.Browser, StorageStatePath);
        await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        Page = await Context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Context is null) return;
        var failed = TestContext.Current.TestState?.Result is TestResult.Failed;
        var tracePath = failed
            ? Path.Combine(AppContext.BaseDirectory, "playwright-traces",
                $"{TestContext.Current.Test?.TestDisplayName ?? "unknown"}.zip")
            : null;
        await Context.Tracing.StopAsync(new() { Path = tracePath });
        await Context.DisposeAsync();
    }
}

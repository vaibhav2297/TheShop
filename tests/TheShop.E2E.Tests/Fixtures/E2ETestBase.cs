using Microsoft.Playwright;
using Xunit;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>
/// Base class for all E2E journeys: skips when the local environment is down, creates a fresh
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
            "E2E environment not running — execute tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1 first.");

        Context = await ShopBrowser.NewContextAsync(Playwright.Browser, StorageStatePath);
        await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        Page = await Context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Context is null) return;

        // The local stack rotates refresh tokens on every use (config.toml
        // enable_refresh_token_rotation): the token embedded in the cached storage-state file
        // gets consumed and replaced the moment this test's session refreshes. Re-persist it so
        // the next test (in this run or a later `dotnet test` process) picks up the still-valid
        // rotated token instead of the now-dead one the file started with. Skipped when the file
        // is already gone — a sign-out journey deletes it deliberately and must not have it
        // recreated with a signed-out state.
        if (StorageStatePath is { } path && File.Exists(path))
            await Context.StorageStateAsync(new() { Path = path });

        var failed = TestContext.Current.TestState?.Result is TestResult.Failed;
        var tracePath = failed
            ? Path.Combine(AppContext.BaseDirectory, "playwright-traces",
                $"{TestContext.Current.Test?.TestDisplayName ?? "unknown"}.zip")
            : null;
        await Context.Tracing.StopAsync(new() { Path = tracePath });
        await Context.DisposeAsync();
    }
}

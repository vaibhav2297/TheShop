using TheShop.E2E.Tests.Auth;
using Xunit;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>Base for journeys that start signed in as a given persona.</summary>
public abstract class AuthenticatedE2ETestBase(PlaywrightFixture playwright, string personaEmail)
    : E2ETestBase(playwright)
{
    private string? _statePath;

    protected override string? StorageStatePath => _statePath;

    public override async ValueTask InitializeAsync()
    {
        Assert.SkipUnless(E2EEnvironment.IsAvailable,
            "E2E environment not running — execute tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1 first.");
        _statePath = await AuthStateFactory.EnsureSignedInAsync(Playwright.Browser, personaEmail);
        await base.InitializeAsync();
    }
}

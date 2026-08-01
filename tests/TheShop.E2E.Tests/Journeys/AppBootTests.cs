using TheShop.E2E.Tests.Fixtures;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Proves the app boots and renders its layout under the E2E harness.</summary>
[Trait("Category", "E2E")]
[Trait("Suite", "Smoke")]
public sealed class AppBootTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task App_boots_and_renders_the_layout()
    {
        await Page.GotoAsync("/");
        // The host shell serves instantly; .mud-layout appears only after the WASM runtime
        // boots and MainLayout renders — this is the real "app is alive" signal.
        await Page.Locator(".mud-layout").WaitForAsync(new() { Timeout = 30_000 });
    }
}

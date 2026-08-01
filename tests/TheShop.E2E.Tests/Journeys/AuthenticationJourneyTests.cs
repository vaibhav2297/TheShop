using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// Authentication journeys (.specs/authentication/spec.md §6) beyond sign-in, which
/// <see cref="SignInJourneyTests"/> already covers (AC-2). AC-1's sign-up flow is out of scope
/// here — that spec's own SDD record is still Draft/unresolved, unlike every other feature this
/// plan covers.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "auth")]
public sealed class AuthenticationJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.CustomerEmail)
{
    [Fact]
    public async Task AC11_Signing_out_ends_the_session_and_returns_to_the_public_view()
    {
        try
        {
            await Page.GotoAsync("/");
            await Page.Locator(".mud-layout").WaitForAsync(new() { Timeout = 30_000 });

            // MudMenu wraps the activator in its own role="button" div, so the icon button's
            // aria-label matches twice — the outer wrapper is what actually receives the click.
            await Page.GetByRole(Microsoft.Playwright.AriaRole.Button, new() { Name = Strings.Nav_Account }).First.ClickAsync();
            await Page.GetByText(Strings.Logout, new() { Exact = true }).ClickAsync();

            await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath == WebRoutes.Home, new() { Timeout = 15_000 });
            await Page.WaitForFunctionAsync("() => !localStorage.getItem('shop.auth.session')", null,
                new() { Timeout = 15_000 });
        }
        finally
        {
            // SignOutCommand revokes the session server-side, so the cached storage state this
            // test started from is now dead. Delete it so any later test needing the Customer
            // persona mints a fresh one instead of reusing a revoked session.
            var path = AuthStateFactory.StatePath(AuthStateFactory.CustomerEmail);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

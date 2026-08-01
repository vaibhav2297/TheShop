using FluentAssertions;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Proves the whole Phase 2 chain: real OTP sign-in, storage-state reuse, session persistence.</summary>
[Trait("Category", "E2E")]
[Trait("Suite", "Smoke")]
[Trait("Feature", "auth")]
public sealed class SignInJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.CustomerEmail)
{
    [Fact]
    public async Task Signed_in_customer_lands_on_an_authenticated_page()
    {
        await Page.GotoAsync("/");
        await Page.Locator(".mud-layout").WaitForAsync(new() { Timeout = 30_000 });
        var session = await Page.EvaluateAsync<string?>("() => localStorage.getItem('shop.auth.session')");
        session.Should().NotBeNullOrEmpty("the OTP sign-in must persist a Supabase session");
    }
}

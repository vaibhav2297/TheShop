using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Guest access evidence for .specs/008_homepage-brand/spec.md AC-11.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "008_homepage-brand")]
public sealed class ManageHomepageBrandGuestJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task AC11_GuestCannotOpenEditor()
    {
        var editor = new ManageHomepageBrandPage(Page);
        await editor.GotoAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Regex("/sign-in"));
        await Assertions.Expect(editor.Root).ToHaveCountAsync(0);
    }
}

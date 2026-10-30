using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Anonymous routing evidence for .specs/003_manage-hero-banner/spec.md AC-3.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "003_manage-hero-banner")]
public sealed class ManageHeroBannerAnonymousJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task AC3_AnonymousUserCannotOpenHomepageManagement()
    {
        var editor = new ManageHeroBannerPage(Page);
        await editor.GotoAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/sign-in"));
        await Assertions.Expect(editor.Root).ToHaveCountAsync(0);
    }
}

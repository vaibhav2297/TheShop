using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>View-only browser/RPC evidence for .specs/005_homepage-featured/spec.md AC-4.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "005_homepage-featured")]
public sealed class ManageFeaturedViewOnlyJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.SupportEmail)
{
    [Fact]
    public async Task AC4_ViewOnlyAbsentAndHiddenStatesHaveNoMutationAndDirectRpcIsDenied()
    {
        using var data = new FeaturedData();
        await data.ResetAsync();
        var editor = new ManageFeaturedPage(Page);
        await editor.GotoAsync();
        await Assertions.Expect(editor.Root).ToHaveAttributeAsync("data-state", "absent");
        await Assertions.Expect(editor.Root).ToContainTextAsync(Strings.Featured_Absent);
        await Assertions.Expect(editor.Save).ToHaveCountAsync(0);
        await Assertions.Expect(editor.Root.Locator("input")).ToHaveCountAsync(0);
        await data.SeedAsync(await editor.ArtworkAsync());
        await editor.GotoAsync();
        await Assertions.Expect(editor.Root).ToHaveAttributeAsync("data-state", "edit");
        await Assertions.Expect(editor.Root).ToContainTextAsync(data.Title);
        await Assertions.Expect(editor.Root).ToContainTextAsync(Strings.Featured_Hidden);
        await Assertions.Expect(editor.Root.Locator("input")).ToHaveCountAsync(0);
        await Assertions.Expect(editor.Save).ToHaveCountAsync(0);
        await Assertions.Expect(editor.Cancel).ToHaveCountAsync(0);
        var token = await Page.EvaluateAsync<string>("()=>{let s=JSON.parse(localStorage.getItem('shop.auth.session'));return s.accessToken??s.AccessToken??s.access_token;}");
        token.Should().NotBeNullOrWhiteSpace();
        foreach (var create in new[] { true, false })
        {
            var response = await data.AsStaffAsync(token, FeaturedData.RpcParameters(create, "Forbidden", data.Key1, data.Key2));
            response.Status.Should().Be(403);
            response.Body.Should().Contain("42501");
        }
        (await data.RowsAsync())[0].GetProperty("title").GetString().Should().Be(data.Title);
    }
}

using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Guest and missing-View browser evidence for .specs/005_homepage-featured/spec.md AC-3.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "005_homepage-featured")]
public sealed class ManageFeaturedAnonymousJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task AC3_GuestRedirectsAndCustomerOrActionOnlyStaffCannotReadManagement()
    {
        var editor = new ManageFeaturedPage(Page);
        await editor.GotoAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/sign-in"));
        await Assertions.Expect(editor.Root).ToHaveCountAsync(0);
        using var data = new FeaturedData();
        foreach (var action in new[] { "", "create", "edit" })
        {
            var role = action.Length == 0 ? (Guid?)null : await data.GrantActionAsync(AuthStateFactory.CustomerEmail, action);
            File.Delete(AuthStateFactory.StatePath(AuthStateFactory.CustomerEmail));
            var state = await AuthStateFactory.EnsureSignedInAsync(Playwright.Browser, AuthStateFactory.CustomerEmail);
            await using var context = await ShopBrowser.NewContextAsync(Playwright.Browser, state);
            try
            {
                var page = await context.NewPageAsync();
                var denied = new ManageFeaturedPage(page);
                await denied.GotoAsync();
                await Assertions.Expect(denied.Root).ToHaveCountAsync(0);
                await Assertions.Expect(page.GetByText(Strings.AccessDenied_Title, new() { Exact = true })).ToBeVisibleAsync();
                if (action.Length > 0)
                {
                    var permissions = await page.EvaluateAsync<string[]>("()=>{const s=JSON.parse(localStorage.getItem('shop.auth.session'));const t=s.accessToken??s.AccessToken??s.access_token;return JSON.parse(atob(t.split('.')[1].replace(/-/g,'+').replace(/_/g,'/'))).perms;}");
                    permissions.Should().Contain("homepage." + action).And.NotContain("homepage.view");
                }
                await context.StorageStateAsync(new() { Path = state });
            }
            finally
            {
                if (role is { } granted) await data.RemoveRoleAsync(granted);
                File.Delete(AuthStateFactory.StatePath(AuthStateFactory.CustomerEmail));
            }
        }
    }
}

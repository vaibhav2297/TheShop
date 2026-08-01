using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Breadcrumb trail journeys (.specs/breadcrumbs/spec.md §6). Anonymous — storefront chrome.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "breadcrumbs")]
public sealed class BreadcrumbsJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task AC1_Storefront_page_shows_a_trail_from_home_to_the_current_page()
    {
        await Page.GotoAsync(WebRoutes.Products);
        await Page.Locator(".mud-layout").WaitForAsync(new() { Timeout = 30_000 });

        var trail = Page.GetByRole(AriaRole.Navigation, new() { Name = Strings.Breadcrumb_AriaLabel });
        // A clickable crumb (Href set) renders as a link; the disabled/current crumb (Href
        // null) renders as a plain <button> — GetByText would only match the inner text span,
        // not the element carrying aria-current, so target the two roles directly.
        await trail.GetByRole(AriaRole.Link, new() { Name = Strings.Nav_Home, Exact = true })
            .WaitForAsync(new() { Timeout = 15_000 });

        // AC-4: the final crumb is the current page and is not a link.
        var current = trail.GetByRole(AriaRole.Button, new() { Name = Strings.Nav_Products, Exact = true });
        await current.WaitForAsync(new() { Timeout = 15_000 });
        (await current.GetAttributeAsync("aria-current")).Should().Be("page");
    }

    [Fact]
    public async Task AC3_Clicking_a_non_final_level_navigates_to_that_page()
    {
        await Page.GotoAsync(WebRoutes.Products);
        await Page.Locator(".mud-layout").WaitForAsync(new() { Timeout = 30_000 });

        var trail = Page.GetByRole(AriaRole.Navigation, new() { Name = Strings.Breadcrumb_AriaLabel });
        await trail.GetByRole(AriaRole.Link, new() { Name = Strings.Nav_Home, Exact = true }).ClickAsync();

        await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath == WebRoutes.Home, new() { Timeout = 15_000 });
    }
}

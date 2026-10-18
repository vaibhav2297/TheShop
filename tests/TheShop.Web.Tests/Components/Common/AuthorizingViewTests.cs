using Bunit;
using FluentAssertions;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="AuthorizingView"/> — the full-page loading indicator shown by
/// <c>App.razor</c>'s <c>AuthorizeRouteView.Authorizing</c> template while the authentication state
/// (including the current user's permission set) resolves, preventing a flash of
/// <see cref="AccessDeniedView"/> on a cold load of a permission-gated route.
/// <see href=".specs/role-based-access-control/spec.md"/>
/// </summary>
public class AuthorizingViewTests : TestContext
{
    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_Always_ShowsTheLocalizedAuthorizingMessage()
    {
        var cut = Render<AuthorizingView>();

        cut.Markup.Should().Contain(Strings.Authorizing_Loading);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_Always_ShowsAnIndeterminateProgressIndicator()
    {
        var cut = Render<AuthorizingView>();

        cut.FindAll(".shop-loader").Should().HaveCount(1);
    }

    [Fact]
    public void Render_NativeStatus_ForwardsRootAttributesAndKeepsLoaderDecorative()
    {
        var cut = Render<AuthorizingView>(parameters => parameters
            .Add(component => component.Class, "custom-authorizing")
            .AddUnmatched("data-testid", "authorizing"));

        var status = cut.Find("[role=status]");
        status.ClassList.Should().Contain("custom-authorizing");
        status.GetAttribute("data-testid").Should().Be("authorizing");
        status.TextContent.Trim().Should().Be(Strings.Authorizing_Loading);
        cut.Find(".shop-loader").GetAttribute("aria-hidden").Should().Be("true");
        cut.FindAll("[class*='mud-']").Should().BeEmpty();
    }
}

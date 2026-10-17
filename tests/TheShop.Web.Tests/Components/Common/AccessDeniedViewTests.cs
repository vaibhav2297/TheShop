using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="AccessDeniedView"/> — the shared in-place access-denied content block
/// (Figma node <c>2470:2220</c>) rendered inside the normal page chrome at the attempted URL
/// (Behavior 3, AC-10). The view shows a single "Back to Home" button that always targets the
/// store home, regardless of the visitor's admin-area access.
/// <see href=".specs/role-based-access-control/spec.md"/>
/// </summary>
public class AccessDeniedViewTests : TestContext
{
    // =========================================================================
    // Title + message (Figma node 2470:2220)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_Always_ShowsTheLocalizedAccessDeniedTitle()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("staff-user");

        var cut = Render<AccessDeniedView>();

        cut.Markup.Should().Contain(Strings.AccessDenied_Title);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_Always_ShowsTheLocalizedAccessDeniedMessage()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("staff-user");

        var cut = Render<AccessDeniedView>();

        cut.Markup.Should().Contain(Strings.AccessDenied_Message);
    }

    // =========================================================================
    // Back button — a single "Back to Home" link that always targets the store home
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_Always_ShowsASingleBackToHomeButton()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("staff-user");
        authContext.SetPolicies(PolicyNames.AdminDashboard);

        var cut = Render<AccessDeniedView>();

        cut.FindAll("a.shop-button").Should().HaveCount(1);
        cut.Markup.Should().Contain(Strings.AccessDenied_BackToHome);
        cut.Find("a.shop-button").GetAttribute("href").Should().Be(Routes.Home);
    }

    [Fact]
    public void Render_UsesNativeHeadingAndForwardsStylingAndAttributesToTheRoot()
    {
        var cut = Render<AccessDeniedView>(p => p
            .Add(c => c.Class, "custom")
            .Add(c => c.Style, "max-width: 50rem")
            .AddUnmatched("data-probe", "denied"));

        var root = cut.Find("section.shop-access-denied");
        root.ClassList.Should().Contain("custom");
        root.GetAttribute("style").Should().Be("max-width: 50rem");
        root.GetAttribute("data-probe").Should().Be("denied");
        cut.Find("h1").TextContent.Should().Be(Strings.AccessDenied_Title);
        cut.FindAll("[class*='mud-']").Should().BeEmpty();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-10: Render_Always_ShowsTheLocalizedAccessDeniedTitle,
//         Render_Always_ShowsTheLocalizedAccessDeniedMessage,
//         Render_Always_ShowsASingleBackToHomeButton

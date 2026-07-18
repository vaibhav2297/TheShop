using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
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
    public AccessDeniedViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

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
        authContext.SetPolicies(PolicyNames.AdminArea);

        var cut = Render<AccessDeniedView>();

        cut.FindAll("a.mud-button-root").Should().HaveCount(1);
        cut.Markup.Should().Contain(Strings.AccessDenied_BackToHome);
        cut.Find("a.mud-button-root").GetAttribute("href").Should().Be(Routes.Home);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-10: Render_Always_ShowsTheLocalizedAccessDeniedTitle,
//         Render_Always_ShowsTheLocalizedAccessDeniedMessage,
//         Render_Always_ShowsASingleBackToHomeButton

using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
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
    public AuthorizingViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

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

        cut.FindAll(".mud-progress-circular").Should().NotBeEmpty();
    }
}

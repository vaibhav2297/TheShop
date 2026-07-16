using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using TheShop.Web.Pages.Admin;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Admin;

/// <summary>
/// Tests for the <see cref="ManageProducts"/> admin harness shell — the verification surface for
/// RBAC's admin gating (Figma node <c>2470:2200</c>). Additionally gated on <c>products.view</c>
/// within the page itself, rendering <c>AccessDeniedView</c> in place when that finer-grained
/// permission is missing. Permission policies resolve against the claims minted into the access
/// token; bUnit's auth context stands in for that principal here.
/// </summary>
public class ManageProductsTests : TestContext
{
    public ManageProductsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BreadcrumbState>();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    // =========================================================================
    // Held products.view permission — content renders
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_WhenUserHoldsProductsViewPermission_ShowsManageProductsContent()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Products.View.Code));

        var cut = Render<ManageProducts>();

        cut.Markup.Should().Contain(Strings.ManageProducts_Heading);
    }

    // =========================================================================
    // Missing products.view permission — AccessDeniedView renders in place, not the content
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_WhenUserLacksProductsViewPermission_ShowsAccessDeniedInsteadOfContent()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("support-user"); // authenticated, but no products.view policy granted

        var cut = Render<ManageProducts>();

        cut.Markup.Should().NotContain(Strings.ManageProducts_Heading,
            "an absent permission must hide the capability entirely, not merely disable it");
        cut.Markup.Should().Contain(Strings.AccessDenied_Title);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_WhenUserIsNotAuthenticated_ShowsAccessDeniedInsteadOfContent()
    {
        var authContext = this.AddAuthorization();
        authContext.SetNotAuthorized();

        var cut = Render<ManageProducts>();

        cut.Markup.Should().NotContain(Strings.ManageProducts_Heading);
        cut.Markup.Should().Contain(Strings.AccessDenied_Title);
    }

    // =========================================================================
    // Breadcrumb — set unconditionally in OnInitialized, independent of the permission gate
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Render_Always_SetsBreadcrumbTrailToManageProductsHeading()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin-user");
        authContext.SetPolicies(PolicyNames.Permission(PermissionCatalogue.Products.View.Code));

        Render<ManageProducts>();

        var breadcrumbs = Services.GetRequiredService<BreadcrumbState>();
        breadcrumbs.Trail.Should().Contain(item => item.Text == Strings.ManageProducts_Heading);
    }
}

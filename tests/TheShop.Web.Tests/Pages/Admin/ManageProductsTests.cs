using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
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
/// RBAC's admin gating (Figma node <c>2470:2200</c>). Gated on <c>products.view</c> by its
/// route-level <c>[AuthorizePermission]</c> attribute — the denied/redirect experience is
/// App.razor's router scaffolding, so this class asserts the structural seam. Permission
/// policies resolve against the claims minted into the access token; bUnit's auth context
/// stands in for that principal here.
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
    // Structural — the page carries its own products.view route-level policy; the
    // denied/redirect experience it triggers is App.razor's router scaffolding
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void ManageProducts_Always_CarriesTheProductsViewAuthorizePolicy()
    {
        var attributes = typeof(ManageProducts).GetCustomAttributes<AuthorizeAttribute>().ToList();

        attributes.Should().Contain(
            a => a.Policy == PolicyNames.Permission(PermissionCatalogue.Products.View.Code),
            "the route-level policy is the only gate between a non-viewer and the shell — " +
            "App.razor's NotAuthorized template renders the denied/redirect experience");
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

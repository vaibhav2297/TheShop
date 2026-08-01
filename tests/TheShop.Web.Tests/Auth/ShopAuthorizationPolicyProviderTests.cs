using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using Xunit;

namespace TheShop.Web.Tests.Auth;

/// <summary>
/// Tests for <see cref="TheShop.Web.Auth.ShopAuthorizationPolicyProvider"/>, exercised through
/// the real <see cref="DependencyInjection.AddPresentation"/> registrations so the tests cover
/// the production wiring: <c>perm:{code}</c> policies synthesized on demand from the permission
/// claims in the access token. The admin console gate (<see cref="PolicyNames.AdminDashboard"/>)
/// is one of these ordinary permission policies, on <c>dashboard.view</c> — no separate
/// admin-area policy exists.
/// </summary>
public class ShopAuthorizationPolicyProviderTests
{
    private readonly IAuthorizationService _authorization;

    public ShopAuthorizationPolicyProviderTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPresentation();
        _authorization = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal UserWithPermissions(params string[] permissionCodes) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                .. permissionCodes.Select(code => new Claim(TheShop.Web.Auth.ShopClaimTypes.Permission, code)),
            ],
            authenticationType: "TestAuth"));

    private static ClaimsPrincipal AnonymousUser() => new(new ClaimsIdentity());

    // =========================================================================
    // perm:{code} policies — synthesized on demand from the catalogue
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Authorize_WhenUserHoldsThePermissionClaim_Succeeds()
    {
        var user = UserWithPermissions(PermissionCatalogue.Products.View.Code);

        var result = await _authorization.AuthorizeAsync(
            user, null, PolicyNames.Permission(PermissionCatalogue.Products.View.Code));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Authorize_WhenUserHoldsADifferentPermissionClaim_Fails()
    {
        var user = UserWithPermissions(PermissionCatalogue.Orders.View.Code);

        var result = await _authorization.AuthorizeAsync(
            user, null, PolicyNames.Permission(PermissionCatalogue.Products.View.Code));

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Authorize_WhenUserIsAnonymous_FailsEvenIfAClaimWereForged()
    {
        var result = await _authorization.AuthorizeAsync(
            AnonymousUser(), null, PolicyNames.Permission(PermissionCatalogue.Products.View.Code));

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task Authorize_WhenPolicyNamesACodeOutsideTheCatalogue_ThrowsSoTyposFailFast()
    {
        var user = UserWithPermissions(PermissionCatalogue.Products.View.Code);

        var act = () => _authorization.AuthorizeAsync(user, null, PolicyNames.Permission("not.a-permission"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // =========================================================================
    // AdminDashboard — the admin console gate is an ordinary permission policy on
    // dashboard.view; no derived "admin area" concept remains
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void AdminDashboard_Always_IsThePermissionPolicyForTheDashboardViewCode()
    {
        // The constant exists only because [Authorize] attributes need compile-time values —
        // it must never drift from the catalogue's dashboard.view code.
        PolicyNames.AdminDashboard.Should().Be(
            PolicyNames.Permission(PermissionCatalogue.Dashboard.View.Code));
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AdminDashboard_WhenUserHoldsTheDashboardViewPermission_Succeeds()
    {
        var user = UserWithPermissions(PermissionCatalogue.Dashboard.View.Code);

        var result = await _authorization.AuthorizeAsync(user, null, PolicyNames.AdminDashboard);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AdminDashboard_WhenUserHoldsOtherScreenPermissionsButNotDashboardView_Fails()
    {
        // Holding another admin screen's permission no longer opens the console — the
        // dashboard is gated on its own permission exactly like every other screen.
        var user = UserWithPermissions(
            PermissionCatalogue.Brands.View.Code, PermissionCatalogue.Products.View.Code);

        var result = await _authorization.AuthorizeAsync(user, null, PolicyNames.AdminDashboard);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AdminDashboard_WhenAuthenticatedUserHoldsNoPermissionClaims_Fails()
    {
        var result = await _authorization.AuthorizeAsync(UserWithPermissions(), null, PolicyNames.AdminDashboard);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AdminDashboard_WhenUserIsAnonymous_Fails()
    {
        var result = await _authorization.AuthorizeAsync(AnonymousUser(), null, PolicyNames.AdminDashboard);

        result.Succeeded.Should().BeFalse();
    }
}

using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;
using Xunit;

namespace TheShop.Web.Tests.Auth;

/// <summary>
/// Tests for <see cref="TheShop.Web.Auth.ShopAuthorizationPolicyProvider"/> and the
/// <see cref="PolicyNames.AdminArea"/> policy, exercised through the real
/// <see cref="DependencyInjection.AddPresentation"/> registrations so the tests cover the
/// production wiring: <c>perm:{code}</c> policies synthesized on demand from the permission
/// claims in the access token, and the coarse admin-area gate satisfied by any permission claim.
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
    // AdminArea — any permission claim reaches the admin surface; none does not
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AdminArea_WhenUserHoldsAnyPermissionClaim_Succeeds()
    {
        var user = UserWithPermissions(PermissionCatalogue.Orders.View.Code);

        var result = await _authorization.AuthorizeAsync(user, null, PolicyNames.AdminArea);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AdminArea_WhenAuthenticatedUserHoldsNoPermissionClaims_Fails()
    {
        var result = await _authorization.AuthorizeAsync(UserWithPermissions(), null, PolicyNames.AdminArea);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AdminArea_WhenUserIsAnonymous_Fails()
    {
        var result = await _authorization.AuthorizeAsync(AnonymousUser(), null, PolicyNames.AdminArea);

        result.Succeeded.Should().BeFalse();
    }
}

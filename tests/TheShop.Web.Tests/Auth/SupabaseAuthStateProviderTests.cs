using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Web.Auth;
using Xunit;

namespace TheShop.Web.Tests.Auth;

/// <summary>
/// Tests for <see cref="SupabaseAuthStateProvider"/> — builds the <see cref="ClaimsPrincipal"/>
/// entirely from the current session's access token: identity claims plus the RBAC claims
/// (<c>perms</c> → <see cref="ShopClaimTypes.Permission"/>, <c>app_roles</c> →
/// <see cref="ClaimTypes.Role"/>) minted by the database's custom access token hook. A token
/// refresh republishes the principal via <see cref="IAuthService.AuthStateChanged"/>.
/// </summary>
public class SupabaseAuthStateProviderTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private static AuthSession SessionWithToken(string accessToken) =>
        new(Guid.NewGuid(), "staff@theshop.test", accessToken, string.Empty,
            DateTimeOffset.UtcNow.AddHours(1));

    /// <summary>
    /// Builds an unsigned (alg "none") JWT carrying the hook's claim shape. The provider only
    /// parses — it never validates a signature; Supabase already validated the token server-side.
    /// </summary>
    private static string BuildJwt(string[] perms, string[]? appRoles = null)
    {
        static string Encode(object payload) =>
            Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var header = Encode(new { alg = "none", typ = "JWT" });
        var body = Encode(new
        {
            sub = Guid.NewGuid().ToString(),
            perms,
            app_roles = appRoles ?? [],
            perm_v = 1,
        });
        return $"{header}.{body}.";
    }

    // =========================================================================
    // Permission claims come straight from the token
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task GetAuthenticationStateAsync_WhenTokenCarriesPermissions_MintsOnePermissionClaimPerCode()
    {
        _auth.CurrentSession.Returns(SessionWithToken(BuildJwt(["products.view", "orders.view"])));
        var provider = new SupabaseAuthStateProvider(_auth);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeTrue();
        state.User.HasClaim(ShopClaimTypes.Permission, "products.view").Should().BeTrue();
        state.User.HasClaim(ShopClaimTypes.Permission, "orders.view").Should().BeTrue();
        state.User.HasClaim(ShopClaimTypes.Permission, "orders.refund").Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task GetAuthenticationStateAsync_WhenTokenCarriesAppRoles_MintsStandardRoleClaims()
    {
        _auth.CurrentSession.Returns(SessionWithToken(BuildJwt([], appRoles: ["Support"])));
        var provider = new SupabaseAuthStateProvider(_auth);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.IsInRole("Support").Should().BeTrue();
        state.User.IsInRole("SuperAdmin").Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task GetAuthenticationStateAsync_Always_CarriesIdentityClaimsFromTheSession()
    {
        var session = SessionWithToken(BuildJwt([]));
        _auth.CurrentSession.Returns(session);
        var provider = new SupabaseAuthStateProvider(_auth);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(session.UserId.ToString());
        state.User.FindFirst(ClaimTypes.Email)!.Value.Should().Be(session.Email);
    }

    // =========================================================================
    // Anonymous and fail-closed paths
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task GetAuthenticationStateAsync_WhenNoSession_ReturnsAnonymous()
    {
        _auth.CurrentSession.Returns((AuthSession?)null);
        var provider = new SupabaseAuthStateProvider(_auth);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task GetAuthenticationStateAsync_WhenTokenIsMalformed_AuthenticatesWithZeroPermissionClaims()
    {
        _auth.CurrentSession.Returns(SessionWithToken("not-a-jwt"));
        var provider = new SupabaseAuthStateProvider(_auth);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeTrue();
        state.User.Claims.Should().NotContain(c => c.Type == ShopClaimTypes.Permission,
            "a token we cannot parse must deny every permission-gated surface, not grant one");
    }

    // =========================================================================
    // An auth-state change (sign-in/out, silent token refresh) republishes the principal
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public async Task AuthStateChanged_RebuildsThePrincipalFromTheCurrentToken()
    {
        _auth.CurrentSession.Returns(SessionWithToken(BuildJwt(["products.view"])));
        var provider = new SupabaseAuthStateProvider(_auth);

        (await provider.GetAuthenticationStateAsync())
            .User.HasClaim(ShopClaimTypes.Permission, "products.view").Should().BeTrue();

        // A refresh mints a token without the permission (revocation landed server-side).
        _auth.CurrentSession.Returns(SessionWithToken(BuildJwt([])));
        Task<AuthenticationState>? notified = null;
        provider.AuthenticationStateChanged += t => notified = t;

        _auth.AuthStateChanged += Raise.Event<Action>();

        notified.Should().NotBeNull();
        var state = await notified!;
        state.User.HasClaim(ShopClaimTypes.Permission, "products.view").Should().BeFalse();
    }
}

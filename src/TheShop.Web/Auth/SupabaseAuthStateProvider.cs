using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TheShop.Application.Common.Interfaces;

namespace TheShop.Web.Auth;

/// <summary>
/// Blazor <see cref="AuthenticationStateProvider"/> backed by <see cref="IAuthService"/>.
/// Builds the <see cref="ClaimsPrincipal"/> entirely from the current
/// <see cref="AuthSession"/>'s access token: identity claims (user id, email) plus the RBAC
/// claims stamped by the database's custom access token hook — one
/// <see cref="ShopClaimTypes.Permission"/> claim per granted permission code and one
/// <see cref="ClaimTypes.Role"/> claim per assigned role. Subscribes to
/// <see cref="IAuthService.AuthStateChanged"/> (sign-in, sign-out, silent token refresh) so the
/// principal — and with it every policy decision — follows the token. Client-side claims are a
/// UX mirror whose staleness is bounded by the access-token TTL; Supabase RLS remains the
/// authoritative authorization boundary.
/// </summary>
public sealed class SupabaseAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private readonly IAuthService _auth;

    public SupabaseAuthStateProvider(IAuthService auth)
    {
        _auth = auth;
        _auth.AuthStateChanged += NotifyChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(BuildPrincipal(_auth.CurrentSession)));

    /// <summary>
    /// Forces an immediate re-evaluation of the authentication state for all subscribers.
    /// Call this after manually updating <see cref="AuthState"/> (e.g., post OTP verification).
    /// </summary>
    public void NotifyChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static ClaimsPrincipal BuildPrincipal(AuthSession? session)
    {
        if (session is null)
            return Anonymous;

        List<Claim> claims = [new(ClaimTypes.NameIdentifier, session.UserId.ToString())];

        if (!string.IsNullOrWhiteSpace(session.Email))
            claims.Add(new Claim(ClaimTypes.Email, session.Email));

        AddRbacClaimsFromJwt(session.AccessToken, claims);

        var identity = new ClaimsIdentity(claims, authenticationType: "supabase");
        return new ClaimsPrincipal(identity);
    }

    private static void AddRbacClaimsFromJwt(string accessToken, List<Claim> claims)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return;

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

            // JSON array claims surface as repeated Claim instances of the same type.
            foreach (var c in jwt.Claims)
            {
                if (c.Type == ShopClaimTypes.JwtPermissions)
                    claims.Add(new Claim(ShopClaimTypes.Permission, c.Value));
                else if (c.Type == ShopClaimTypes.JwtAppRoles)
                    claims.Add(new Claim(ClaimTypes.Role, c.Value));
            }
        }
        catch
        {
            // Fail closed: a malformed token yields an authenticated user with zero RBAC
            // claims — every permission-gated surface denies until the next token refresh.
        }
    }

    public void Dispose() => _auth.AuthStateChanged -= NotifyChanged;
}

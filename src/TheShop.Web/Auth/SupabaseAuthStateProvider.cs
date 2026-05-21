using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TheShop.Application.Common.Interfaces;

namespace TheShop.Web.Auth;

public sealed class SupabaseAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private readonly IAuthService _auth;

    public SupabaseAuthStateProvider(IAuthService auth)
    {
        _auth = auth;
        _auth.AuthStateChanged += OnAuthStateChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(BuildPrincipal(_auth.CurrentSession)));

    public void NotifyChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private void OnAuthStateChanged() => NotifyChanged();

    private static ClaimsPrincipal BuildPrincipal(AuthSession? session)
    {
        if (session is null)
            return Anonymous;

        List<Claim> claims = [new(ClaimTypes.NameIdentifier, session.UserId.ToString())];

        if (!string.IsNullOrWhiteSpace(session.Email))
            claims.Add(new Claim(ClaimTypes.Email, session.Email));

        TryAddRoleFromJwt(session.AccessToken, claims);

        var identity = new ClaimsIdentity(claims, authenticationType: "supabase");
        return new ClaimsPrincipal(identity);
    }

    private static void TryAddRoleFromJwt(string accessToken, List<Claim> claims)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return;

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

            foreach (var c in jwt.Claims)
            {
                if (c.Type == "role" || c.Type == ClaimTypes.Role)
                    claims.Add(new Claim(ClaimTypes.Role, c.Value));
            }
        }
        catch
        {
            // A malformed token simply means no role claim — the user is still authenticated.
        }
    }

    public void Dispose() => _auth.AuthStateChanged -= OnAuthStateChanged;
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Common;

namespace TheShop.Web.Auth;

/// <summary>
/// Synthesizes one <c>perm:{code}</c> authorization policy on demand for every permission in
/// <see cref="PermissionCatalogue"/>, instead of pre-registering a policy per catalogue entry.
/// A policy is satisfied by an authenticated user whose principal carries the matching
/// <see cref="ShopClaimTypes.Permission"/> claim (minted into the JWT by the database's custom
/// access token hook). A <c>perm:</c> policy naming a code outside the catalogue resolves to
/// no policy at all, so a typo fails fast at authorization time rather than silently denying.
/// Policy names without the <c>perm:</c> prefix fall through to the
/// default provider.
/// </summary>
public sealed class ShopAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    /// <inheritdoc/>
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PolicyNames.TryGetPermissionCode(policyName, out var code) &&
            PermissionCatalogue.IsDefined(code))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(ShopClaimTypes.Permission, code)
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}

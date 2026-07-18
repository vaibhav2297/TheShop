namespace TheShop.Web.Auth;

/// <summary>
/// Claim-type constants for the RBAC claims that the Supabase custom access token hook stamps
/// into every access token, and the <see cref="System.Security.Claims.ClaimsPrincipal"/> claim
/// types they are mapped to by <see cref="SupabaseAuthStateProvider"/>.
/// </summary>
public static class ShopClaimTypes
{
    /// <summary>
    /// Principal claim type carrying one granted permission code per claim
    /// (e.g. <c>products.view</c>). Authorization policies and
    /// <c>ICurrentUserService.HasPermission</c> read this type.
    /// </summary>
    public const string Permission = "perm";

    /// <summary>Raw JWT claim holding the permission-code array minted by the token hook.</summary>
    public const string JwtPermissions = "perms";

    /// <summary>Raw JWT claim holding the role name array minted by the token hook.</summary>
    public const string JwtAppRoles = "app_roles";
}

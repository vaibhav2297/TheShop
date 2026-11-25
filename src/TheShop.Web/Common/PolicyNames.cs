namespace TheShop.Web.Common;

/// <summary>
/// Centralised Blazor authorization-policy name constants. Every <c>[Authorize(Policy = ...)]</c>
/// / <c>&lt;AuthorizeView Policy="..."&gt;</c> in Web references a value from this class rather
/// than a magic string, mirroring the <see cref="Routes"/> / <see cref="BusyKeys"/> pattern.
/// </summary>
public static class PolicyNames
{
    /// <summary>
    /// The admin console screen's own permission policy (<c>dashboard.view</c>), applied by the
    /// <c>AdminConsole</c> page and the admin nav link. This is an ordinary <c>perm:{code}</c>
    /// permission policy — it exists as a named constant only because <c>[Authorize]</c>
    /// attributes require compile-time values. It must always equal
    /// <c>Permission(PermissionCatalogue.Dashboard.View.Code)</c> (guarded by test); there is
    /// no separate "admin area" policy concept.
    /// </summary>
    public const string AdminDashboard = PermissionPolicyPrefix + "dashboard.view";

    /// <summary>
    /// Prefix identifying a fine-grained permission policy name. Policies carrying this prefix
    /// are synthesized on demand by <c>ShopAuthorizationPolicyProvider</c> — nothing is
    /// pre-registered per catalogue entry.
    /// </summary>
    public const string PermissionPolicyPrefix = "perm:";

    /// <summary>
    /// Builds the fine-grained policy name for a single permission code (e.g.
    /// <c>products.view</c> → <c>perm:products.view</c>).
    /// </summary>
    public static string Permission(string permissionCode) =>
        $"{PermissionPolicyPrefix}{permissionCode}";

    /// <summary>
    /// Extracts the permission code from a <c>perm:</c>-prefixed policy name. Returns
    /// <c>false</c> (with an empty <paramref name="permissionCode"/>) for any other policy name.
    /// </summary>
    public static bool TryGetPermissionCode(string policyName, out string permissionCode)
    {
        if (policyName.StartsWith(PermissionPolicyPrefix, StringComparison.Ordinal))
        {
            permissionCode = policyName[PermissionPolicyPrefix.Length..];
            return true;
        }

        permissionCode = string.Empty;
        return false;
    }
}

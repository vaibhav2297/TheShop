namespace TheShop.Web.Common;

/// <summary>
/// Centralised Blazor authorization-policy name constants. Every <c>[Authorize(Policy = ...)]</c>
/// / <c>&lt;AuthorizeView Policy="..."&gt;</c> in Web references a value from this class rather
/// than a magic string, mirroring the <see cref="Routes"/> / <see cref="BusyKeys"/> pattern.
/// </summary>
public static class PolicyNames
{
    /// <summary>
    /// Matches any authenticated user holding at least one permission. Every permission in the
    /// catalogue is admin-area this release, so this is the coarse "can reach the admin surface
    /// at all" gate applied by <c>Pages/Admin/_Imports.razor</c> and the admin nav link.
    /// </summary>
    public const string AdminArea = "AdminArea";

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

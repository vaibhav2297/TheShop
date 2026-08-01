using Microsoft.AspNetCore.Authorization;
using TheShop.Web.Common;

namespace TheShop.Web.Auth;

/// <summary>
/// Route-level authorization on a single fine-grained permission code — the Web twin of the
/// Application layer's <c>[RequiresPermission]</c>. Equivalent to
/// <c>[Authorize(Policy = PolicyNames.Permission(code))]</c>, which cannot be written directly
/// because attribute arguments must be compile-time constants; this attribute composes the
/// <c>perm:{code}</c> policy name at construction instead. The router's
/// <c>NotAuthorized</c> template in <c>App.razor</c> renders the denied/redirect experience
/// when the policy fails.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AuthorizePermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Requires the given permission code (e.g. <c>brands.create</c>) to access the page,
    /// resolved through the on-demand <c>perm:{code}</c> policy machinery.
    /// </summary>
    public AuthorizePermissionAttribute(string permissionCode)
    {
        Policy = PolicyNames.Permission(permissionCode);
    }
}

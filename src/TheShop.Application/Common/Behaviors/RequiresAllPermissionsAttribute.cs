namespace TheShop.Application.Common.Behaviors;

/// <summary>
/// Requires every listed permission before a MediatR command or query runs.
/// When combined with other permission attributes, every requirement must pass.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiresAllPermissionsAttribute : Attribute
{
    /// <summary>Declares a nonempty set of required permission codes from the permission catalogue.</summary>
    /// <exception cref="ArgumentException">The list is empty or contains a blank code.</exception>
    /// <exception cref="ArgumentNullException">The list is null.</exception>
    public RequiresAllPermissionsAttribute(params string[] permissionCodes)
    {
        ArgumentNullException.ThrowIfNull(permissionCodes);
        if (permissionCodes.Length == 0 || permissionCodes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one permission is required, and codes must not be blank.", nameof(permissionCodes));

        PermissionCodes = Array.AsReadOnly((string[])permissionCodes.Clone());
    }

    /// <summary>The permission codes the caller must hold together.</summary>
    public IReadOnlyList<string> PermissionCodes { get; }
}

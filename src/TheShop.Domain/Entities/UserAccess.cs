using TheShop.Domain.ValueObjects;

namespace TheShop.Domain.Entities;

/// <summary>
/// A user's role assignments — the aggregate that resolves effective permissions for
/// authorization decisions. Effective permissions are the union of every permission granted by
/// any assigned role (FR-4).
/// </summary>
public sealed class UserAccess
{
    public Guid UserId { get; }

    private readonly List<Role> _roles;

    public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();

    private UserAccess(Guid userId, IEnumerable<Role> roles)
    {
        UserId = userId;
        _roles = [.. roles];
    }

    /// <summary>
    /// Reconstructs a <see cref="UserAccess"/> from persisted role assignments without
    /// re-running creation invariants. Use only from repository mappers.
    /// </summary>
    public static UserAccess Rehydrate(Guid userId, IEnumerable<Role> roles) => new(userId, roles);

    /// <summary>
    /// The union of every permission granted by any of this user's assigned roles (FR-4).
    /// </summary>
    public IReadOnlySet<Permission> EffectivePermissions() =>
        _roles.SelectMany(r => r.Permissions).ToHashSet();

    /// <summary>
    /// <c>true</c> when any assigned role grants <paramref name="permission"/>.
    /// </summary>
    public bool HasPermission(Permission permission) =>
        _roles.Any(r => r.Permissions.Contains(permission));

    /// <summary>
    /// <c>true</c> when any assigned role grants at least one admin-area permission
    /// (<see cref="PermissionCatalogue.IsAdminArea"/>).
    /// </summary>
    public bool HoldsAdminAreaPermission() =>
        _roles.SelectMany(r => r.Permissions).Any(p => PermissionCatalogue.IsAdminArea(p.Code));
}

using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Domain.Entities;

/// <summary>
/// A named collection of permissions grantable to users. System roles — the four built-in roles
/// seeded by migration — are immutable: <see cref="Grant"/>, <see cref="Revoke"/>, and
/// <see cref="Rename"/> all throw <see cref="SystemRoleImmutableException"/> for them (FR-2).
/// This mutation surface exists for future runtime RBAC administration; nothing calls it this
/// release, since role configuration is provisioned read-only via migration seed data.
/// </summary>
public sealed class Role
{
    private const string NameKeyRequiredKey = "Rbac_Role_NameKeyRequired";

    public Guid Id { get; }

    /// <summary>
    /// A resource key identifying this role's display name (e.g. <c>Support</c>).
    /// Localization happens in Web (FR-2/AC-11) — Domain never holds display text.
    /// </summary>
    public string NameKey { get; private set; }

    public bool IsSystem { get; }

    private readonly List<Permission> _permissions;

    public IReadOnlyCollection<Permission> Permissions => _permissions.AsReadOnly();

    private Role(Guid id, string nameKey, bool isSystem, IEnumerable<Permission> permissions)
    {
        Id = id;
        NameKey = nameKey;
        IsSystem = isSystem;
        _permissions = [.. permissions];
    }

    /// <summary>
    /// Reconstructs a <see cref="Role"/> from persisted data without re-running creation
    /// invariants. Use only from repository mappers.
    /// </summary>
    public static Role Rehydrate(Guid id, string nameKey, bool isSystem, IEnumerable<Permission> permissions) =>
        new(id, nameKey, isSystem, permissions);

    /// <summary>
    /// Grants <paramref name="permission"/> to this role, if not already granted.
    /// </summary>
    /// <exception cref="SystemRoleImmutableException">Thrown when <see cref="IsSystem"/> is <c>true</c>.</exception>
    public void Grant(Permission permission)
    {
        if (IsSystem)
            throw new SystemRoleImmutableException(Id);

        if (!_permissions.Contains(permission))
            _permissions.Add(permission);
    }

    /// <summary>
    /// Revokes <paramref name="permission"/> from this role, if currently granted.
    /// </summary>
    /// <exception cref="SystemRoleImmutableException">Thrown when <see cref="IsSystem"/> is <c>true</c>.</exception>
    public void Revoke(Permission permission)
    {
        if (IsSystem)
            throw new SystemRoleImmutableException(Id);

        _permissions.RemoveAll(p => p.Equals(permission));
    }

    /// <summary>
    /// Renames this role's display resource key.
    /// </summary>
    /// <exception cref="SystemRoleImmutableException">Thrown when <see cref="IsSystem"/> is <c>true</c>.</exception>
    /// <exception cref="DomainException">Thrown when <paramref name="nameKey"/> is blank.</exception>
    public void Rename(string nameKey)
    {
        if (IsSystem)
            throw new SystemRoleImmutableException(Id);

        if (string.IsNullOrWhiteSpace(nameKey))
            throw new DomainException(NameKeyRequiredKey);

        NameKey = nameKey.Trim();
    }
}

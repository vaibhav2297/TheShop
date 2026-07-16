namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a mutation (<c>Grant</c>, <c>Revoke</c>, or <c>Rename</c>) is attempted on a
/// system role. The four built-in roles are immutable this release (FR-2).
/// </summary>
public sealed class SystemRoleImmutableException(Guid roleId) : DomainException(MessageResourceKey)
{
    public const string MessageResourceKey = "Rbac_SystemRoleImmutable";

    /// <summary>
    /// The identifier of the system role the mutation was attempted on.
    /// </summary>
    public Guid RoleId { get; } = roleId;
}

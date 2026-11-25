using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="Role"/> — system-role immutability (FR-2) and the
/// grant/revoke/rename mutation surface that exists for future runtime RBAC administration.
/// <see href=".specs/role-based-access-control/spec.md"/>
/// </summary>
public class RoleTests
{
    private static Role BuildCustomRole(params Permission[] permissions) =>
        Role.Rehydrate(Guid.NewGuid(), "CustomRole", isSystem: false, permissions);

    private static Role BuildSystemRole(params Permission[] permissions) =>
        Role.Rehydrate(Guid.NewGuid(), "Support", isSystem: true, permissions);

    // =========================================================================
    // Rehydrate — reconstructs without re-running invariants
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Rehydrate_WithPersistedData_ReturnsRoleWithExactProperties()
    {
        var id = Guid.NewGuid();
        var permission = PermissionCatalogue.Orders.View;

        var role = Role.Rehydrate(id, "Support", isSystem: true, [permission]);

        role.Id.Should().Be(id);
        role.NameKey.Should().Be("Support");
        role.IsSystem.Should().BeTrue();
        role.Permissions.Should().ContainSingle().Which.Should().Be(permission);
    }

    // =========================================================================
    // Grant — non-system role (AC-2's mutation surface, unused this release)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Grant_OnNonSystemRole_AddsPermission()
    {
        var role = BuildCustomRole();

        role.Grant(PermissionCatalogue.Products.View);

        role.Permissions.Should().ContainSingle().Which.Should().Be(PermissionCatalogue.Products.View);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Grant_WhenPermissionAlreadyGranted_DoesNotDuplicate()
    {
        var role = BuildCustomRole(PermissionCatalogue.Products.View);

        role.Grant(PermissionCatalogue.Products.View);

        role.Permissions.Should().ContainSingle();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Grant_OnSystemRole_ThrowsSystemRoleImmutableException()
    {
        var role = BuildSystemRole();

        var act = () => role.Grant(PermissionCatalogue.Products.View);

        act.Should().Throw<SystemRoleImmutableException>()
           .Which.RoleId.Should().Be(role.Id);
    }

    // =========================================================================
    // Revoke — non-system role
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Revoke_OnNonSystemRole_RemovesPermission()
    {
        var role = BuildCustomRole(PermissionCatalogue.Products.View, PermissionCatalogue.Products.Edit);

        role.Revoke(PermissionCatalogue.Products.View);

        role.Permissions.Should().ContainSingle().Which.Should().Be(PermissionCatalogue.Products.Edit);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Revoke_WhenPermissionNotGranted_IsANoOp()
    {
        var role = BuildCustomRole(PermissionCatalogue.Products.Edit);

        var act = () => role.Revoke(PermissionCatalogue.Products.View);

        act.Should().NotThrow();
        role.Permissions.Should().ContainSingle().Which.Should().Be(PermissionCatalogue.Products.Edit);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Revoke_OnSystemRole_ThrowsSystemRoleImmutableException()
    {
        var role = BuildSystemRole(PermissionCatalogue.Orders.View);

        var act = () => role.Revoke(PermissionCatalogue.Orders.View);

        act.Should().Throw<SystemRoleImmutableException>()
           .Which.RoleId.Should().Be(role.Id);
    }

    // =========================================================================
    // Rename — non-system role
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Rename_OnNonSystemRole_UpdatesNameKey()
    {
        var role = BuildCustomRole();

        role.Rename("UpdatedName");

        role.NameKey.Should().Be("UpdatedName");
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Rename_WithSurroundingWhitespace_Trims()
    {
        var role = BuildCustomRole();

        role.Rename("  UpdatedName  ");

        role.NameKey.Should().Be("UpdatedName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "role-based-access-control")]
    public void Rename_OnNonSystemRoleWithBlankName_ThrowsDomainException(string blankName)
    {
        var role = BuildCustomRole();

        var act = () => role.Rename(blankName);

        act.Should().Throw<DomainException>()
           .Which.MessageKey.Should().Be("Rbac_Role_NameKeyRequired");
    }

    // =========================================================================
    // Rename — system role (FR-2: built-in roles cannot be renamed)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Rename_OnSystemRole_ThrowsSystemRoleImmutableException()
    {
        var role = BuildSystemRole();

        var act = () => role.Rename("Hacked");

        act.Should().Throw<SystemRoleImmutableException>()
           .Which.RoleId.Should().Be(role.Id);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-2: Grant_OnSystemRole_ThrowsSystemRoleImmutableException,
//        Revoke_OnSystemRole_ThrowsSystemRoleImmutableException,
//        Rename_OnSystemRole_ThrowsSystemRoleImmutableException
//        (built-in roles offer no way to be renamed, deleted, or have their permissions edited)

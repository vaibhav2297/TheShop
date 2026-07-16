using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="UserAccess"/> — resolving effective permissions as the union of every
/// role a user holds (FR-4), and detecting admin-area reach for the coarse admin-panel gate
/// (FR-6).
/// <see href=".specs/role-based-access-control/spec.md"/>
/// </summary>
public class UserAccessTests
{
    private static Role SupportRole() =>
        Role.Rehydrate(Guid.NewGuid(), "Support", isSystem: true,
            [PermissionCatalogue.Orders.View, PermissionCatalogue.Orders.Edit, PermissionCatalogue.Customers.View]);

    private static Role AdminRole() =>
        Role.Rehydrate(Guid.NewGuid(), "Admin", isSystem: true,
            [PermissionCatalogue.Orders.View, PermissionCatalogue.Products.View, PermissionCatalogue.Products.Edit]);

    private static Role CustomerRole() =>
        Role.Rehydrate(Guid.NewGuid(), "Customer", isSystem: true, []);

    // =========================================================================
    // EffectivePermissions — union across all assigned roles (FR-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void EffectivePermissions_WithSingleRole_ReturnsThatRolesPermissions()
    {
        var access = UserAccess.Rehydrate(Guid.NewGuid(), [SupportRole()]);

        access.EffectivePermissions().Should().BeEquivalentTo(
        [
            PermissionCatalogue.Orders.View,
            PermissionCatalogue.Orders.Edit,
            PermissionCatalogue.Customers.View,
        ]);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void EffectivePermissions_WithMultipleRoles_ReturnsUnionOfAllRolesPermissions()
    {
        var access = UserAccess.Rehydrate(Guid.NewGuid(), [SupportRole(), AdminRole()]);

        access.EffectivePermissions().Should().BeEquivalentTo(
        [
            PermissionCatalogue.Orders.View,
            PermissionCatalogue.Orders.Edit,
            PermissionCatalogue.Customers.View,
            PermissionCatalogue.Products.View,
            PermissionCatalogue.Products.Edit,
        ]);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void EffectivePermissions_WhenTwoRolesShareAPermission_DeduplicatesInTheUnion()
    {
        // Both roles grant orders.view — the union must not report it twice.
        var access = UserAccess.Rehydrate(Guid.NewGuid(), [SupportRole(), AdminRole()]);

        access.EffectivePermissions().Count(p => p == PermissionCatalogue.Orders.View).Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void EffectivePermissions_WithNoRoles_ReturnsEmptySet()
    {
        var access = UserAccess.Rehydrate(Guid.NewGuid(), []);

        access.EffectivePermissions().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void EffectivePermissions_WithOnlyTheCustomerRole_ReturnsEmptySet()
    {
        // AC-1: a newly signed-up user holds exactly the Customer role, which grants nothing.
        var access = UserAccess.Rehydrate(Guid.NewGuid(), [CustomerRole()]);

        access.EffectivePermissions().Should().BeEmpty();
    }

    // =========================================================================
    // HasPermission
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void HasPermission_WhenAnyRoleGrantsIt_ReturnsTrue()
    {
        var access = UserAccess.Rehydrate(Guid.NewGuid(), [SupportRole()]);

        access.HasPermission(PermissionCatalogue.Customers.View).Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void HasPermission_WhenNoRoleGrantsIt_ReturnsFalse()
    {
        var access = UserAccess.Rehydrate(Guid.NewGuid(), [SupportRole()]);

        access.HasPermission(PermissionCatalogue.Settings.View).Should().BeFalse();
    }

    // =========================================================================
    // HoldsAdminAreaPermission — the coarse admin-panel reachability gate (FR-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void HoldsAdminAreaPermission_WhenUserHoldsAnyCataloguePermission_ReturnsTrue()
    {
        var access = UserAccess.Rehydrate(Guid.NewGuid(), [SupportRole()]);

        access.HoldsAdminAreaPermission().Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void HoldsAdminAreaPermission_WhenUserOnlyHoldsTheCustomerRole_ReturnsFalse()
    {
        // AC-1: a Customer-only user cannot reach the admin panel.
        var access = UserAccess.Rehydrate(Guid.NewGuid(), [CustomerRole()]);

        access.HoldsAdminAreaPermission().Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void HoldsAdminAreaPermission_WhenUserHasNoRolesAtAll_ReturnsFalse()
    {
        var access = UserAccess.Rehydrate(Guid.NewGuid(), []);

        access.HoldsAdminAreaPermission().Should().BeFalse();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: EffectivePermissions_WithOnlyTheCustomerRole_ReturnsEmptySet,
//        HoldsAdminAreaPermission_WhenUserOnlyHoldsTheCustomerRole_ReturnsFalse
// AC-4/AC-5 (union-of-roles decision basis for UI/action gating):
//        EffectivePermissions_WithMultipleRoles_ReturnsUnionOfAllRolesPermissions,
//        HasPermission_WhenAnyRoleGrantsIt_ReturnsTrue

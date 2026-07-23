using FluentAssertions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for <see cref="PermissionCatalogue"/> — the single source of truth for every
/// permission code, organized by admin module (FR-3, AC-3).
/// <see href=".specs/role-based-access-control/spec.md"/>
/// </summary>
public class PermissionCatalogueTests
{
    // The ten modules the spec requires, each gated by its own permissions (AC-3).
    private static readonly string[] ExpectedModules =
    [
        "products", "categories", "orders", "customers", "coupons",
        "promotions", "reports", "settings", "admin_users", "roles",
    ];

    // =========================================================================
    // Every spec module is represented, with view/create/edit/delete at minimum (AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void All_Always_ContainsEveryModuleListedInTheSpec()
    {
        var modulesInCatalogue = PermissionCatalogue.All.Select(p => p.Module).Distinct();

        modulesInCatalogue.Should().BeEquivalentTo(ExpectedModules);
    }

    [Theory]
    [InlineData("products")]
    [InlineData("categories")]
    [InlineData("orders")]
    [InlineData("customers")]
    [InlineData("coupons")]
    [InlineData("promotions")]
    [InlineData("reports")]
    [InlineData("settings")]
    [InlineData("admin_users")]
    [InlineData("roles")]
    [Trait("Feature", "role-based-access-control")]
    public void All_ForEveryModule_IncludesViewCreateEditAndDeleteAtMinimum(string module)
    {
        var actions = PermissionCatalogue.All.Where(p => p.Module == module).Select(p => p.Action);

        actions.Should().Contain(["view", "create", "edit", "delete"]);
    }

    // =========================================================================
    // Module-specific sensitive actions (spec FR-3: "issuing a refund or exporting customer data")
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Orders_Always_IncludesRefundAsASensitiveAction()
    {
        PermissionCatalogue.Orders.Refund.Code.Should().Be("orders.refund");
        PermissionCatalogue.All.Should().Contain(PermissionCatalogue.Orders.Refund);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Customers_Always_IncludesExportAsASensitiveAction()
    {
        PermissionCatalogue.Customers.Export.Code.Should().Be("customers.export");
        PermissionCatalogue.All.Should().Contain(PermissionCatalogue.Customers.Export);
    }

    // =========================================================================
    // Codes are unique across the whole catalogue — the seed generation source (FR-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void All_Always_HasNoDuplicateCodes()
    {
        var codes = PermissionCatalogue.All.Select(p => p.Code).ToList();

        codes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void All_Always_ContainsExactlyFortyTwoPermissions()
    {
        // 8 modules x 4 actions (view/create/edit/delete) + Orders.Refund + Customers.Export
        PermissionCatalogue.All.Should().HaveCount(42);
    }

    // =========================================================================
    // IsAdminArea — every catalogue module is admin-area this release
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void IsAdminArea_ForEveryCataloguePermission_ReturnsTrue()
    {
        PermissionCatalogue.All.Should().OnlyContain(p => PermissionCatalogue.IsAdminArea(p.Code));
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void IsAdminArea_ForACodeNotInTheCatalogue_ReturnsFalse()
    {
        PermissionCatalogue.IsAdminArea("unknown.view").Should().BeFalse();
    }

    // =========================================================================
    // add-brand: the Brands module (Decision 2) — the four brands.* codes exist (TASK-004, AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void All_Always_IncludesTheBrandsModule()
    {
        PermissionCatalogue.All.Select(p => p.Module).Should().Contain("brands");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Brands_Always_ExposesViewCreateEditAndDeleteWithTheExpectedCodes()
    {
        PermissionCatalogue.Brands.View.Code.Should().Be("brands.view");
        PermissionCatalogue.Brands.Create.Code.Should().Be("brands.create");
        PermissionCatalogue.Brands.Edit.Code.Should().Be("brands.edit");
        PermissionCatalogue.Brands.Delete.Code.Should().Be("brands.delete");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void All_Always_ContainsAllFourBrandsPermissions()
    {
        PermissionCatalogue.All.Should().Contain(
        [
            PermissionCatalogue.Brands.View, PermissionCatalogue.Brands.Create,
            PermissionCatalogue.Brands.Edit, PermissionCatalogue.Brands.Delete,
        ]);
    }
}

// =============================================================================
// AC → Test mapping (role-based-access-control)
// =============================================================================
// AC-3: All_Always_ContainsEveryModuleListedInTheSpec,
//        All_ForEveryModule_IncludesViewCreateEditAndDeleteAtMinimum,
//        Orders_Always_IncludesRefundAsASensitiveAction,
//        Customers_Always_IncludesExportAsASensitiveAction,
//        All_Always_HasNoDuplicateCodes
//
// NOTE (flagged for the user, not modified here — out of scope for the add-brand test-writer
// invocation): add-brand's Decision 2 extends PermissionCatalogue.All with the Brands module (4
// more codes, 1 more module). All_Always_ContainsExactlyFortyTwoPermissions (expects 42) and
// All_Always_ContainsEveryModuleListedInTheSpec (expects exactly the pre-add-brand 10 modules)
// are now stale and will fail once add-brand's PermissionCatalogue change is compiled in. These
// belong to the role-based-access-control feature's own regression suite and were left untouched
// per the append-only rule; they should be updated (42 → 46, module list +"brands") the next time
// that feature's tests are revisited.

// =============================================================================
// AC → Test mapping (add-brand)
// =============================================================================
// AC-8: All_Always_IncludesTheBrandsModule, Brands_Always_ExposesViewCreateEditAndDeleteWithTheExpectedCodes,
//        All_Always_ContainsAllFourBrandsPermissions
//        (the four brands.* codes existing is a prerequisite for the brands.create authorization
//        boundary; the boundary itself is exercised in CreateBrandCommandTests and the
//        Infrastructure RLS tests)

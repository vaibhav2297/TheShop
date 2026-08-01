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
    // The ten modules the spec requires (AC-3), plus the Brands module added by the
    // add-brand feature (its Decision 2), plus the single-action Dashboard module that
    // gates the admin console shell itself.
    private static readonly string[] ExpectedModules =
    [
        "products", "categories", "orders", "customers", "coupons",
        "promotions", "reports", "settings", "admin_users", "roles", "brands",
        "dashboard",
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
    [InlineData("brands")]
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
    public void All_Always_ContainsExactlyFortySevenPermissions()
    {
        // 11 modules x 4 actions (view/create/edit/delete) + Orders.Refund + Customers.Export
        // + the single-action Dashboard module (dashboard.view only)
        PermissionCatalogue.All.Should().HaveCount(47);
    }

    // =========================================================================
    // Dashboard — the admin console shell's own single-action module
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Dashboard_Always_ExposesOnlyTheViewPermission()
    {
        PermissionCatalogue.Dashboard.View.Code.Should().Be("dashboard.view");
        PermissionCatalogue.All.Should().Contain(PermissionCatalogue.Dashboard.View);
        PermissionCatalogue.All.Where(p => p.Module == "dashboard")
            .Should().ContainSingle("the admin console shell is view-only — there is nothing to create, edit, or delete");
    }

    // =========================================================================
    // IsDefined — catalogue membership, the fail-fast check for perm:{code} policies
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void IsDefined_ForEveryCataloguePermission_ReturnsTrue()
    {
        PermissionCatalogue.All.Should().OnlyContain(p => PermissionCatalogue.IsDefined(p.Code));
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void IsDefined_ForACodeNotInTheCatalogue_ReturnsFalse()
    {
        PermissionCatalogue.IsDefined("unknown.view").Should().BeFalse();
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

// =============================================================================
// AC → Test mapping (add-brand)
// =============================================================================
// AC-8: All_Always_IncludesTheBrandsModule, Brands_Always_ExposesViewCreateEditAndDeleteWithTheExpectedCodes,
//        All_Always_ContainsAllFourBrandsPermissions
//        (the four brands.* codes existing is a prerequisite for the brands.create authorization
//        boundary; the boundary itself is exercised in CreateBrandCommandTests and the
//        Infrastructure RLS tests)

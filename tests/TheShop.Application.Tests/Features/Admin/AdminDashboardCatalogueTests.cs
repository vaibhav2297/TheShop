using FluentAssertions;
using TheShop.Application.Features.Admin;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Admin;

/// <summary>
/// Structural tests for <see cref="AdminDashboardCatalogue"/> — the fixed set of five governed
/// modules, in display order (spec FR-2), each paired with the exact permission code that gates
/// its card (spec FR-5 / RULE-2; plan §4/§10 table: Products→products.view,
/// Categories→categories.view, Brands→brands.view, Users→admin_users.view, Roles→roles.view).
/// The handler's own gating/placeholder behavior is covered separately in
/// <see cref="TheShop.Application.Tests.Features.Admin.Queries.GetAdminDashboard.GetAdminDashboardHandlerTests"/>.
/// <see href=".specs/admin-console/spec.md"/>
/// </summary>
public class AdminDashboardCatalogueTests
{
    [Fact]
    [Trait("Feature", "admin-console")]
    public void Modules_Always_ContainsExactlyTheFiveGovernedModulesInDisplayOrder()
    {
        AdminDashboardCatalogue.Modules.Select(m => m.Module).Should().Equal(
            AdminModule.Products, AdminModule.Categories, AdminModule.Brands, AdminModule.Users, AdminModule.Roles);
    }

    [Theory]
    [InlineData(AdminModule.Products)]
    [InlineData(AdminModule.Categories)]
    [InlineData(AdminModule.Brands)]
    [InlineData(AdminModule.Users)]
    [InlineData(AdminModule.Roles)]
    [Trait("Feature", "admin-console")]
    public void Modules_Always_ContainsExactlyOneEntryPerModule(AdminModule module)
    {
        AdminDashboardCatalogue.Modules.Should().ContainSingle(m => m.Module == module);
    }

    [Fact]
    [Trait("Feature", "admin-console")]
    public void Modules_Always_PairsEachModuleWithItsCataloguePermissionCode()
    {
        var byModule = AdminDashboardCatalogue.Modules.ToDictionary(m => m.Module, m => m.ViewPermissionCode);

        byModule[AdminModule.Products].Should().Be(PermissionCatalogue.Products.View.Code);
        byModule[AdminModule.Categories].Should().Be(PermissionCatalogue.Categories.View.Code);
        byModule[AdminModule.Brands].Should().Be(PermissionCatalogue.Brands.View.Code);
        byModule[AdminModule.Users].Should().Be(PermissionCatalogue.AdminUsers.View.Code,
            "the Users card must be gated by the admin_users module, not a customers-facing permission");
        byModule[AdminModule.Roles].Should().Be(PermissionCatalogue.Roles.View.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1 (supports): Modules_Always_ContainsExactlyTheFiveGovernedModulesInDisplayOrder,
//                   Modules_Always_ContainsExactlyOneEntryPerModule
// AC-3 (supports): Modules_Always_PairsEachModuleWithItsCataloguePermissionCode

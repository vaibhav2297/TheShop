using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Products.Queries.GetAdminProductFilters;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Structural test asserting <see cref="GetAdminProductFiltersQuery"/> is wired into the RBAC
/// pipeline with the <c>products.view</c> permission (AC-18).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class GetAdminProductFiltersQueryTests
{
    [Fact]
    [Trait("Feature", "manage-product")]
    public void GetAdminProductFiltersQuery_Always_IsDecoratedWithTheProductsViewPermissionRequirement()
    {
        var attribute = typeof(GetAdminProductFiltersQuery).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Products.View.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-18: GetAdminProductFiltersQuery_Always_IsDecoratedWithTheProductsViewPermissionRequirement

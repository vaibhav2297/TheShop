using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Structural test asserting <see cref="GetAdminProductsPageQuery"/> is wired into the RBAC
/// pipeline with the <c>products.view</c> permission (AC-18).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class GetAdminProductsPageQueryTests
{
    [Fact]
    [Trait("Feature", "manage-product")]
    public void GetAdminProductsPageQuery_Always_IsDecoratedWithTheProductsViewPermissionRequirement()
    {
        var attribute = typeof(GetAdminProductsPageQuery).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Products.View.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-18: GetAdminProductsPageQuery_Always_IsDecoratedWithTheProductsViewPermissionRequirement

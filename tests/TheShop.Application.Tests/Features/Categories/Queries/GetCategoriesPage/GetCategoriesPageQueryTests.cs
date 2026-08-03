using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Categories.Queries.GetCategoriesPage;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Queries.GetCategoriesPage;

/// <summary>
/// Structural test asserting <see cref="GetCategoriesPageQuery"/> is wired into the RBAC pipeline
/// with the <c>categories.view</c> permission (spec constraint: "gated by its own fine-grained
/// permission", AC-21).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class GetCategoriesPageQueryTests
{
    [Fact]
    [Trait("Feature", "manage-categories")]
    public void GetCategoriesPageQuery_Always_IsDecoratedWithTheCategoriesViewPermissionRequirement()
    {
        var attribute = typeof(GetCategoriesPageQuery).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Categories.View.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-21: GetCategoriesPageQuery_Always_IsDecoratedWithTheCategoriesViewPermissionRequirement

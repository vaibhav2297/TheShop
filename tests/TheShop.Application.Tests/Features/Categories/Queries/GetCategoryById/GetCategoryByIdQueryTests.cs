using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Categories.Queries.GetCategoryById;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Queries.GetCategoryById;

/// <summary>
/// Structural test asserting <see cref="GetCategoryByIdQuery"/> is wired into the RBAC pipeline
/// with the <c>categories.view</c> permission, not <c>categories.edit</c> (plan §5 Decision 9 —
/// reading a category is a read; the edit page's own <c>AuthorizePermission</c> gate, not this
/// query, is what denies a view-only admin's direct link, AC-20).
/// <see href=".specs/manage-categories/spec.md"/>
/// <see href=".specs/manage-categories/plan.md"/>
/// </summary>
public class GetCategoryByIdQueryTests
{
    [Fact]
    [Trait("Feature", "manage-categories")]
    public void GetCategoryByIdQuery_Always_IsDecoratedWithTheCategoriesViewPermissionRequirement()
    {
        var attribute = typeof(GetCategoryByIdQuery).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Categories.View.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-20: GetCategoryByIdQuery_Always_IsDecoratedWithTheCategoriesViewPermissionRequirement
//         (confirms the query itself requires only categories.view — the page-level categories.edit
//         gate is what denies a view-only admin's direct link, per plan Decision 9)

using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Brands.Queries.GetBrandById;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Queries.GetBrandById;

/// <summary>
/// Structural test asserting <see cref="GetBrandByIdQuery"/> is wired into the RBAC pipeline with
/// the <c>brands.view</c> permission, not <c>brands.edit</c> (plan §5 Decision 11 — reading a brand
/// is a read; the edit page's own <c>AuthorizeView</c> wrapper, not this query, is what denies a
/// view-only admin's direct link to the edit form, AC-16).
/// <see href=".specs/manage-brands/spec.md"/>
/// <see href=".specs/manage-brands/plan.md"/>
/// </summary>
public class GetBrandByIdQueryTests
{
    [Fact]
    [Trait("Feature", "manage-brands")]
    public void GetBrandByIdQuery_Always_IsDecoratedWithTheBrandsViewPermissionRequirement()
    {
        var attribute = typeof(GetBrandByIdQuery).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Brands.View.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-16: GetBrandByIdQuery_Always_IsDecoratedWithTheBrandsViewPermissionRequirement
//         (confirms the query itself requires only brands.view — the page-level brands.edit gate
//         is what denies a view-only admin's direct link, per plan Decision 11)

using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Brands.Queries.GetBrandsPage;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Queries.GetBrandsPage;

/// <summary>
/// Structural test asserting <see cref="GetBrandsPageQuery"/> is wired into the RBAC pipeline with
/// the <c>brands.view</c> permission (spec Constraint: "Each capability is gated by its own
/// fine-grained permission ... never by an 'is an admin' shortcut", RULE-8, AC-17). The generic
/// <see cref="AuthorizationBehavior{TRequest, TResponse}"/> enforcement mechanism itself is already
/// covered by the role-based-access-control feature's own <c>AuthorizationBehaviorTests</c>; this
/// test only verifies manage-brands wired the correct permission code onto the query.
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class GetBrandsPageQueryTests
{
    [Fact]
    [Trait("Feature", "manage-brands")]
    public void GetBrandsPageQuery_Always_IsDecoratedWithTheBrandsViewPermissionRequirement()
    {
        var attribute = typeof(GetBrandsPageQuery).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Brands.View.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-17: GetBrandsPageQuery_Always_IsDecoratedWithTheBrandsViewPermissionRequirement

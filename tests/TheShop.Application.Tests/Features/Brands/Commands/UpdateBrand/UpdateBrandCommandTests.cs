using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Brands.Commands.UpdateBrand;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.UpdateBrand;

/// <summary>
/// Structural test asserting <see cref="UpdateBrandCommand"/> is wired into the RBAC pipeline with
/// the <c>brands.edit</c> permission (spec Constraint: "gated by its own fine-grained permission",
/// RULE-8, AC-16).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class UpdateBrandCommandTests
{
    [Fact]
    [Trait("Feature", "manage-brands")]
    public void UpdateBrandCommand_Always_IsDecoratedWithTheBrandsEditPermissionRequirement()
    {
        var attribute = typeof(UpdateBrandCommand).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Brands.Edit.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-16: UpdateBrandCommand_Always_IsDecoratedWithTheBrandsEditPermissionRequirement

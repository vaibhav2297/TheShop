using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Brands.Commands.DeleteBrands;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.DeleteBrands;

/// <summary>
/// Structural test asserting <see cref="DeleteBrandsCommand"/> is wired into the RBAC pipeline
/// with the <c>brands.delete</c> permission — the single-row delete and the bulk delete share this
/// one command (plan §5 Decision 1), so a staff member without the permission is refused "by direct
/// means" too (FR-21, AC-26, RULE-8).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class DeleteBrandsCommandTests
{
    [Fact]
    [Trait("Feature", "manage-brands")]
    public void DeleteBrandsCommand_Always_IsDecoratedWithTheBrandsDeletePermissionRequirement()
    {
        var attribute = typeof(DeleteBrandsCommand).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Brands.Delete.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-13: DeleteBrandsCommand_Always_IsDecoratedWithTheBrandsDeletePermissionRequirement
// AC-26: DeleteBrandsCommand_Always_IsDecoratedWithTheBrandsDeletePermissionRequirement
//         (an edit-but-not-delete holder's direct attempt is refused by this gate)

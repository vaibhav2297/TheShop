using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Categories.Commands.DeleteCategories;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.DeleteCategories;

/// <summary>
/// Structural test asserting <see cref="DeleteCategoriesCommand"/> is wired into the RBAC pipeline
/// with the <c>categories.delete</c> permission — the single-row delete and the bulk delete share
/// this one command (plan §5 Decision 4), so a staff member without the permission is refused "by
/// direct means" too (AC-31).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class DeleteCategoriesCommandTests
{
    [Fact]
    [Trait("Feature", "manage-categories")]
    public void DeleteCategoriesCommand_Always_IsDecoratedWithTheCategoriesDeletePermissionRequirement()
    {
        var attribute = typeof(DeleteCategoriesCommand).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Categories.Delete.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-17: DeleteCategoriesCommand_Always_IsDecoratedWithTheCategoriesDeletePermissionRequirement
// AC-31: DeleteCategoriesCommand_Always_IsDecoratedWithTheCategoriesDeletePermissionRequirement
//         (an edit-but-not-delete holder's direct attempt is refused by this gate)

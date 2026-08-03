using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Categories.Commands.UpdateCategory;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.UpdateCategory;

/// <summary>
/// Structural test asserting <see cref="UpdateCategoryCommand"/> is wired into the RBAC pipeline
/// with the <c>categories.edit</c> permission (spec constraint: "gated by its own fine-grained
/// permission", AC-8).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class UpdateCategoryCommandTests
{
    [Fact]
    [Trait("Feature", "manage-categories")]
    public void UpdateCategoryCommand_Always_IsDecoratedWithTheCategoriesEditPermissionRequirement()
    {
        var attribute = typeof(UpdateCategoryCommand).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Categories.Edit.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-8: UpdateCategoryCommand_Always_IsDecoratedWithTheCategoriesEditPermissionRequirement

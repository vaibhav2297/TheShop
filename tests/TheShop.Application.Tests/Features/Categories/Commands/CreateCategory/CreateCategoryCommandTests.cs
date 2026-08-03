using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Categories.Commands.CreateCategory;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.CreateCategory;

/// <summary>
/// Structural test asserting <see cref="CreateCategoryCommand"/> is wired into the RBAC pipeline
/// with the correct permission code (spec constraint: "gated by a specific fine-grained
/// permission ... never by an 'is an admin' shortcut", AC-20).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class CreateCategoryCommandTests
{
    [Fact]
    [Trait("Feature", "manage-categories")]
    public void CreateCategoryCommand_Always_IsDecoratedWithTheCategoriesCreatePermissionRequirement()
    {
        var attribute = typeof(CreateCategoryCommand).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Categories.Create.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-20: CreateCategoryCommand_Always_IsDecoratedWithTheCategoriesCreatePermissionRequirement

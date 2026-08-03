using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Categories.Commands.SetCategoryStatus;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.SetCategoryStatus;

/// <summary>
/// Structural test asserting <see cref="SetCategoryStatusCommand"/> is wired into the RBAC
/// pipeline with the <c>categories.edit</c> permission — the single inline toggle and the bulk
/// activate/deactivate action share this one command (plan §5 Decision 4).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class SetCategoryStatusCommandTests
{
    [Fact]
    [Trait("Feature", "manage-categories")]
    public void SetCategoryStatusCommand_Always_IsDecoratedWithTheCategoriesEditPermissionRequirement()
    {
        var attribute = typeof(SetCategoryStatusCommand).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Categories.Edit.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-16: SetCategoryStatusCommand_Always_IsDecoratedWithTheCategoriesEditPermissionRequirement
// AC-28: SetCategoryStatusCommand_Always_IsDecoratedWithTheCategoriesEditPermissionRequirement

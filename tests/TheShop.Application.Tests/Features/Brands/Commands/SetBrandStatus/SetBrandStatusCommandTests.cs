using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Brands.Commands.SetBrandStatus;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.SetBrandStatus;

/// <summary>
/// Structural test asserting <see cref="SetBrandStatusCommand"/> is wired into the RBAC pipeline
/// with the <c>brands.edit</c> permission — the single inline toggle and the bulk
/// activate/deactivate action share this one command (plan §5 Decision 1), so one permission
/// covers both (FR-18, FR-20, RULE-8).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class SetBrandStatusCommandTests
{
    [Fact]
    [Trait("Feature", "manage-brands")]
    public void SetBrandStatusCommand_Always_IsDecoratedWithTheBrandsEditPermissionRequirement()
    {
        var attribute = typeof(SetBrandStatusCommand).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Brands.Edit.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-21: SetBrandStatusCommand_Always_IsDecoratedWithTheBrandsEditPermissionRequirement
// AC-23: SetBrandStatusCommand_Always_IsDecoratedWithTheBrandsEditPermissionRequirement

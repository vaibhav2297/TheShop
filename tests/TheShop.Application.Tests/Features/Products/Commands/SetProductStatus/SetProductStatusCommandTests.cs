using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Products.Commands.SetProductStatus;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.SetProductStatus;

/// <summary>
/// Structural test asserting <see cref="SetProductStatusCommand"/> is wired into the RBAC
/// pipeline with the <c>products.edit</c> permission — the single inline toggle and the bulk
/// activate/deactivate action share this one command (plan §5 Decision 2), so one permission
/// covers both (AC-10, AC-11, AC-18).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class SetProductStatusCommandTests
{
    [Fact]
    [Trait("Feature", "manage-product")]
    public void SetProductStatusCommand_Always_IsDecoratedWithTheProductsEditPermissionRequirement()
    {
        var attribute = typeof(SetProductStatusCommand).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Products.Edit.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-10, AC-11, AC-18: SetProductStatusCommand_Always_IsDecoratedWithTheProductsEditPermissionRequirement

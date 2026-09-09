using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Products.Commands.DeleteProducts;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.DeleteProducts;

/// <summary>
/// Structural test asserting <see cref="DeleteProductsCommand"/> is wired into the RBAC pipeline
/// with the <c>products.delete</c> permission — the single-row delete and the bulk delete share
/// this one command (plan §5 Decision 2), so a staff member without the permission is refused
/// "by direct means" too (AC-13, AC-18).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class DeleteProductsCommandTests
{
    [Fact]
    [Trait("Feature", "manage-product")]
    public void DeleteProductsCommand_Always_IsDecoratedWithTheProductsDeletePermissionRequirement()
    {
        var attribute = typeof(DeleteProductsCommand).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Products.Delete.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-13, AC-18: DeleteProductsCommand_Always_IsDecoratedWithTheProductsDeletePermissionRequirement

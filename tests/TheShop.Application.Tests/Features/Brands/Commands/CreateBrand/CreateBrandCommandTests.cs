using System.Reflection;
using FluentAssertions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Features.Brands.Commands.CreateBrand;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.CreateBrand;

/// <summary>
/// Structural test asserting <see cref="CreateBrandCommand"/> is wired into the RBAC pipeline
/// with the correct permission code (spec Constraint §4: "gated by a specific fine-grained
/// permission ... never by an 'is an admin' shortcut", AC-8). The generic
/// <see cref="AuthorizationBehavior{TRequest, TResponse}"/> enforcement mechanism itself — what
/// happens when the required permission is present/absent — is already covered by the
/// role-based-access-control feature's own <c>AuthorizationBehaviorTests</c>; this test only
/// verifies add-brand wired the correct permission code onto the command.
/// <see href=".specs/add-brand/spec.md"/>
/// </summary>
public class CreateBrandCommandTests
{
    [Fact]
    [Trait("Feature", "add-brand")]
    public void CreateBrandCommand_Always_IsDecoratedWithTheBrandsCreatePermissionRequirement()
    {
        var attribute = typeof(CreateBrandCommand).GetCustomAttribute<RequiresPermissionAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PermissionCode.Should().Be(PermissionCatalogue.Brands.Create.Code);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-8: CreateBrandCommand_Always_IsDecoratedWithTheBrandsCreatePermissionRequirement

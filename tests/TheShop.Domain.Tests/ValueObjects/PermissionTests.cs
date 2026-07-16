using FluentAssertions;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for the <see cref="Permission"/> value object — format validation and equality for
/// permission codes in <c>module.action</c> form (FR-3).
/// <see href=".specs/role-based-access-control/spec.md"/>
/// </summary>
public class PermissionTests
{
    // =========================================================================
    // Create — happy path
    // =========================================================================

    [Theory]
    [InlineData("products.view", "products", "view")]
    [InlineData("orders.refund", "orders", "refund")]
    [InlineData("admin_users.view", "admin_users", "view")]
    [Trait("Feature", "role-based-access-control")]
    public void Create_WithValidModuleDotActionCode_ParsesModuleAndAction(string code, string expectedModule, string expectedAction)
    {
        var permission = Permission.Create(code);

        permission.Code.Should().Be(code);
        permission.Module.Should().Be(expectedModule);
        permission.Action.Should().Be(expectedAction);
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Create_WhenCodeHasSurroundingWhitespace_TrimsBeforeStoring()
    {
        var permission = Permission.Create("  products.view  ");

        permission.Code.Should().Be("products.view");
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void ToString_Always_ReturnsCode()
    {
        var permission = Permission.Create("orders.view");

        permission.ToString().Should().Be("orders.view");
    }

    // =========================================================================
    // Create — validation failures
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "role-based-access-control")]
    public void Create_WhenCodeIsNullOrWhitespace_ThrowsInvalidPermissionException(string? code)
    {
        var act = () => Permission.Create(code!);

        act.Should().Throw<InvalidPermissionException>()
           .Which.MessageKey.Should().Be(InvalidPermissionException.MessageResourceKey);
    }

    [Theory]
    [InlineData("productsview")]        // no dot
    [InlineData("products.")]           // missing action
    [InlineData(".view")]               // missing module
    [InlineData("products.view.extra")] // too many segments
    [InlineData("Products.View")]       // uppercase not allowed
    [InlineData("products.view!")]      // disallowed character
    [InlineData("123.view")]            // digits not allowed
    [Trait("Feature", "role-based-access-control")]
    public void Create_WhenCodeDoesNotMatchModuleDotActionFormat_ThrowsInvalidPermissionException(string code)
    {
        var act = () => Permission.Create(code);

        act.Should().Throw<InvalidPermissionException>()
           .Which.AttemptedCode.Should().Be(code);
    }

    // =========================================================================
    // Equality — by code
    // =========================================================================

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Equals_WhenCodesMatch_ReturnsTrue()
    {
        var a = Permission.Create("products.view");
        var b = Permission.Create("products.view");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Equals_WhenComparedToNull_ReturnsFalse()
    {
        var a = Permission.Create("products.view");

        a.Equals(null).Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Equals_WhenCodesDiffer_ReturnsFalse()
    {
        var a = Permission.Create("products.view");
        var b = Permission.Create("products.edit");

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void GetHashCode_WhenCodesMatch_AreEqual()
    {
        var a = Permission.Create("products.view");
        var b = Permission.Create("products.view");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-3: Create_WithValidModuleDotActionCode_ParsesModuleAndAction,
//        Create_WhenCodeDoesNotMatchModuleDotActionFormat_ThrowsInvalidPermissionException
//        (permission code format is the atomic unit every module/action gate is built from)

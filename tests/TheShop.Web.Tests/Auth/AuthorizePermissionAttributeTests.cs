using FluentAssertions;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using Xunit;

namespace TheShop.Web.Tests.Auth;

/// <summary>
/// Tests for <see cref="AuthorizePermissionAttribute"/> — the route-level twin of the
/// Application layer's <c>[RequiresPermission]</c>, which must compose exactly the same
/// <c>perm:{code}</c> policy name that <c>ShopAuthorizationPolicyProvider</c> synthesizes.
/// </summary>
public class AuthorizePermissionAttributeTests
{
    [Fact]
    [Trait("Feature", "role-based-access-control")]
    public void Ctor_ForAPermissionCode_ComposesThePermPrefixedPolicyName()
    {
        var attribute = new AuthorizePermissionAttribute("brands.create");

        attribute.Policy.Should().Be(PolicyNames.Permission("brands.create"));
    }
}

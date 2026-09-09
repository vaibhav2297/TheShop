using FluentAssertions;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Domain.Tests.Enums;

/// <summary>
/// Tests for <see cref="ProductStatusFilter"/> — the optional status narrowing on the admin
/// manage-products list (FR-4). Modelled with no "all" member: a <see langword="null"/>
/// <c>ProductStatusFilter?</c> means no narrowing, so the enum itself only ever names Active or
/// Inactive.
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class ProductStatusFilterTests
{
    [Fact]
    [Trait("Feature", "manage-product")]
    public void ProductStatusFilter_DefinesExactlyTwoOptions()
    {
        Enum.GetValues<ProductStatusFilter>().Should().HaveCount(2);
    }

    [Theory]
    [InlineData(ProductStatusFilter.Active)]
    [InlineData(ProductStatusFilter.Inactive)]
    [Trait("Feature", "manage-product")]
    public void ProductStatusFilter_EachSpecOptionIsDefined(ProductStatusFilter status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1, AC-4 (status takes one value or no restriction): ProductStatusFilter_DefinesExactlyTwoOptions,
//        ProductStatusFilter_EachSpecOptionIsDefined

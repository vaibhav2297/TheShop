using FluentAssertions;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Domain.Tests.Enums;

/// <summary>
/// Tests for <see cref="AdminProductSortOption"/> — the six sort orders offered on the admin
/// manage-products list (FR-5, plan §4). Separate from the storefront's
/// <see cref="ProductSortOption"/>: the admin list sorts on the lowest variant price.
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class AdminProductSortOptionTests
{
    [Fact]
    [Trait("Feature", "manage-product")]
    public void AdminProductSortOption_DefinesExactlySixOptions()
    {
        Enum.GetValues<AdminProductSortOption>().Should().HaveCount(6);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void AdminProductSortOption_NameAToZIsTheDefaultValue()
    {
        // FR-5: "Name ascending is default." default(AdminProductSortOption) must be NameAToZ so
        // any code that forgets to set Sort explicitly still gets the spec's default order.
        default(AdminProductSortOption).Should().Be(AdminProductSortOption.NameAToZ);
    }

    [Theory]
    [InlineData(AdminProductSortOption.NameAToZ)]
    [InlineData(AdminProductSortOption.NameZToA)]
    [InlineData(AdminProductSortOption.NewestFirst)]
    [InlineData(AdminProductSortOption.OldestFirst)]
    [InlineData(AdminProductSortOption.LowestVariantPriceAsc)]
    [InlineData(AdminProductSortOption.LowestVariantPriceDesc)]
    [Trait("Feature", "manage-product")]
    public void AdminProductSortOption_EachSpecOptionIsDefined(AdminProductSortOption option)
    {
        Enum.IsDefined(option).Should().BeTrue();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-5, AC-28 (every FR-5 sort order, price sorts use lowest variant price): AdminProductSortOption_DefinesExactlySixOptions,
//        AdminProductSortOption_EachSpecOptionIsDefined
// AC-1 (name ascending default): AdminProductSortOption_NameAToZIsTheDefaultValue

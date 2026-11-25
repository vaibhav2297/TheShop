using FluentAssertions;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Domain.Tests.Enums;

/// <summary>
/// Tests for <see cref="ProductSortOption"/> — the fixed sort set from spec constraint
/// "The catalogue can be sorted by: Newest (the default), Price: low → high,
/// Price: high → low, Name: A → Z, and Name: Z → A." (FR-7, AC-7).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class ProductSortOptionTests
{
    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ProductSortOption_DefinesExactlyFiveOptions()
    {
        Enum.GetValues<ProductSortOption>().Should().HaveCount(5);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ProductSortOption_NewestFirstIsTheDefaultValue()
    {
        // The default(ProductSortOption) must be NewestFirst so any code that forgets to set
        // Sort explicitly still gets the spec's default sort order.
        default(ProductSortOption).Should().Be(ProductSortOption.NewestFirst);
    }

    [Theory]
    [InlineData(ProductSortOption.NewestFirst)]
    [InlineData(ProductSortOption.PriceLowToHigh)]
    [InlineData(ProductSortOption.PriceHighToLow)]
    [InlineData(ProductSortOption.NameAToZ)]
    [InlineData(ProductSortOption.NameZToA)]
    [Trait("Feature", "product-catalogue")]
    public void ProductSortOption_EachSpecOptionIsDefined(ProductSortOption option)
    {
        Enum.IsDefined(option).Should().BeTrue();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-7: ProductSortOption_DefinesExactlyFiveOptions, ProductSortOption_NewestFirstIsTheDefaultValue,
//        ProductSortOption_EachSpecOptionIsDefined

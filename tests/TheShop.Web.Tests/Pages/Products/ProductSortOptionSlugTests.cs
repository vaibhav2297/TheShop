using FluentAssertions;
using TheShop.Domain.Enums;
using TheShop.Web.Pages.Products;
using Xunit;

namespace TheShop.Web.Tests.Pages.Products;

/// <summary>
/// Tests for <see cref="ProductSortOptionSlug"/> — the stable enum↔URL-slug mapping that keeps the
/// numeric <see cref="ProductSortOption"/> out of shareable links.
/// </summary>
public class ProductSortOptionSlugTests
{
    [Theory]
    [Trait("Feature", "product-catalogue")]
    [InlineData(ProductSortOption.NewestFirst)]
    [InlineData(ProductSortOption.PriceLowToHigh)]
    [InlineData(ProductSortOption.PriceHighToLow)]
    [InlineData(ProductSortOption.NameAToZ)]
    [InlineData(ProductSortOption.NameZToA)]
    public void ToSlug_ThenFromSlug_RoundTripsEveryOption(ProductSortOption sort)
    {
        ProductSortOptionSlug.FromSlug(sort.ToSlug()).Should().Be(sort);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToSlug_UsesStableHumanReadableSlugs()
    {
        // Pinning the slugs guards against an accidental rename that would silently break every
        // shared link.
        ProductSortOption.PriceLowToHigh.ToSlug().Should().Be("price-asc");
        ProductSortOption.PriceHighToLow.ToSlug().Should().Be("price-desc");
        ProductSortOption.NameAToZ.ToSlug().Should().Be("name-asc");
        ProductSortOption.NameZToA.ToSlug().Should().Be("name-desc");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void FromSlug_UnknownSlug_FallsBackToNewestFirst()
    {
        ProductSortOptionSlug.FromSlug("not-a-real-slug").Should().Be(ProductSortOption.NewestFirst);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void FromSlug_Null_FallsBackToNewestFirst()
    {
        ProductSortOptionSlug.FromSlug(null).Should().Be(ProductSortOption.NewestFirst);
    }
}

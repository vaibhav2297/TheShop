using FluentAssertions;
using TheShop.Domain.Enums;
using TheShop.Web.Common.Sorting;
using Xunit;

namespace TheShop.Web.Tests.Common.Sorting;

[Trait("Feature", "product-catalogue")]
public class ProductSortCatalogueTests : SortCatalogueTests<ProductSortOption>
{
    protected override SortCatalogue<ProductSortOption> Catalogue => ProductSortCatalogue.Instance;

    [Fact]
    public void Default_IsNewestFirst()
    {
        // The catalogue opens newest-first (spec FR-7, AC-7), and the default is what an omitted
        // `sort` parameter means — changing it silently changes every existing bare link.
        ProductSortCatalogue.Instance.Default.Should().Be(ProductSortOption.NewestFirst);
    }

    [Fact]
    public void Slugs_MatchTheSharedVocabulary()
    {
        // Pinning the slugs guards against a rename that would silently break every shared link.
        var catalogue = ProductSortCatalogue.Instance;

        catalogue.ToSlug(ProductSortOption.NewestFirst).Should().Be("newest");
        catalogue.ToSlug(ProductSortOption.PriceLowToHigh).Should().Be("price-asc");
        catalogue.ToSlug(ProductSortOption.PriceHighToLow).Should().Be("price-desc");
        catalogue.ToSlug(ProductSortOption.NameAToZ).Should().Be("name-asc");
        catalogue.ToSlug(ProductSortOption.NameZToA).Should().Be("name-desc");
    }
}

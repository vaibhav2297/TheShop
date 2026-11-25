using FluentAssertions;
using TheShop.Domain.Enums;
using TheShop.Web.Common.Sorting;
using Xunit;

namespace TheShop.Web.Tests.Common.Sorting;

[Trait("Feature", "manage-brands")]
public class BrandSortCatalogueTests : SortCatalogueTests<BrandSortOption>
{
    protected override SortCatalogue<BrandSortOption> Catalogue => BrandSortCatalogue.Instance;

    [Fact]
    public void Default_IsNameAToZ()
    {
        // The manage-brands list opens alphabetically (spec FR-5), and the default is what an
        // omitted `sort` parameter means — changing it silently changes every existing bare link.
        BrandSortCatalogue.Instance.Default.Should().Be(BrandSortOption.NameAToZ);
    }

    [Fact]
    public void Slugs_MatchTheSharedVocabulary()
    {
        // Pinning the slugs guards against a rename that would silently break shared links, and
        // against the brand list drifting away from the catalogue's tokens for the same concepts.
        var catalogue = BrandSortCatalogue.Instance;

        catalogue.ToSlug(BrandSortOption.NameAToZ).Should().Be("name-asc");
        catalogue.ToSlug(BrandSortOption.NameZToA).Should().Be("name-desc");
        catalogue.ToSlug(BrandSortOption.NewestFirst).Should().Be("newest");
    }
}

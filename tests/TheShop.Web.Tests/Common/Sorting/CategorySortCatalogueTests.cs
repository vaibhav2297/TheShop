using FluentAssertions;
using TheShop.Domain.Enums;
using TheShop.Web.Common.Sorting;
using Xunit;

namespace TheShop.Web.Tests.Common.Sorting;

[Trait("Feature", "manage-categories")]
public class CategorySortCatalogueTests : SortCatalogueTests<CategorySortOption>
{
    protected override SortCatalogue<CategorySortOption> Catalogue => CategorySortCatalogue.Instance;

    [Fact]
    public void Default_IsNameAToZ()
    {
        // The manage-categories list opens alphabetically (spec FR-5), and the default is what an
        // omitted `sort` parameter means — changing it silently changes every existing bare link.
        CategorySortCatalogue.Instance.Default.Should().Be(CategorySortOption.NameAToZ);
    }

    [Fact]
    public void Slugs_MatchTheSharedVocabulary()
    {
        // Pinning the slugs guards against a rename that would silently break shared links, and
        // against the category list drifting away from the catalogue's tokens for the same concepts.
        var catalogue = CategorySortCatalogue.Instance;

        catalogue.ToSlug(CategorySortOption.NameAToZ).Should().Be("name-asc");
        catalogue.ToSlug(CategorySortOption.NameZToA).Should().Be("name-desc");
        catalogue.ToSlug(CategorySortOption.NewestFirst).Should().Be("newest");
        catalogue.ToSlug(CategorySortOption.OldestFirst).Should().Be("oldest");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Default_IsNameAToZ
// AC-32: Slugs_MatchTheSharedVocabulary, Options_CoverEveryMemberOfTheEnum (base class — Newest/Oldest reachable)

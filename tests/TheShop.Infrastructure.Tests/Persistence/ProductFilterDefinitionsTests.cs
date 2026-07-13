using FluentAssertions;
using TheShop.Application.Features.Products;
using TheShop.Infrastructure.Persistence;
using TheShop.Infrastructure.Persistence.Records;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="ProductFilterDefinitions"/> — the filter-definition registry that
/// backs the catalogue's dynamic, backend-driven filter sidebar (FR-6, AC-6; plan §5 decisions
/// 4-5: "adding a new filter dimension is one new entry here").
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class ProductFilterDefinitionsTests
{
    // =========================================================================
    // Registry membership — one entry per selectable filter key
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void All_ContainsExactlyOneEntryPerSelectableFilterKey()
    {
        var registeredKeys = ProductFilterDefinitions.All.Select(d => d.Key);

        registeredKeys.Should().BeEquivalentTo(ProductFilterKeys.SelectableKeys);
    }

    // =========================================================================
    // FindByKey
    // =========================================================================

    [Theory]
    [InlineData(ProductFilterKeys.Category)]
    [InlineData(ProductFilterKeys.Brand)]
    [InlineData(ProductFilterKeys.Flavour)]
    [InlineData(ProductFilterKeys.Nicotine)]
    [Trait("Feature", "product-catalogue")]
    public void FindByKey_WithKnownKey_ReturnsMatchingDefinition(string key)
    {
        var definition = ProductFilterDefinitions.FindByKey(key);

        definition.Should().NotBeNull();
        definition!.Key.Should().Be(key);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void FindByKey_WithUnknownKey_ReturnsNull()
    {
        var definition = ProductFilterDefinitions.FindByKey("not-a-real-filter");

        definition.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void FindByKey_IsCaseInsensitive()
    {
        var definition = ProductFilterDefinitions.FindByKey(ProductFilterKeys.Brand.ToUpperInvariant());

        definition.Should().NotBeNull();
    }

    // =========================================================================
    // ProjectOptions — shapes each group's options from the RPC facets payload
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ProjectOptions_ForCategory_MapsLookupRowsToFilterOptions()
    {
        var categoryId = Guid.NewGuid();
        var facets = new CatalogueFiltersRecord
        {
            Categories = [new FilterLookupRecord { Id = categoryId, Name = "Disposables" }],
        };

        var definition = ProductFilterDefinitions.FindByKey(ProductFilterKeys.Category)!;
        var options = definition.ProjectOptions(facets);

        options.Should().ContainSingle(o => o.Value == categoryId.ToString() && o.Label == "Disposables");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ProjectOptions_ForFlavour_MapsDistinctFlavourStringsToFilterOptions()
    {
        var facets = new CatalogueFiltersRecord { Flavours = ["Blue Razz Ice", "Mango"] };

        var definition = ProductFilterDefinitions.FindByKey(ProductFilterKeys.Flavour)!;
        var options = definition.ProjectOptions(facets);

        options.Should().Contain(o => o.Value == "Blue Razz Ice" && o.Label == "Blue Razz Ice");
        options.Should().Contain(o => o.Value == "Mango" && o.Label == "Mango");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ProjectOptions_ForNicotine_MapsEachDistinctStrengthToAnOption()
    {
        var facets = new CatalogueFiltersRecord { NicotineStrengths = [50, 30] };

        var definition = ProductFilterDefinitions.FindByKey(ProductFilterKeys.Nicotine)!;
        var options = definition.ProjectOptions(facets);

        // The exact label format (e.g. "50 mg") is a presentation detail not pinned down by the
        // spec or plan; this only asserts the echoed Value round-trips the strength and the
        // option carries a non-empty display label.
        options.Should().Contain(o => o.Value == "50" && !string.IsNullOrWhiteSpace(o.Label));
        options.Should().Contain(o => o.Value == "30" && !string.IsNullOrWhiteSpace(o.Label));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ProjectOptions_WhenFacetsAreEmpty_ReturnsEmptyOptions()
    {
        // Edge case: catalogue has no products at all → no filter options.
        var facets = new CatalogueFiltersRecord();

        foreach (var definition in ProductFilterDefinitions.All)
        {
            definition.ProjectOptions(facets).Should().BeEmpty();
        }
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: All_ContainsExactlyOneEntryPerSelectableFilterKey, FindByKey_WithKnownKey_ReturnsMatchingDefinition,
//        ProjectOptions_ForCategory_MapsLookupRowsToFilterOptions, ProjectOptions_ForFlavour_*,
//        ProjectOptions_ForNicotine_*

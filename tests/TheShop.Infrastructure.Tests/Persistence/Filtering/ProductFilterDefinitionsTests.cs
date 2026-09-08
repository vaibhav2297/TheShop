using FluentAssertions;
using Newtonsoft.Json;
using TheShop.Application.Features.Products;
using TheShop.Infrastructure.Persistence.Filtering;
using TheShop.Infrastructure.Persistence.Records;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence.Filtering;

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
    public void ProjectOptions_WhenFacetsAreEmpty_ReturnsEmptyOptions()
    {
        // Edge case: catalogue has no products at all → no filter options.
        var facets = new CatalogueFiltersRecord();

        foreach (var definition in ProductFilterDefinitions.All)
        {
            definition.ProjectOptions(facets).Should().BeEmpty();
        }
    }

    // =========================================================================
    // create-product — get_catalogue_filters() supplies the generic option_types payload that
    // replaces the retired Flavour/Nicotine facets (Decision 3, AC-18). Building the catalogue's
    // dynamic filter controls from it belongs to the product-catalogue feature; this feature's
    // job is only to supply the data, which this test proves the record actually carries.
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void CatalogueFiltersRecord_DeserializesOptionTypesFromTheRpcPayload()
    {
        var json = """
            {
                "categories": [],
                "brands": [],
                "option_types": [
                    { "name": "Flavour", "values": ["Mango", "Mint"] }
                ],
                "price_min": 6.99,
                "price_max": 54.99
            }
            """;

        var record = JsonConvert.DeserializeObject<CatalogueFiltersRecord>(json)!;

        record.OptionTypes.Should().ContainSingle();
        record.OptionTypes[0].Name.Should().Be("Flavour");
        record.OptionTypes[0].Values.Should().BeEquivalentTo(["Mango", "Mint"]);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void CatalogueFiltersRecord_WithNoOptionTypesInThePayload_DefaultsToAnEmptyList()
    {
        var json = """{ "categories": [], "brands": [], "price_min": 0, "price_max": 0 }""";

        var record = JsonConvert.DeserializeObject<CatalogueFiltersRecord>(json)!;

        record.OptionTypes.Should().BeEmpty();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: All_ContainsExactlyOneEntryPerSelectableFilterKey, FindByKey_WithKnownKey_ReturnsMatchingDefinition,
//        ProjectOptions_ForCategory_MapsLookupRowsToFilterOptions

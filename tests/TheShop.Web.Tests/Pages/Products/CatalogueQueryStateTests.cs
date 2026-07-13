using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Enums;
using TheShop.Web.Pages.Products;
using Xunit;

namespace TheShop.Web.Tests.Pages.Products;

/// <summary>
/// Tests for <see cref="CatalogueQueryState"/> — the deep-link (de)serialization of the catalogue's
/// filter / price / sort / page state to and from the URL query string. Covers clean-URL defaults,
/// full round-trips, and the robust parsing a hand-edited URL demands.
/// </summary>
public class CatalogueQueryStateTests
{
    // Rebuilds the parsed-query shape (repeated params → StringValues) that
    // GetUriWithQueryParameters + QueryHelpers.ParseQuery would produce for a state, so the two
    // halves are exercised together without a NavigationManager.
    private static Dictionary<string, StringValues> Serialize(CatalogueQueryState state)
    {
        var query = new Dictionary<string, StringValues>(StringComparer.Ordinal);
        foreach (var (key, value) in state.ToQueryParameters())
        {
            query[key] = value switch
            {
                string[] array => new StringValues(array),
                null => StringValues.Empty,
                _ => new StringValues(Convert.ToString(value, CultureInfo.InvariantCulture)),
            };
        }

        return query;
    }

    private static Dictionary<string, StringValues> Query(params (string Key, string[] Values)[] entries)
    {
        var query = new Dictionary<string, StringValues>(StringComparer.Ordinal);
        foreach (var (key, values) in entries)
            query[key] = new StringValues(values);

        return query;
    }

    // =========================================================================
    // Clean-URL defaults — no param for a value that equals the default
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Default_ToQueryParameters_IsEmpty()
    {
        CatalogueQueryState.Default.ToQueryParameters().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToQueryParameters_NewestFirstAndPageOne_OmitsSortAndPage()
    {
        var state = new CatalogueQueryState([], null, null, ProductSortOption.NewestFirst, 1);

        state.ToQueryParameters().Should().NotContainKeys("sort", "page");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToQueryParameters_EmptyFilterValues_OmitsTheKey()
    {
        var state = new CatalogueQueryState(
            [new AppliedFilterDto(ProductFilterKeys.Category, [])], null, null, ProductSortOption.NewestFirst, 1);

        state.ToQueryParameters().Should().NotContainKey(ProductFilterKeys.Category);
    }

    // =========================================================================
    // Round-trips
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void RoundTrip_FullState_ReproducesEveryField()
    {
        var state = new CatalogueQueryState(
            [
                new AppliedFilterDto(ProductFilterKeys.Category, ["vanilla", "menthol"]),
                new AppliedFilterDto(ProductFilterKeys.Brand, ["acme"]),
            ],
            PriceMin: 10.50m,
            PriceMax: 99.99m,
            Sort: ProductSortOption.PriceLowToHigh,
            Page: 3);

        var restored = CatalogueQueryState.FromQuery(Serialize(state));

        restored.PriceMin.Should().Be(10.50m);
        restored.PriceMax.Should().Be(99.99m);
        restored.Sort.Should().Be(ProductSortOption.PriceLowToHigh);
        restored.Page.Should().Be(3);
        restored.Filters.Should().BeEquivalentTo(state.Filters);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void RoundTrip_Default_ReproducesDefault()
    {
        var restored = CatalogueQueryState.FromQuery(Serialize(CatalogueQueryState.Default));

        restored.Filters.Should().BeEmpty();
        restored.PriceMin.Should().BeNull();
        restored.PriceMax.Should().BeNull();
        restored.Sort.Should().Be(ProductSortOption.NewestFirst);
        restored.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void FromQuery_RepeatedFilterValues_ParseIntoOneGroup()
    {
        var state = CatalogueQueryState.FromQuery(
            Query((ProductFilterKeys.Flavour, ["vanilla", "menthol", "berry"])));

        var flavour = state.Filters.Should().ContainSingle().Subject;
        flavour.Key.Should().Be(ProductFilterKeys.Flavour);
        flavour.Values.Should().BeEquivalentTo(["vanilla", "menthol", "berry"]);
    }

    // =========================================================================
    // ToggleFilter — atomic, compounding merge (rapid-selection race fix)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToggleFilter_AddsAnOption_ToAnEmptySelection()
    {
        var state = CatalogueQueryState.Default.ToggleFilter(ProductFilterKeys.Category, "cat-1", isSelected: true);

        state.Filters.Should().ContainSingle(f => f.Key == ProductFilterKeys.Category)
            .Which.Values.Should().ContainSingle().Which.Should().Be("cat-1");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToggleFilter_AppliedInSequenceAcrossGroups_CompoundsInsteadOfOverwriting()
    {
        // The rapid-selection regression: two toggles applied back-to-back must accumulate. Each
        // builds on the state the previous one returned, so neither group is dropped.
        var state = CatalogueQueryState.Default
            .ToggleFilter(ProductFilterKeys.Category, "cat-1", isSelected: true)
            .ToggleFilter(ProductFilterKeys.Brand, "acme", isSelected: true);

        state.Filters.Should().Contain(f => f.Key == ProductFilterKeys.Category && f.Values.Contains("cat-1"));
        state.Filters.Should().Contain(f => f.Key == ProductFilterKeys.Brand && f.Values.Contains("acme"));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToggleFilter_AppliedInSequenceWithinAGroup_AccumulatesValues()
    {
        var state = CatalogueQueryState.Default
            .ToggleFilter(ProductFilterKeys.Flavour, "vanilla", isSelected: true)
            .ToggleFilter(ProductFilterKeys.Flavour, "menthol", isSelected: true);

        state.Filters.Should().ContainSingle(f => f.Key == ProductFilterKeys.Flavour)
            .Which.Values.Should().BeEquivalentTo(["vanilla", "menthol"]);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToggleFilter_Deselecting_RemovesTheValueAndDropsAnEmptiedGroup()
    {
        var state = CatalogueQueryState.Default
            .ToggleFilter(ProductFilterKeys.Category, "cat-1", isSelected: true)
            .ToggleFilter(ProductFilterKeys.Category, "cat-1", isSelected: false);

        state.Filters.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToggleFilter_ReTogglingSameValue_DoesNotDuplicateIt()
    {
        var state = CatalogueQueryState.Default
            .ToggleFilter(ProductFilterKeys.Category, "cat-1", isSelected: true)
            .ToggleFilter(ProductFilterKeys.Category, "cat-1", isSelected: true);

        state.Filters.Should().ContainSingle(f => f.Key == ProductFilterKeys.Category)
            .Which.Values.Should().ContainSingle();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToggleFilter_ResetsToPageOneButPreservesPriceAndSort()
    {
        var state = new CatalogueQueryState([], 10m, 90m, ProductSortOption.PriceLowToHigh, Page: 5)
            .ToggleFilter(ProductFilterKeys.Category, "cat-1", isSelected: true);

        state.Page.Should().Be(1);
        state.PriceMin.Should().Be(10m);
        state.PriceMax.Should().Be(90m);
        state.Sort.Should().Be(ProductSortOption.PriceLowToHigh);
    }

    // =========================================================================
    // Robust parsing of hand-edited / malformed URLs
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void FromQuery_UnknownFilterKey_IsIgnored()
    {
        var state = CatalogueQueryState.FromQuery(Query(("colour", ["blue"])));

        state.Filters.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void FromQuery_UnknownSortSlug_FallsBackToNewestFirst()
    {
        var state = CatalogueQueryState.FromQuery(Query(("sort", ["bogus"])));

        state.Sort.Should().Be(ProductSortOption.NewestFirst);
    }

    [Theory]
    [Trait("Feature", "product-catalogue")]
    [InlineData("0")]
    [InlineData("-4")]
    [InlineData("abc")]
    public void FromQuery_NonPositiveOrMalformedPage_ClampsToOne(string page)
    {
        var state = CatalogueQueryState.FromQuery(Query(("page", [page])));

        state.Page.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void FromQuery_MalformedPrice_YieldsNull()
    {
        var state = CatalogueQueryState.FromQuery(Query(("price_min", ["not-a-number"])));

        state.PriceMin.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void FromQuery_Price_ParsedWithInvariantCulture()
    {
        // The dot is always the decimal separator in the URL, regardless of the running culture.
        var state = CatalogueQueryState.FromQuery(Query(("price_max", ["1234.56"])));

        state.PriceMax.Should().Be(1234.56m);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void FromQuery_EmptyFilterValue_IsDropped()
    {
        var state = CatalogueQueryState.FromQuery(Query((ProductFilterKeys.Brand, ["", "   "])));

        state.Filters.Should().BeEmpty();
    }
}

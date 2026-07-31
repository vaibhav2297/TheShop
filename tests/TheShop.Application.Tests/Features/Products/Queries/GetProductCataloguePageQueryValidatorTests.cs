using FluentAssertions;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Queries.GetProductCataloguePage;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Tests for <see cref="GetProductCataloguePageQueryValidator"/> — rejects malformed
/// pagination, price range, sort, and filter-key input before it reaches
/// <see cref="IProductRepository"/> (plan §9 validators; spec Edge Cases &amp; Error Handling).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class GetProductCataloguePageQueryValidatorTests
{
    private readonly GetProductCataloguePageQueryValidator _validator = new();

    private static GetProductCataloguePageQuery ValidQuery(
        IReadOnlyList<AppliedFilterDto>? selectedFilters = null,
        decimal? priceMin = null,
        decimal? priceMax = null,
        ProductSortOption sort = ProductSortOption.NewestFirst,
        int page = 1,
        int pageSize = 12) =>
        new(selectedFilters ?? [], priceMin, priceMax, sort, new PaginationRequest(page, pageSize));

    // =========================================================================
    // Happy path
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WithAllValidValues_IsValid()
    {
        var query = ValidQuery(
            selectedFilters: [new AppliedFilterDto(ProductFilterKeys.Brand, ["elf-bar-id"])],
            priceMin: 10m, priceMax: 50m);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    // =========================================================================
    // Page (Catalogue_Page_Invalid)
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenPageIsBelowOne_HasPageInvalidError(int page)
    {
        var result = _validator.Validate(ValidQuery(page: page));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.CataloguePageInvalid);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenPageIsOne_HasNoPageError()
    {
        // Boundary: page 1 is the smallest valid page.
        var result = _validator.Validate(ValidQuery(page: 1));

        result.Errors.Should().NotContain(e => e.ErrorMessage == ProductErrorKeys.CataloguePageInvalid);
    }

    // =========================================================================
    // PageSize (Catalogue_PageSize_Invalid) — AC-8: 12 products per page
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(49)]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenPageSizeIsOutOfRange_HasPageSizeInvalidError(int pageSize)
    {
        var result = _validator.Validate(ValidQuery(pageSize: pageSize));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.CataloguePageSizeInvalid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(48)]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenPageSizeIsWithinRange_HasNoPageSizeError(int pageSize)
    {
        var result = _validator.Validate(ValidQuery(pageSize: pageSize));

        result.Errors.Should().NotContain(e => e.ErrorMessage == ProductErrorKeys.CataloguePageSizeInvalid);
    }

    // =========================================================================
    // Price range (Catalogue_PriceRange_Invalid)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenPriceMinIsGreaterThanPriceMax_HasPriceRangeInvalidError()
    {
        var result = _validator.Validate(ValidQuery(priceMin: 50m, priceMax: 10m));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.CataloguePriceRangeInvalid);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenPriceMinEqualsPriceMax_HasNoPriceRangeError()
    {
        // Boundary: min == max is a valid (single-point) range.
        var result = _validator.Validate(ValidQuery(priceMin: 25m, priceMax: 25m));

        result.Errors.Should().NotContain(e => e.ErrorMessage == ProductErrorKeys.CataloguePriceRangeInvalid);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenOnlyOneBoundIsSupplied_HasNoPriceRangeError()
    {
        var result = _validator.Validate(ValidQuery(priceMin: 25m, priceMax: null));

        result.Errors.Should().NotContain(e => e.ErrorMessage == ProductErrorKeys.CataloguePriceRangeInvalid);
    }

    // =========================================================================
    // Sort (Catalogue_Sort_Invalid) — AC-7
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenSortIsUndefined_HasSortInvalidError()
    {
        var result = _validator.Validate(ValidQuery(sort: (ProductSortOption)999));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.CatalogueSortInvalid);
    }

    // =========================================================================
    // Selected filter keys (Catalogue_Filter_Invalid) — AC-6
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenSelectedFilterKeyIsUnknown_HasFilterInvalidError()
    {
        var result = _validator.Validate(
            ValidQuery(selectedFilters: [new AppliedFilterDto("not-a-real-filter", ["x"])]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.CatalogueFilterInvalid);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenSelectedFilterKeyIsPrice_HasFilterInvalidError()
    {
        // Price is a Range group; it must not be sent as a multi-select AppliedFilterDto — it
        // travels via the query's own PriceMin/PriceMax fields instead.
        var result = _validator.Validate(
            ValidQuery(selectedFilters: [new AppliedFilterDto(ProductFilterKeys.Price, ["10"])]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.CatalogueFilterInvalid);
    }

    [Theory]
    [InlineData(ProductFilterKeys.Category)]
    [InlineData(ProductFilterKeys.Brand)]
    [InlineData(ProductFilterKeys.Flavour)]
    [InlineData(ProductFilterKeys.Nicotine)]
    [Trait("Feature", "product-catalogue")]
    public void Validate_WhenSelectedFilterKeyIsSelectable_HasNoFilterError(string key)
    {
        var result = _validator.Validate(
            ValidQuery(selectedFilters: [new AppliedFilterDto(key, ["some-value"])]));

        result.Errors.Should().NotContain(e => e.ErrorMessage == ProductErrorKeys.CatalogueFilterInvalid);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: Validate_WhenSelectedFilterKeyIsUnknown_HasFilterInvalidError,
//        Validate_WhenSelectedFilterKeyIsPrice_HasFilterInvalidError,
//        Validate_WhenSelectedFilterKeyIsSelectable_HasNoFilterError
// AC-7: Validate_WhenSortIsUndefined_HasSortInvalidError
// AC-8: Validate_WhenPageIsBelowOne_HasPageInvalidError, Validate_WhenPageSizeIsOutOfRange_HasPageSizeInvalidError,
//        Validate_WhenPageSizeIsWithinRange_HasNoPageSizeError

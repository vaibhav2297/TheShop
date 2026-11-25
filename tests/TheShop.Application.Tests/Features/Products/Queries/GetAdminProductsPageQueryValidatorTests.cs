using FluentAssertions;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Tests for <see cref="GetAdminProductsPageQueryValidator"/>: the requested page must be at
/// least 1 (AC-1), and a supplied price range must be non-negative with
/// <c>PriceMin &lt;= PriceMax</c> (RULE-10).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class GetAdminProductsPageQueryValidatorTests
{
    private readonly GetAdminProductsPageQueryValidator _validator = new();

    private static GetAdminProductsPageQuery Query(
        int page = 1, decimal? priceMin = null, decimal? priceMax = null) =>
        new(null, null, null, null, priceMin, priceMax, AdminProductSortOption.NameAToZ, new PaginationRequest(page, 10));

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WithPageOneAndNoPriceRange_IsValid()
    {
        var result = _validator.Validate(Query());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Feature", "manage-product")]
    public void Validate_WithPageBelowOne_HasPageInvalidError(int page)
    {
        var result = _validator.Validate(Query(page: page));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.PageInvalid);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WithAValidPriceRange_IsValid()
    {
        var result = _validator.Validate(Query(priceMin: 10m, priceMax: 25m));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WithOnlyOneBoundSupplied_IsValid()
    {
        var result = _validator.Validate(Query(priceMin: 10m));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WhenPriceMinExceedsPriceMax_HasPriceRangeInvalidError()
    {
        var result = _validator.Validate(Query(priceMin: 25m, priceMax: 10m));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.PriceRangeInvalid);
    }

    [Theory]
    [InlineData(-1)]
    [Trait("Feature", "manage-product")]
    public void Validate_WithANegativePriceMin_HasPriceRangeInvalidError(decimal min)
    {
        var result = _validator.Validate(Query(priceMin: min, priceMax: 10m));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.PriceRangeInvalid);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WithANegativePriceMax_HasPriceRangeInvalidError()
    {
        var result = _validator.Validate(Query(priceMax: -1m));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.PriceRangeInvalid);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WithPriceMinEqualToPriceMax_IsValid()
    {
        // RULE-10 endpoints are inclusive — AC-26's CAD 10-10 / CAD 30-30 bounds must validate.
        var result = _validator.Validate(Query(priceMin: 10m, priceMax: 10m));

        result.IsValid.Should().BeTrue();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1 (page navigation): Validate_WithPageOneAndNoPriceRange_IsValid, Validate_WithPageBelowOne_HasPageInvalidError
// RULE-10 (valid, non-negative price range): Validate_WithAValidPriceRange_IsValid,
//        Validate_WithOnlyOneBoundSupplied_IsValid, Validate_WhenPriceMinExceedsPriceMax_HasPriceRangeInvalidError,
//        Validate_WithANegativePriceMin_HasPriceRangeInvalidError, Validate_WithANegativePriceMax_HasPriceRangeInvalidError,
//        Validate_WithPriceMinEqualToPriceMax_IsValid

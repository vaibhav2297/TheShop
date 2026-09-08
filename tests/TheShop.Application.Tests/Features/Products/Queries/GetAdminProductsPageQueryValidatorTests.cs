using FluentAssertions;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Tests for <see cref="GetAdminProductsPageQueryValidator"/>: the requested page must be at
/// least 1 (AC-2); the fixed page size is clamped by the handler rather than validated here.
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class GetAdminProductsPageQueryValidatorTests
{
    private readonly GetAdminProductsPageQueryValidator _validator = new();

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithPageOne_IsValid()
    {
        var result = _validator.Validate(new GetAdminProductsPageQuery(new PaginationRequest(1, 10)));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Feature", "create-product")]
    public void Validate_WithPageBelowOne_HasPageInvalidError(int page)
    {
        var result = _validator.Validate(new GetAdminProductsPageQuery(new PaginationRequest(page, 10)));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.PageInvalid);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-2 (page navigation): Validate_WithPageOne_IsValid, Validate_WithPageBelowOne_HasPageInvalidError

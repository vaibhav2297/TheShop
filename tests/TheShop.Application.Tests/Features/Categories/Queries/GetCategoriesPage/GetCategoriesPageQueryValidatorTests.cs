using FluentAssertions;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.Queries.GetCategoriesPage;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Queries.GetCategoriesPage;

/// <summary>
/// Tests for <see cref="GetCategoriesPageQueryValidator"/> — the requested page number must be at
/// least 1; page size is not validated here because the handler clamps it to the fixed page size
/// (spec constraint: "the page size is fixed and not chosen by the staff member").
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class GetCategoriesPageQueryValidatorTests
{
    private readonly GetCategoriesPageQueryValidator _validator = new();

    private static GetCategoriesPageQuery Query(int page = 1) =>
        new(null, null, CategorySortOption.NameAToZ, new PaginationRequest(page, 10));

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WithPageOne_IsValid()
    {
        var result = _validator.Validate(Query(page: 1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenPageIsZero_HasPageInvalidError()
    {
        var result = _validator.Validate(Query(page: 0));

        result.Errors.Should().Contain(e => e.ErrorMessage == CategoryErrorKeys.PageInvalid);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenPageIsNegative_HasPageInvalidError()
    {
        var result = _validator.Validate(Query(page: -1));

        result.Errors.Should().Contain(e => e.ErrorMessage == CategoryErrorKeys.PageInvalid);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Validate_WithPageOne_IsValid (page-1 default that the initial list load relies on)

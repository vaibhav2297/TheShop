using FluentAssertions;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.Commands.DeleteCategories;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.DeleteCategories;

/// <summary>
/// Tests for <see cref="DeleteCategoriesCommandValidator"/> — at least one id, no empty guids, no
/// duplicates (structural guard behind the row-selection UI).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class DeleteCategoriesCommandValidatorTests
{
    private readonly DeleteCategoriesCommandValidator _validator = new();

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid()
    {
        var cmd = new DeleteCategoriesCommand([Guid.NewGuid(), Guid.NewGuid()]);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenIdsIsEmpty_HasCategoryIdsRequiredError()
    {
        var cmd = new DeleteCategoriesCommand([]);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == CategoryErrorKeys.CategoryIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenAnIdIsEmpty_HasCategoryIdsRequiredError()
    {
        var cmd = new DeleteCategoriesCommand([Guid.NewGuid(), Guid.Empty]);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == CategoryErrorKeys.CategoryIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenIdsContainADuplicate_HasCategoryIdsRequiredError()
    {
        var id = Guid.NewGuid();
        var cmd = new DeleteCategoriesCommand([id, id]);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == CategoryErrorKeys.CategoryIdsRequired);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-17: Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid (single-row delete is a one-element list)
// AC-29..AC-30 (bulk delete): Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid

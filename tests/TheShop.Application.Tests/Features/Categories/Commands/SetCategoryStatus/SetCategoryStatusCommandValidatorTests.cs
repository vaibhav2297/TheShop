using FluentAssertions;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.Commands.SetCategoryStatus;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.SetCategoryStatus;

/// <summary>
/// Tests for <see cref="SetCategoryStatusCommandValidator"/> — at least one id, no empty guids, no
/// duplicates (structural guard behind the row-selection UI).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class SetCategoryStatusCommandValidatorTests
{
    private readonly SetCategoryStatusCommandValidator _validator = new();

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid()
    {
        var cmd = new SetCategoryStatusCommand([Guid.NewGuid(), Guid.NewGuid()], true);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenIdsIsEmpty_HasCategoryIdsRequiredError()
    {
        var cmd = new SetCategoryStatusCommand([], true);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == CategoryErrorKeys.CategoryIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenAnIdIsEmpty_HasCategoryIdsRequiredError()
    {
        var cmd = new SetCategoryStatusCommand([Guid.NewGuid(), Guid.Empty], true);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == CategoryErrorKeys.CategoryIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenIdsContainADuplicate_HasCategoryIdsRequiredError()
    {
        var id = Guid.NewGuid();
        var cmd = new SetCategoryStatusCommand([id, id], true);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == CategoryErrorKeys.CategoryIdsRequired);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-16: Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid (single-row toggle is a one-element list)
// AC-28: Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid (bulk activate/deactivate)

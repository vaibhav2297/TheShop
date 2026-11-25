using FluentAssertions;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.SetProductStatus;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.SetProductStatus;

/// <summary>
/// Tests for <see cref="SetProductStatusCommandValidator"/> — at least one id, no empty guids, no
/// duplicates (structural guard behind the row-selection UI).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class SetProductStatusCommandValidatorTests
{
    private readonly SetProductStatusCommandValidator _validator = new();

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid()
    {
        var cmd = new SetProductStatusCommand([Guid.NewGuid(), Guid.NewGuid()], true);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WhenIdsIsEmpty_HasProductIdsRequiredError()
    {
        var cmd = new SetProductStatusCommand([], true);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.ProductIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WhenAnIdIsEmpty_HasProductIdsRequiredError()
    {
        var cmd = new SetProductStatusCommand([Guid.NewGuid(), Guid.Empty], true);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.ProductIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WhenIdsContainADuplicate_HasProductIdsRequiredError()
    {
        var id = Guid.NewGuid();
        var cmd = new SetProductStatusCommand([id, id], true);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.ProductIdsRequired);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-10 (single-row toggle is a one-element list): Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid
// AC-10 (bulk activate/deactivate): Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid

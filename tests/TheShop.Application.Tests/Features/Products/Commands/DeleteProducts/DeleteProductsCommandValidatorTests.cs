using FluentAssertions;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.DeleteProducts;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.DeleteProducts;

/// <summary>
/// Tests for <see cref="DeleteProductsCommandValidator"/> — at least one id, no empty guids, no
/// duplicates (structural guard behind the row-selection UI).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class DeleteProductsCommandValidatorTests
{
    private readonly DeleteProductsCommandValidator _validator = new();

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid()
    {
        var cmd = new DeleteProductsCommand([Guid.NewGuid(), Guid.NewGuid()]);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WhenIdsIsEmpty_HasProductIdsRequiredError()
    {
        var cmd = new DeleteProductsCommand([]);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.ProductIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WhenAnIdIsEmpty_HasProductIdsRequiredError()
    {
        var cmd = new DeleteProductsCommand([Guid.NewGuid(), Guid.Empty]);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.ProductIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void Validate_WhenIdsContainADuplicate_HasProductIdsRequiredError()
    {
        var id = Guid.NewGuid();
        var cmd = new DeleteProductsCommand([id, id]);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.ProductIdsRequired);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-13 (single-row delete is a one-element list): Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid
// AC-16, AC-17 (bulk delete): Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid

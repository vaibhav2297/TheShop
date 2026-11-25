using FluentAssertions;
using TheShop.Application.Features.Brands;
using TheShop.Application.Features.Brands.Commands.DeleteBrands;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.DeleteBrands;

/// <summary>
/// Tests for <see cref="DeleteBrandsCommandValidator"/> — at least one id, no empty guids, no
/// duplicates (structural guard behind the row-selection UI, RULE-12).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class DeleteBrandsCommandValidatorTests
{
    private readonly DeleteBrandsCommandValidator _validator = new();

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid()
    {
        var cmd = new DeleteBrandsCommand([Guid.NewGuid(), Guid.NewGuid()]);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenIdsIsEmpty_HasBrandIdsRequiredError()
    {
        var cmd = new DeleteBrandsCommand([]);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == BrandErrorKeys.BrandIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenAnIdIsEmpty_HasBrandIdsRequiredError()
    {
        var cmd = new DeleteBrandsCommand([Guid.NewGuid(), Guid.Empty]);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == BrandErrorKeys.BrandIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenIdsContainADuplicate_HasBrandIdsRequiredError()
    {
        var id = Guid.NewGuid();
        var cmd = new DeleteBrandsCommand([id, id]);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == BrandErrorKeys.BrandIdsRequired);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-13: Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid (single-row delete is a one-element list)
// AC-21..AC-24 (bulk delete): Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid

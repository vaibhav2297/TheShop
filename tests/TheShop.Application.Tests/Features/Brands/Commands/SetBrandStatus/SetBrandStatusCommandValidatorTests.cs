using FluentAssertions;
using TheShop.Application.Features.Brands;
using TheShop.Application.Features.Brands.Commands.SetBrandStatus;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.SetBrandStatus;

/// <summary>
/// Tests for <see cref="SetBrandStatusCommandValidator"/> — at least one id, no empty guids, no
/// duplicates (structural guard behind the row-selection UI, RULE-12).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class SetBrandStatusCommandValidatorTests
{
    private readonly SetBrandStatusCommandValidator _validator = new();

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid()
    {
        var cmd = new SetBrandStatusCommand([Guid.NewGuid(), Guid.NewGuid()], true);

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenIdsIsEmpty_HasBrandIdsRequiredError()
    {
        var cmd = new SetBrandStatusCommand([], true);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == BrandErrorKeys.BrandIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenAnIdIsEmpty_HasBrandIdsRequiredError()
    {
        var cmd = new SetBrandStatusCommand([Guid.NewGuid(), Guid.Empty], true);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == BrandErrorKeys.BrandIdsRequired);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenIdsContainADuplicate_HasBrandIdsRequiredError()
    {
        var id = Guid.NewGuid();
        var cmd = new SetBrandStatusCommand([id, id], true);

        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.ErrorMessage == BrandErrorKeys.BrandIdsRequired);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-21: Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid (single-row toggle is a one-element list)
// AC-23: Validate_WithOneOrMoreDistinctNonEmptyIds_IsValid (bulk activate/deactivate)

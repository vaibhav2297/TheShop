using FluentAssertions;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.UpdateProduct;
using TheShop.Application.Features.Products.DTOs;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.UpdateProduct;

/// <summary>
/// Tests for <see cref="UpdateProductCommandValidator"/> — the same field-shape rules as
/// <c>CreateProductCommandValidator</c> (plan Section 9), plus <see cref="UpdateProductCommand.Id"/>
/// and <see cref="UpdateProductCommand.RowVersion"/> presence (Decision 11).
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class UpdateProductCommandValidatorTests
{
    private readonly UpdateProductCommandValidator _validator = new();

    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid BrandId = Guid.NewGuid();

    private static UpdateProductCommand ValidCommand(
        Guid? id = null,
        string rowVersion = "row-version-1",
        string name = "Elf Bar BC5000",
        string? description = "A long-lasting disposable vape.",
        string sku = "ELF-BC5000",
        decimal? originalPrice = 24.99m,
        decimal? salePrice = null,
        IReadOnlyList<OptionTypeInput>? optionTypes = null,
        IReadOnlyList<SpecificationInput>? specifications = null,
        IReadOnlyList<VariantInput>? variants = null) =>
        new(id ?? Guid.NewGuid(), rowVersion, name, description, sku, CategoryId, BrandId,
            originalPrice, salePrice, false, [], optionTypes ?? [], specifications ?? [], variants ?? []);

    // =========================================================================
    // Happy path
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithAllValidValues_IsValid()
    {
        var result = _validator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    // =========================================================================
    // Id / RowVersion (Decision 11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithEmptyId_HasNotFoundError()
    {
        var result = _validator.Validate(ValidCommand(id: Guid.Empty));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.NotFound);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithBlankRowVersion_HasModifiedElsewhereError()
    {
        var result = _validator.Validate(ValidCommand(rowVersion: ""));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.ModifiedElsewhere);
    }

    // =========================================================================
    // Name / description (RULE-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithBlankName_HasNameRequiredError()
    {
        var result = _validator.Validate(ValidCommand(name: ""));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.NameRequired);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithNameOver150Characters_HasNameTooLongError()
    {
        var result = _validator.Validate(ValidCommand(name: new string('a', 151)));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.NameTooLong);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithDescriptionOver20000Characters_HasDescriptionTooLongError()
    {
        var result = _validator.Validate(ValidCommand(description: new string('a', 20_001)));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.DescriptionTooLong);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithDescriptionContainingUnsupportedMarkup_HasDescriptionUnsupportedContentError()
    {
        var result = _validator.Validate(ValidCommand(description: "<script>alert(1)</script>"));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.DescriptionUnsupportedContent);
    }

    // =========================================================================
    // Specifications (product-description RULE-3, RULE-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithEveryRowNamedAndValued_IsValid()
    {
        var result = _validator.Validate(ValidCommand(specifications: [
            new SpecificationInput(null, "Material", "Stainless steel", 0),
        ]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithABlankSpecificationValue_HasSpecificationValueRequiredError()
    {
        var result = _validator.Validate(ValidCommand(specifications: [
            new SpecificationInput(null, "Material", "   ", 0),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SpecificationValueRequired);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithDuplicateSpecificationNames_HasSpecificationNameDuplicatedError()
    {
        var result = _validator.Validate(ValidCommand(specifications: [
            new SpecificationInput(null, "Material", "Stainless steel", 0),
            new SpecificationInput(null, " MATERIAL ", "Aluminum", 1),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SpecificationNameDuplicated);
    }

    // =========================================================================
    // SKU / category / brand (RULE-8, RULE-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithBlankSku_HasSkuRequiredError()
    {
        var result = _validator.Validate(ValidCommand(sku: ""));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SkuRequired);
    }

    // =========================================================================
    // Price / sale price (RULE-4, RULE-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithNoOptionTypesAndNoPrice_HasPriceRequiredError()
    {
        var result = _validator.Validate(ValidCommand(originalPrice: null));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.PriceRequired);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithSalePriceAboveOriginalPrice_HasSalePriceTooHighError()
    {
        var result = _validator.Validate(ValidCommand(originalPrice: 19.99m, salePrice: 24.99m));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SalePriceTooHigh);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Feature", "create-product")]
    public void Validate_WithZeroOrNegativePrice_HasPriceInvalidError(decimal price)
    {
        var result = _validator.Validate(ValidCommand(originalPrice: price));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.PriceInvalid);
    }

    // =========================================================================
    // Option types (RULE-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithDuplicateOptionTypeNames_HasOptionNameDuplicatedError()
    {
        var result = _validator.Validate(ValidCommand(optionTypes: [
            new OptionTypeInput(null, "Flavour", [new OptionValueInput(null, "Mango")]),
            new OptionTypeInput(null, "Flavour", [new OptionValueInput(null, "Mint")]),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.OptionNameDuplicated);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-21 (name blank/too long): Validate_WithBlankName_HasNameRequiredError,
//        Validate_WithNameOver150Characters_HasNameTooLongError
// AC-25 (sale price must be lower than price): Validate_WithSalePriceAboveOriginalPrice_HasSalePriceTooHighError
// AC-27 (SKU required): Validate_WithBlankSku_HasSkuRequiredError
// AC-28 (duplicate option names refused): Validate_WithDuplicateOptionTypeNames_HasOptionNameDuplicatedError
// AC-32 (a failed save must not proceed on stale input): Validate_WithEmptyId_HasNotFoundError, Validate_WithBlankRowVersion_HasModifiedElsewhereError
// RULE-4 (price required only without option types): Validate_WithNoOptionTypesAndNoPrice_HasPriceRequiredError,
//        Validate_WithZeroOrNegativePrice_HasPriceInvalidError
// product-description AC-2/AC-5 (edit rejects an over-limit description, unrelated details unaffected): Validate_WithDescriptionOver20000Characters_HasDescriptionTooLongError
// product-description AC-7 (blank row value identified): Validate_WithABlankSpecificationValue_HasSpecificationValueRequiredError
// product-description AC-8 (duplicate names rejected on edit too): Validate_WithDuplicateSpecificationNames_HasSpecificationNameDuplicatedError
// product-description AC-11 (executable content never saved): Validate_WithDescriptionContainingUnsupportedMarkup_HasDescriptionUnsupportedContentError

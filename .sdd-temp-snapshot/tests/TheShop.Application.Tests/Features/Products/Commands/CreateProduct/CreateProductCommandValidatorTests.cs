using FluentAssertions;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.CreateProduct;
using TheShop.Application.Features.Products.DTOs;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Commands.CreateProduct;

/// <summary>
/// Tests for <see cref="CreateProductCommandValidator"/> — field-shape validation per plan
/// Section 9: name/description length (RULE-1), SKU/category/brand presence (RULE-8, RULE-3),
/// price/sale-price shape on the product and every variant (RULE-4, RULE-5), image type/size and
/// one-primary (RULE-7), variant SKU distinctness (RULE-8), and option type/value
/// presence/uniqueness (RULE-9).
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _validator = new();

    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid BrandId = Guid.NewGuid();

    private static CreateProductCommand ValidCommand(
        string name = "Elf Bar BC5000",
        string? description = "A long-lasting disposable vape.",
        string sku = "ELF-BC5000",
        decimal? originalPrice = 24.99m,
        decimal? salePrice = null,
        bool isPublished = false,
        IReadOnlyList<ProductGalleryEntry>? gallery = null,
        IReadOnlyList<OptionTypeInput>? optionTypes = null,
        IReadOnlyList<SpecificationInput>? specifications = null,
        IReadOnlyList<VariantInput>? variants = null) =>
        new(name, description, sku, CategoryId, BrandId, originalPrice, salePrice, isPublished,
            gallery ?? [], optionTypes ?? [], specifications ?? [], variants ?? []);

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
    // Name (RULE-1)
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "create-product")]
    public void Validate_WithBlankName_HasNameRequiredError(string name)
    {
        var result = _validator.Validate(ValidCommand(name: name));

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
    [Trait("Feature", "create-product")]
    public void Validate_WithNameOfExactly150Characters_IsValid()
    {
        var result = _validator.Validate(ValidCommand(name: new string('a', 150)));

        result.IsValid.Should().BeTrue();
    }

    // =========================================================================
    // Description (product-description RULE-2, superseding create-product RULE-1's 2,000-char bound)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithDescriptionOfExactly20000Characters_IsValid()
    {
        var result = _validator.Validate(ValidCommand(description: new string('a', 20_000)));

        result.IsValid.Should().BeTrue("RULE-2 allows up to exactly 20,000 plain-text characters (AC-4)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithDescriptionOver20000Characters_HasDescriptionTooLongError()
    {
        var result = _validator.Validate(ValidCommand(description: new string('a', 20_001)));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.DescriptionTooLong);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithNoDescription_IsValid()
    {
        var result = _validator.Validate(ValidCommand(description: null));

        result.IsValid.Should().BeTrue("a description is optional (RULE-1)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithDescriptionContainingUnsupportedMarkup_HasDescriptionUnsupportedContentError()
    {
        var result = _validator.Validate(ValidCommand(description: "<script>alert(1)</script>"));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.DescriptionUnsupportedContent);
    }

    [Theory]
    [InlineData("<p>Paragraph</p>")]
    [InlineData("<h1>Heading</h1>")]
    [InlineData("<strong>Bold</strong><em>Italic</em>")]
    [InlineData("<ul><li>Item</li></ul>")]
    [InlineData("<ol><li>Item</li></ol>")]
    [InlineData("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer\">Link</a>")]
    [Trait("Feature", "product-description")]
    public void Validate_WithEveryFR1SupportedFormat_IsValid(string description)
    {
        var result = _validator.Validate(ValidCommand(description: description));

        result.IsValid.Should().BeTrue("every FR-1 format (paragraphs, headings, bold, italic, lists, links) is allowed");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithDescriptionOverMaxHtmlBytes_HasDescriptionTooLargeError()
    {
        var oversized = "<p>" + new string('a', 199_999) + "</p>";

        var result = _validator.Validate(ValidCommand(description: oversized));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.DescriptionTooLarge);
    }

    // =========================================================================
    // SKU / category / brand presence (RULE-8, RULE-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithBlankSku_HasSkuRequiredError()
    {
        var result = _validator.Validate(ValidCommand(sku: ""));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SkuRequired);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithEmptyCategoryId_HasCategoryRequiredError()
    {
        var command = ValidCommand() with { CategoryId = Guid.Empty };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.CategoryRequired);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithEmptyBrandId_HasBrandRequiredError()
    {
        var command = ValidCommand() with { BrandId = Guid.Empty };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.BrandRequired);
    }

    // =========================================================================
    // Price (RULE-4) — required only for a product with no option types
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
    public void Validate_WithOptionTypesAndNoProductPrice_IsValid()
    {
        var optionTypes = OneFlavourOptionType(out var mangoId, out _);
        var result = _validator.Validate(ValidCommand(
            originalPrice: null,
            optionTypes: optionTypes,
            variants: [Variant(mangoId, "SKU-MANGO", 24.99m)]));

        result.IsValid.Should().BeTrue("RULE-4's price requirement applies only to a product with no option types");
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

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithMoreThanTwoDecimalPlaces_HasPriceInvalidError()
    {
        var result = _validator.Validate(ValidCommand(originalPrice: 24.999m));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.PriceInvalid);
    }

    // =========================================================================
    // Sale price (RULE-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithSalePriceEqualToPrice_HasSalePriceTooHighError()
    {
        var result = _validator.Validate(ValidCommand(originalPrice: 24.99m, salePrice: 24.99m));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SalePriceTooHigh);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithSalePriceAboveOriginalPrice_HasSalePriceTooHighError()
    {
        var result = _validator.Validate(ValidCommand(originalPrice: 19.99m, salePrice: 24.99m));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SalePriceTooHigh);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithSalePriceBelowOriginalPrice_IsValid()
    {
        var result = _validator.Validate(ValidCommand(originalPrice: 24.99m, salePrice: 19.99m));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_OnAVariantWithSalePriceAboveItsOwnPrice_HasSalePriceTooHighError()
    {
        var optionTypes = OneFlavourOptionType(out var mangoId, out var mintId);
        var result = _validator.Validate(ValidCommand(
            originalPrice: null,
            optionTypes: optionTypes,
            variants: [
                Variant(mangoId, "SKU-MANGO", 24.99m, salePrice: 29.99m),
                Variant(mintId, "SKU-MINT", 24.99m),
            ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SalePriceTooHigh);
    }

    // =========================================================================
    // Gallery — image type/size, one primary (RULE-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithAnImageOfAnUnacceptedType_HasImageInvalidTypeError()
    {
        var result = _validator.Validate(ValidCommand(gallery: [
            new ProductGalleryEntry(null, new ProductImageUpload([1, 2, 3], "photo.gif", "image/gif"), 0, true, Guid.NewGuid()),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.ImageInvalidType);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithAnImageOverTwoMegabytes_HasImageTooLargeError()
    {
        var oversized = new byte[(2 * 1024 * 1024) + 1];
        var result = _validator.Validate(ValidCommand(gallery: [
            new ProductGalleryEntry(null, new ProductImageUpload(oversized, "photo.png", "image/png"), 0, true, Guid.NewGuid()),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.ImageTooLarge);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    [Trait("Feature", "create-product")]
    public void Validate_WithAnAcceptedImageType_IsValid(string contentType)
    {
        var result = _validator.Validate(ValidCommand(gallery: [
            new ProductGalleryEntry(null, new ProductImageUpload([1, 2, 3], "photo.png", contentType), 0, true, Guid.NewGuid()),
        ]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithMoreThanOneImageMarkedPrimary_HasPrimaryImageRequiredError()
    {
        var result = _validator.Validate(ValidCommand(gallery: [
            new ProductGalleryEntry(null, new ProductImageUpload([1, 2, 3], "a.png", "image/png"), 0, true, Guid.NewGuid()),
            new ProductGalleryEntry(null, new ProductImageUpload([1, 2, 3], "b.png", "image/png"), 1, true, Guid.NewGuid()),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.PrimaryImageRequired);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithNoImagesAtAll_DoesNotRequireAPrimaryImage()
    {
        var result = _validator.Validate(ValidCommand(gallery: []));

        result.IsValid.Should().BeTrue("an Unpublished product may be saved with no images yet (RULE-15)");
    }

    // =========================================================================
    // Variant SKU distinctness (RULE-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithAVariantSkuMatchingTheProductSku_HasSkuDuplicatedInRequestError()
    {
        var optionTypes = OneFlavourOptionType(out var mangoId, out var mintId);
        var result = _validator.Validate(ValidCommand(
            sku: "ELF-BC5000",
            originalPrice: null,
            optionTypes: optionTypes,
            variants: [
                Variant(mangoId, "ELF-BC5000", 24.99m),
                Variant(mintId, "SKU-MINT", 24.99m),
            ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SkuDuplicatedInRequest);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithTwoVariantsSharingASku_HasSkuDuplicatedInRequestError()
    {
        var optionTypes = OneFlavourOptionType(out var mangoId, out var mintId);
        var result = _validator.Validate(ValidCommand(
            originalPrice: null,
            optionTypes: optionTypes,
            variants: [
                Variant(mangoId, "SKU-SAME", 24.99m),
                Variant(mintId, "SKU-SAME", 24.99m),
            ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SkuDuplicatedInRequest);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithABlankVariantSku_HasSkuRequiredError()
    {
        var optionTypes = OneFlavourOptionType(out var mangoId, out _);
        var result = _validator.Validate(ValidCommand(
            originalPrice: null,
            optionTypes: optionTypes,
            variants: [Variant(mangoId, "", 24.99m)]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SkuRequired);
    }

    // =========================================================================
    // Option types — presence and uniqueness (RULE-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithABlankOptionTypeName_HasOptionNameRequiredError()
    {
        var result = _validator.Validate(ValidCommand(optionTypes: [
            new OptionTypeInput(null, "", [new OptionValueInput(null, "Mango")]),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.OptionNameRequired);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithAnOptionTypeWithNoValues_HasOptionValueRequiredError()
    {
        var result = _validator.Validate(ValidCommand(optionTypes: [
            new OptionTypeInput(null, "Flavour", []),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.OptionValueRequired);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithDuplicateOptionTypeNames_HasOptionNameDuplicatedError()
    {
        var result = _validator.Validate(ValidCommand(optionTypes: [
            new OptionTypeInput(null, "Flavour", [new OptionValueInput(null, "Mango")]),
            new OptionTypeInput(null, "flavour", [new OptionValueInput(null, "Mint")]),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.OptionNameDuplicated);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Validate_WithDuplicateOptionValuesWithinAType_HasOptionValueDuplicatedError()
    {
        var result = _validator.Validate(ValidCommand(optionTypes: [
            new OptionTypeInput(null, "Flavour", [
                new OptionValueInput(null, "Mango"), new OptionValueInput(null, "Mango"),
            ]),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.OptionValueDuplicated);
    }

    // =========================================================================
    // Specifications (product-description RULE-3, RULE-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithNoSpecifications_IsValid()
    {
        var result = _validator.Validate(ValidCommand(specifications: []));

        result.IsValid.Should().BeTrue("specifications are optional (RULE-3)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithEveryRowNamedAndValued_IsValid()
    {
        var result = _validator.Validate(ValidCommand(specifications: [
            new SpecificationInput(null, "Material", "Stainless steel", 0),
            new SpecificationInput(null, "Capacity", "750 ml", 1),
        ]));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "product-description")]
    public void Validate_WithABlankSpecificationName_HasSpecificationNameRequiredError(string name)
    {
        var result = _validator.Validate(ValidCommand(specifications: [
            new SpecificationInput(null, name, "Stainless steel", 0),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SpecificationNameRequired);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "product-description")]
    public void Validate_WithABlankSpecificationValue_HasSpecificationValueRequiredError(string value)
    {
        var result = _validator.Validate(ValidCommand(specifications: [
            new SpecificationInput(null, "Material", value, 0),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SpecificationValueRequired);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithDuplicateNamesIgnoringCaseAndSpaces_HasSpecificationNameDuplicatedError()
    {
        var result = _validator.Validate(ValidCommand(specifications: [
            new SpecificationInput(null, "Material", "Stainless steel", 0),
            new SpecificationInput(null, " material ", "Aluminum", 1),
        ]));

        result.Errors.Should().Contain(e => e.ErrorMessage == ProductErrorKeys.SpecificationNameDuplicated);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Validate_WithDuplicateSpecificationNames_CarriesBothRowPositionsInCustomState()
    {
        var result = _validator.Validate(ValidCommand(specifications: [
            new SpecificationInput(null, "Material", "Stainless steel", 0),
            new SpecificationInput(null, "material", "Aluminum", 1),
        ]));

        var failure = result.Errors.Single(e => e.ErrorMessage == ProductErrorKeys.SpecificationNameDuplicated);
        failure.CustomState.Should().BeEquivalentTo(new[] { "0", "1" },
            "the row positions travel through Result.ErrorArgs so the form can flag both offending rows");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static IReadOnlyList<OptionTypeInput> OneFlavourOptionType(out Guid mangoId, out Guid mintId)
    {
        mangoId = Guid.NewGuid();
        mintId = Guid.NewGuid();
        return [new OptionTypeInput(Guid.NewGuid(), "Flavour", [
            new OptionValueInput(mangoId, "Mango"), new OptionValueInput(mintId, "Mint"),
        ])];
    }

    private static VariantInput Variant(Guid optionValueId, string sku, decimal? price, decimal? salePrice = null) =>
        new(Guid.NewGuid(), [optionValueId], sku, price, salePrice, true, null);
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-8 (image type/size refused): Validate_WithAnImageOfAnUnacceptedType_HasImageInvalidTypeError,
//        Validate_WithAnImageOverTwoMegabytes_HasImageTooLargeError, Validate_WithAnAcceptedImageType_IsValid
// AC-21 (name blank/too long): Validate_WithBlankName_HasNameRequiredError,
//        Validate_WithNameOver150Characters_HasNameTooLongError, Validate_WithNameOfExactly150Characters_IsValid,
//        Validate_WithNoDescription_IsValid
// product-description AC-4 (2,001 and exactly 20,000 characters both save): Validate_WithDescriptionOfExactly20000Characters_IsValid
// product-description AC-5 (20,001 characters rejected): Validate_WithDescriptionOver20000Characters_HasDescriptionTooLongError
// product-description AC-7 (blank row name/value identified): Validate_WithABlankSpecificationName_HasSpecificationNameRequiredError,
//        Validate_WithABlankSpecificationValue_HasSpecificationValueRequiredError
// product-description AC-8 ("Material"/" material " duplicate rejected): Validate_WithDuplicateNamesIgnoringCaseAndSpaces_HasSpecificationNameDuplicatedError,
//        Validate_WithDuplicateSpecificationNames_CarriesBothRowPositionsInCustomState
// product-description AC-10/AC-11 (unsupported markup removed/rejected, executable content never saved): Validate_WithDescriptionContainingUnsupportedMarkup_HasDescriptionUnsupportedContentError,
//        Validate_WithEveryFR1SupportedFormat_IsValid, Validate_WithDescriptionOverMaxHtmlBytes_HasDescriptionTooLargeError
// AC-23 (category and brand required): Validate_WithEmptyCategoryId_HasCategoryRequiredError, Validate_WithEmptyBrandId_HasBrandRequiredError
// AC-25 (sale price must be lower than price): Validate_WithSalePriceEqualToPrice_HasSalePriceTooHighError,
//        Validate_WithSalePriceAboveOriginalPrice_HasSalePriceTooHighError, Validate_WithSalePriceBelowOriginalPrice_IsValid,
//        Validate_OnAVariantWithSalePriceAboveItsOwnPrice_HasSalePriceTooHighError
// AC-27 (variant/product SKU distinctness within the request): Validate_WithAVariantSkuMatchingTheProductSku_HasSkuDuplicatedInRequestError,
//        Validate_WithTwoVariantsSharingASku_HasSkuDuplicatedInRequestError, Validate_WithABlankVariantSku_HasSkuRequiredError,
//        Validate_WithBlankSku_HasSkuRequiredError
// AC-28 (duplicate/empty option names and values refused): Validate_WithABlankOptionTypeName_HasOptionNameRequiredError,
//        Validate_WithAnOptionTypeWithNoValues_HasOptionValueRequiredError, Validate_WithDuplicateOptionTypeNames_HasOptionNameDuplicatedError,
//        Validate_WithDuplicateOptionValuesWithinAType_HasOptionValueDuplicatedError
// RULE-4 (price required only without option types): Validate_WithNoOptionTypesAndNoPrice_HasPriceRequiredError,
//        Validate_WithOptionTypesAndNoProductPrice_IsValid, Validate_WithZeroOrNegativePrice_HasPriceInvalidError,
//        Validate_WithMoreThanTwoDecimalPlaces_HasPriceInvalidError
// RULE-7 (exactly one primary whenever any image exists): Validate_WithMoreThanOneImageMarkedPrimary_HasPrimaryImageRequiredError,
//        Validate_WithNoImagesAtAll_DoesNotRequireAPrimaryImage

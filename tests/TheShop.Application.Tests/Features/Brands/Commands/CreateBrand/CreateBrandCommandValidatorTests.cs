using FluentAssertions;
using TheShop.Application.Features.Brands;
using TheShop.Application.Features.Brands.Commands.CreateBrand;
using TheShop.Application.Features.Brands.DTOs;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.CreateBrand;

/// <summary>
/// Tests for <see cref="CreateBrandCommandValidator"/> — field-level validation mirroring the
/// domain invariants (RULE-1/RULE-3) plus the logo type/size constraints (RULE-4).
/// <see href=".specs/add-brand/spec.md"/>
/// </summary>
public class CreateBrandCommandValidatorTests
{
    private readonly CreateBrandCommandValidator _validator = new();

    private static CreateBrandCommand ValidCommand(
        string name = "Elf Bar",
        string? description = null,
        BrandLogoUpload? logo = null,
        bool isActive = true) =>
        new(name, description, logo, isActive);

    private static BrandLogoUpload Logo(string contentType = "image/png", int sizeBytes = 1024) =>
        new(new byte[sizeBytes], "logo.png", contentType);

    // =========================================================================
    // Name — RULE-1 (AC-2)
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenNameIsEmptyOrWhitespace_HasNameRequiredError(string name)
    {
        var cmd = ValidCommand(name: name);
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Name) &&
            e.ErrorMessage == BrandErrorKeys.NameRequired);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WithAName_HasNoNameRequiredError()
    {
        var cmd = ValidCommand();
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Name));
    }

    // =========================================================================
    // Name — RULE-3 (100 characters)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenNameExceeds100Characters_HasNameTooLongError()
    {
        var cmd = ValidCommand(name: new string('a', 101));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Name) &&
            e.ErrorMessage == BrandErrorKeys.NameTooLong);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenNameIsExactly100Characters_HasNoNameError()
    {
        var cmd = ValidCommand(name: new string('a', 100));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Name));
    }

    // =========================================================================
    // Description — RULE-3 (250 characters, optional per FR-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenDescriptionExceeds250Characters_HasDescriptionTooLongError()
    {
        var cmd = ValidCommand(description: new string('a', 251));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Description) &&
            e.ErrorMessage == BrandErrorKeys.DescriptionTooLong);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenDescriptionIsExactly250Characters_HasNoDescriptionError()
    {
        var cmd = ValidCommand(description: new string('a', 250));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Description));
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenDescriptionIsNull_HasNoDescriptionError()
    {
        var cmd = ValidCommand(description: null);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Description));
    }

    // =========================================================================
    // Logo — RULE-4 accepted content types (AC-5)
    // =========================================================================

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("image/svg+xml")]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenLogoContentTypeIsNotAccepted_HasLogoInvalidTypeError(string contentType)
    {
        var cmd = ValidCommand(logo: Logo(contentType: contentType));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "Logo.ContentType" &&
            e.ErrorMessage == BrandErrorKeys.LogoInvalidType);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenLogoContentTypeIsAccepted_HasNoLogoTypeError(string contentType)
    {
        var cmd = ValidCommand(logo: Logo(contentType: contentType));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "Logo.ContentType");
    }

    // =========================================================================
    // Logo — RULE-4 size limit (2 MB, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenLogoExceeds2Megabytes_HasLogoTooLargeError()
    {
        var cmd = ValidCommand(logo: Logo(sizeBytes: 2 * 1024 * 1024 + 1));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "Logo.Content" &&
            e.ErrorMessage == BrandErrorKeys.LogoTooLarge);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenLogoIsExactly2Megabytes_HasNoLogoSizeError()
    {
        var cmd = ValidCommand(logo: Logo(sizeBytes: 2 * 1024 * 1024));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "Logo.Content");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WhenLogoIsNull_SkipsLogoValidationEntirely()
    {
        var cmd = ValidCommand(logo: null);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "Logo.ContentType" || e.PropertyName == "Logo.Content");
    }

    // =========================================================================
    // Full valid command (AC-1, AC-4, AC-6, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WithNameDescriptionLogoAndInactiveStatus_IsValid()
    {
        var cmd = ValidCommand(name: "Elf Bar", description: "A vape brand.", logo: Logo(), isActive: false);
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Validate_WithNameOnly_IsValid()
    {
        var cmd = ValidCommand();
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Validate_WithNameOnly_IsValid
// AC-2: Validate_WhenNameIsEmptyOrWhitespace_HasNameRequiredError
// AC-4: Validate_WhenLogoContentTypeIsAccepted_HasNoLogoTypeError,
//        Validate_WithNameDescriptionLogoAndInactiveStatus_IsValid
// AC-5: Validate_WhenLogoContentTypeIsNotAccepted_HasLogoInvalidTypeError,
//        Validate_WhenLogoExceeds2Megabytes_HasLogoTooLargeError
// AC-6: Validate_WithNameOnly_IsValid
// AC-7: Validate_WithNameDescriptionLogoAndInactiveStatus_IsValid (isActive: false)

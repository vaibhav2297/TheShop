using FluentAssertions;
using TheShop.Application.Features.Brands;
using TheShop.Application.Features.Brands.Commands.UpdateBrand;
using TheShop.Application.Features.Brands.DTOs;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.UpdateBrand;

/// <summary>
/// Tests for <see cref="UpdateBrandCommandValidator"/> — field-level validation mirroring the
/// domain invariants (RULE-1/RULE-3) plus the logo type/size constraints (RULE-4), applied only
/// when a replacement logo is supplied and the existing one is not being removed (AC-7, AC-11).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class UpdateBrandCommandValidatorTests
{
    private readonly UpdateBrandCommandValidator _validator = new();

    private static UpdateBrandCommand ValidCommand(
        Guid? id = null,
        string name = "Elf Bar",
        string? description = null,
        bool isActive = true,
        BrandLogoUpload? newLogo = null,
        bool removeLogo = false) =>
        new(id ?? Guid.NewGuid(), name, description, isActive, newLogo, removeLogo);

    private static BrandLogoUpload Logo(string contentType = "image/png", int sizeBytes = 1024) =>
        new(new byte[sizeBytes], "logo.png", contentType);

    // =========================================================================
    // Id — must identify a brand
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenIdIsEmpty_HasIdError()
    {
        var cmd = ValidCommand(id: Guid.Empty);
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.Id));
    }

    // =========================================================================
    // Name — RULE-1 (AC-7)
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "manage-brands")]
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
    [Trait("Feature", "manage-brands")]
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
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenNameExceeds100Characters_HasNameTooLongError()
    {
        var cmd = ValidCommand(name: new string('a', 101));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Name) &&
            e.ErrorMessage == BrandErrorKeys.NameTooLong);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenNameIsExactly100Characters_HasNoNameError()
    {
        var cmd = ValidCommand(name: new string('a', 100));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Name));
    }

    // =========================================================================
    // Description — RULE-3 (250 characters)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenDescriptionExceeds250Characters_HasDescriptionTooLongError()
    {
        var cmd = ValidCommand(description: new string('a', 251));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Description) &&
            e.ErrorMessage == BrandErrorKeys.DescriptionTooLong);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenDescriptionIsExactly250Characters_HasNoDescriptionError()
    {
        var cmd = ValidCommand(description: new string('a', 250));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Description));
    }

    // =========================================================================
    // Logo — RULE-4, only checked for a replacement that isn't accompanied by RemoveLogo (AC-11)
    // =========================================================================

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenNewLogoContentTypeIsNotAccepted_HasLogoInvalidTypeError(string contentType)
    {
        var cmd = ValidCommand(newLogo: Logo(contentType: contentType));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "NewLogo.ContentType" &&
            e.ErrorMessage == BrandErrorKeys.LogoInvalidType);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenNewLogoContentTypeIsAccepted_HasNoLogoTypeError(string contentType)
    {
        var cmd = ValidCommand(newLogo: Logo(contentType: contentType));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "NewLogo.ContentType");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenNewLogoExceeds2Megabytes_HasLogoTooLargeError()
    {
        var cmd = ValidCommand(newLogo: Logo(sizeBytes: 2 * 1024 * 1024 + 1));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "NewLogo.Content" &&
            e.ErrorMessage == BrandErrorKeys.LogoTooLarge);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenNewLogoIsExactly2Megabytes_HasNoLogoSizeError()
    {
        var cmd = ValidCommand(newLogo: Logo(sizeBytes: 2 * 1024 * 1024));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "NewLogo.Content");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenNoNewLogoIsSupplied_SkipsLogoValidationEntirely()
    {
        var cmd = ValidCommand(newLogo: null, removeLogo: false);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "NewLogo.ContentType" || e.PropertyName == "NewLogo.Content");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WhenRemoveLogoIsTrue_SkipsLogoValidationEvenIfANewLogoIsSupplied()
    {
        // RemoveLogo takes precedence over NewLogo (plan: "RemoveLogo takes precedence when both
        // it and NewLogo are supplied") — an invalid NewLogo alongside RemoveLogo must not block
        // a save that is, in effect, only removing the logo.
        var cmd = ValidCommand(newLogo: Logo(contentType: "application/pdf"), removeLogo: true);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "NewLogo.ContentType" || e.PropertyName == "NewLogo.Content");
    }

    // =========================================================================
    // Full valid command (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Validate_WithNameDescriptionLogoAndInactiveStatus_IsValid()
    {
        var cmd = ValidCommand(name: "Elf Bar", description: "A vape brand.", isActive: false, newLogo: Logo());
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: Validate_WithNameDescriptionLogoAndInactiveStatus_IsValid
// AC-7: Validate_WhenNameIsEmptyOrWhitespace_HasNameRequiredError
// AC-11: Validate_WhenNewLogoContentTypeIsNotAccepted_HasLogoInvalidTypeError,
//         Validate_WhenNewLogoExceeds2Megabytes_HasLogoTooLargeError

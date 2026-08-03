using FluentAssertions;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.Commands.CreateCategory;
using TheShop.Application.Features.Categories.DTOs;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.CreateCategory;

/// <summary>
/// Tests for <see cref="CreateCategoryCommandValidator"/> — field-level validation mirroring the
/// domain invariants (RULE-1/RULE-3) plus the image type/size constraints (RULE-4).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class CreateCategoryCommandValidatorTests
{
    private readonly CreateCategoryCommandValidator _validator = new();

    private static CreateCategoryCommand ValidCommand(
        string name = "Disposables",
        string? description = null,
        CategoryImageUpload? image = null,
        bool isActive = true) =>
        new(name, description, image, isActive);

    private static CategoryImageUpload Image(string contentType = "image/png", int sizeBytes = 1024) =>
        new(new byte[sizeBytes], "image.png", contentType);

    // =========================================================================
    // Name — RULE-1 (AC-9)
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenNameIsEmptyOrWhitespace_HasNameRequiredError(string name)
    {
        var cmd = ValidCommand(name: name);
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Name) &&
            e.ErrorMessage == CategoryErrorKeys.NameRequired);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WithAName_HasNoNameRequiredError()
    {
        var cmd = ValidCommand();
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Name));
    }

    // =========================================================================
    // Name — RULE-3 (100 characters, AC-12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenNameExceeds100Characters_HasNameTooLongError()
    {
        var cmd = ValidCommand(name: new string('a', 101));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Name) &&
            e.ErrorMessage == CategoryErrorKeys.NameTooLong);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenNameIsExactly100Characters_HasNoNameError()
    {
        var cmd = ValidCommand(name: new string('a', 100));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Name));
    }

    // =========================================================================
    // Description — RULE-3 (250 characters, AC-12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenDescriptionExceeds250Characters_HasDescriptionTooLongError()
    {
        var cmd = ValidCommand(description: new string('a', 251));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(cmd.Description) &&
            e.ErrorMessage == CategoryErrorKeys.DescriptionTooLong);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenDescriptionIsExactly250Characters_HasNoDescriptionError()
    {
        var cmd = ValidCommand(description: new string('a', 250));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Description));
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenDescriptionIsNull_HasNoDescriptionError()
    {
        var cmd = ValidCommand(description: null);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(cmd.Description));
    }

    // =========================================================================
    // Image — RULE-4 accepted content types (AC-14)
    // =========================================================================

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("image/svg+xml")]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenImageContentTypeIsNotAccepted_HasImageInvalidTypeError(string contentType)
    {
        var cmd = ValidCommand(image: Image(contentType: contentType));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "Image.ContentType" &&
            e.ErrorMessage == CategoryErrorKeys.ImageInvalidType);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenImageContentTypeIsAccepted_HasNoImageTypeError(string contentType)
    {
        var cmd = ValidCommand(image: Image(contentType: contentType));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "Image.ContentType");
    }

    // =========================================================================
    // Image — RULE-4 size limit (2 MB, AC-14)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenImageExceeds2Megabytes_HasImageTooLargeError()
    {
        var cmd = ValidCommand(image: Image(sizeBytes: 2 * 1024 * 1024 + 1));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "Image.Content" &&
            e.ErrorMessage == CategoryErrorKeys.ImageTooLarge);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenImageIsExactly2Megabytes_HasNoImageSizeError()
    {
        var cmd = ValidCommand(image: Image(sizeBytes: 2 * 1024 * 1024));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "Image.Content");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenImageIsNull_SkipsImageValidationEntirely()
    {
        var cmd = ValidCommand(image: null);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "Image.ContentType" || e.PropertyName == "Image.Content");
    }

    // =========================================================================
    // Full valid command (AC-6, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WithNameDescriptionImageAndInactiveStatus_IsValid()
    {
        var cmd = ValidCommand(name: "Disposables", description: "Single-use vape devices.", image: Image(), isActive: false);
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
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
// AC-6: Validate_WithNameOnly_IsValid
// AC-7: Validate_WithNameOnly_IsValid, Validate_WithNameDescriptionImageAndInactiveStatus_IsValid (isActive: false)
// AC-9: Validate_WhenNameIsEmptyOrWhitespace_HasNameRequiredError
// AC-12: Validate_WhenNameExceeds100Characters_HasNameTooLongError,
//         Validate_WhenNameIsExactly100Characters_HasNoNameError,
//         Validate_WhenDescriptionExceeds250Characters_HasDescriptionTooLongError,
//         Validate_WhenDescriptionIsExactly250Characters_HasNoDescriptionError
// AC-14: Validate_WhenImageContentTypeIsNotAccepted_HasImageInvalidTypeError,
//         Validate_WhenImageExceeds2Megabytes_HasImageTooLargeError

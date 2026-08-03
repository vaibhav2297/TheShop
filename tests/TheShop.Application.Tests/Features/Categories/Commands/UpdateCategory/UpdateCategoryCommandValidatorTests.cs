using FluentAssertions;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.Commands.UpdateCategory;
using TheShop.Application.Features.Categories.DTOs;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.UpdateCategory;

/// <summary>
/// Tests for <see cref="UpdateCategoryCommandValidator"/> — field-level validation mirroring the
/// domain invariants (RULE-1/RULE-3) plus the image type/size constraints (RULE-4), applied only
/// when a replacement image is supplied and the existing one is not being removed (AC-13, AC-14).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class UpdateCategoryCommandValidatorTests
{
    private readonly UpdateCategoryCommandValidator _validator = new();

    private static UpdateCategoryCommand ValidCommand(
        Guid? id = null,
        string name = "Disposables",
        string? description = null,
        bool isActive = true,
        CategoryImageUpload? newImage = null,
        bool removeImage = false) =>
        new(id ?? Guid.NewGuid(), name, description, isActive, newImage, removeImage);

    private static CategoryImageUpload Image(string contentType = "image/png", int sizeBytes = 1024) =>
        new(new byte[sizeBytes], "image.png", contentType);

    // =========================================================================
    // Id — must identify a category
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenIdIsEmpty_HasIdError()
    {
        var cmd = ValidCommand(id: Guid.Empty);
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.Id));
    }

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
    // Name — RULE-3 (100 characters)
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
    // Description — RULE-3 (250 characters)
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

    // =========================================================================
    // Image — RULE-4, only checked for a replacement that isn't accompanied by RemoveImage (AC-14)
    // =========================================================================

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenNewImageContentTypeIsNotAccepted_HasImageInvalidTypeError(string contentType)
    {
        var cmd = ValidCommand(newImage: Image(contentType: contentType));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "NewImage.ContentType" &&
            e.ErrorMessage == CategoryErrorKeys.ImageInvalidType);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenNewImageContentTypeIsAccepted_HasNoImageTypeError(string contentType)
    {
        var cmd = ValidCommand(newImage: Image(contentType: contentType));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "NewImage.ContentType");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenNewImageExceeds2Megabytes_HasImageTooLargeError()
    {
        var cmd = ValidCommand(newImage: Image(sizeBytes: 2 * 1024 * 1024 + 1));
        var result = _validator.Validate(cmd);

        result.Errors.Should().Contain(e =>
            e.PropertyName == "NewImage.Content" &&
            e.ErrorMessage == CategoryErrorKeys.ImageTooLarge);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenNewImageIsExactly2Megabytes_HasNoImageSizeError()
    {
        var cmd = ValidCommand(newImage: Image(sizeBytes: 2 * 1024 * 1024));
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "NewImage.Content");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenNoNewImageIsSupplied_SkipsImageValidationEntirely()
    {
        var cmd = ValidCommand(newImage: null, removeImage: false);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "NewImage.ContentType" || e.PropertyName == "NewImage.Content");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WhenRemoveImageIsTrue_SkipsImageValidationEvenIfANewImageIsSupplied()
    {
        // RemoveImage takes precedence over NewImage (plan: "RemoveImage takes precedence when both
        // it and NewImage are supplied") — an invalid NewImage alongside RemoveImage must not block
        // a save that is, in effect, only removing the image.
        var cmd = ValidCommand(newImage: Image(contentType: "application/pdf"), removeImage: true);
        var result = _validator.Validate(cmd);

        result.Errors.Should().NotContain(e => e.PropertyName == "NewImage.ContentType" || e.PropertyName == "NewImage.Content");
    }

    // =========================================================================
    // Full valid command (AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Validate_WithNameDescriptionImageAndInactiveStatus_IsValid()
    {
        var cmd = ValidCommand(name: "Disposables", description: "Single-use vape devices.", isActive: false, newImage: Image());
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-8: Validate_WithNameDescriptionImageAndInactiveStatus_IsValid
// AC-9: Validate_WhenNameIsEmptyOrWhitespace_HasNameRequiredError
// AC-14: Validate_WhenNewImageContentTypeIsNotAccepted_HasImageInvalidTypeError,
//         Validate_WhenNewImageExceeds2Megabytes_HasImageTooLargeError

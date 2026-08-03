using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.Commands.UpdateCategory;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.UpdateCategory;

/// <summary>
/// Tests for <see cref="UpdateCategoryHandler"/> — the RULE-2 self-exclusive uniqueness pre-check
/// (AC-11), <c>Category</c> mutation orchestration (Rename/ChangeDescription/Activate/Deactivate,
/// AC-8), the image remove/replace flows with their orphaned-object compensation (RULE-11, AC-13),
/// and persistence-failure handling that leaves the category unchanged.
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class UpdateCategoryHandlerTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private UpdateCategoryHandler CreateSut() => new(_categories, _fileStorage);

    private static UpdateCategoryCommand Command(
        Guid id,
        string name = "Disposables",
        string? description = null,
        bool isActive = true,
        CategoryImageUpload? newImage = null,
        bool removeImage = false) =>
        new(id, name, description, isActive, newImage, removeImage);

    private static CategoryImageUpload Image() => new([1, 2, 3], "image.png", "image/png");

    private Category SetUpExistingCategory(
        string name = "Disposables", string? description = null, bool isActive = true, string? imagePath = null)
    {
        var category = Category.Create(name, description, isActive);
        if (imagePath is not null)
            category.AttachImage(imagePath);

        _categories.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
        return category;
    }

    private void SetUpNoNameClash() =>
        _categories.ExistsByNormalizedNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);

    private void SetUpSuccessfulUpdate() =>
        _categories.UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());

    private void SetUpSuccessfulImageUpload(string key = "categories/abc/new-image.png") =>
        _fileStorage.UploadAsync(
            StorageArea.CategoryImages, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(key);

    // =========================================================================
    // Happy path — name, description, status all change (AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithValidCommand_ReturnsSuccessResult()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        var result = await CreateSut().Handle(Command(category.Id, name: "Pod Systems"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithValidCommand_PersistsTheRenamedRedescribedCategory()
    {
        var category = SetUpExistingCategory(name: "Disposables", description: "Old description.");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(
            Command(category.Id, name: "Pod Systems", description: "New description."), CancellationToken.None);

        await _categories.Received(1).UpdateAsync(
            Arg.Is<Category>(c => c.Name == "Pod Systems" && c.Description == "New description."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithValidCommand_ReturnsDtoReflectingTheNewDetails()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        var result = await CreateSut().Handle(Command(category.Id, name: "Pod Systems"), CancellationToken.None);

        result.Value.Id.Should().Be(category.Id);
        result.Value.Name.Should().Be("Pod Systems");
    }

    // =========================================================================
    // Category no longer exists (edge case: removed by another staff member, AC-23)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenCategoryDoesNotExist_ReturnsNotFoundFailure()
    {
        _categories.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Category?)null);

        var result = await CreateSut().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.NotFound);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenCategoryDoesNotExist_DoesNotAttemptToPersistAnything()
    {
        _categories.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Category?)null);

        await CreateSut().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await _categories.DidNotReceive().UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // RULE-2 self-exclusive uniqueness check (AC-10, AC-11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenRenamedToAnotherCategorysName_ReturnsAlreadyExistsFailure()
    {
        var category = SetUpExistingCategory();
        _categories.ExistsByNormalizedNameAsync(Arg.Any<string>(), category.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateSut().Handle(Command(category.Id, name: "Pod Systems"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.AlreadyExists);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenRenamedToAnotherCategorysName_DoesNotPersistAnything()
    {
        var category = SetUpExistingCategory();
        _categories.ExistsByNormalizedNameAsync(Arg.Any<string>(), category.Id, Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().Handle(Command(category.Id, name: "Pod Systems"), CancellationToken.None);

        await _categories.DidNotReceive().UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenCheckingUniqueness_ExcludesTheCategorysOwnId()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(category.Id, name: "Pod Systems"), CancellationToken.None);

        await _categories.Received(1).ExistsByNormalizedNameAsync("Pod Systems", category.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenSavedWithNoChangesAtAll_Succeeds()
    {
        // AC-11: excludeCategoryId means the category's own current name never collides with itself.
        var category = SetUpExistingCategory(name: "Disposables", description: "Single-use vape devices.", isActive: true);
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        var result = await CreateSut().Handle(
            Command(category.Id, name: "Disposables", description: "Single-use vape devices.", isActive: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        category.Name.Should().Be("Disposables");
    }

    // =========================================================================
    // Domain invariant translation — defence in depth behind the validator (RULE-1/RULE-3, AC-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenNameIsEmpty_ReturnsNameRequiredFailure()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();

        var result = await CreateSut().Handle(Command(category.Id, name: ""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.NameRequired);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenNameIsEmpty_LeavesTheCategoryUnchanged()
    {
        var category = SetUpExistingCategory(name: "Disposables");
        SetUpNoNameClash();

        await CreateSut().Handle(Command(category.Id, name: ""), CancellationToken.None);

        category.Name.Should().Be("Disposables");
        await _categories.DidNotReceive().UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenDescriptionExceeds250Characters_ReturnsDescriptionTooLongFailure()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();

        var result = await CreateSut().Handle(
            Command(category.Id, description: new string('a', 251)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.DescriptionTooLong);
    }

    // =========================================================================
    // Status change (AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenIsActiveIsFalse_PersistsAnInactiveCategory()
    {
        var category = SetUpExistingCategory(isActive: true);
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(category.Id, isActive: false), CancellationToken.None);

        await _categories.Received(1).UpdateAsync(Arg.Is<Category>(c => !c.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenIsActiveIsTrue_PersistsAnActiveCategory()
    {
        var category = SetUpExistingCategory(isActive: false);
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(category.Id, isActive: true), CancellationToken.None);

        await _categories.Received(1).UpdateAsync(Arg.Is<Category>(c => c.IsActive), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Image removal — discards the outgoing object (RULE-11, AC-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenRemoveImageIsTrue_PersistsTheCategoryWithNoImage()
    {
        var category = SetUpExistingCategory(imagePath: "categories/abc/image.webp");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(category.Id, removeImage: true), CancellationToken.None);

        await _categories.Received(1).UpdateAsync(Arg.Is<Category>(c => c.ImagePath == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenRemoveImageIsTrue_DeletesThePreviousImageObjectAfterSaving()
    {
        var category = SetUpExistingCategory(imagePath: "categories/abc/image.webp");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(category.Id, removeImage: true), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.CategoryImages, "categories/abc/image.webp", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenRemoveImageIsTrueButNoImageExisted_DoesNotAttemptToDeleteAnything()
    {
        var category = SetUpExistingCategory(imagePath: null);
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(category.Id, removeImage: true), CancellationToken.None);

        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<StorageArea>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Image replacement — new upload attached, old object discarded (RULE-11, AC-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithANewImage_UploadsItToTheCategoryImagesArea()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();
        SetUpSuccessfulImageUpload();

        await CreateSut().Handle(Command(category.Id, newImage: Image()), CancellationToken.None);

        await _fileStorage.Received(1).UploadAsync(
            StorageArea.CategoryImages, category.Id, Arg.Any<Stream>(), "image.png", "image/png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithANewImage_PersistsTheCategoryWithTheUploadedKey()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();
        SetUpSuccessfulImageUpload("categories/abc/new-image.png");

        await CreateSut().Handle(Command(category.Id, newImage: Image()), CancellationToken.None);

        await _categories.Received(1).UpdateAsync(
            Arg.Is<Category>(c => c.ImagePath == "categories/abc/new-image.png"), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenANewImageReplacesAnExistingOne_DeletesTheReplacedImageObject()
    {
        var category = SetUpExistingCategory(imagePath: "categories/abc/old-image.webp");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();
        SetUpSuccessfulImageUpload("categories/abc/new-image.png");

        await CreateSut().Handle(Command(category.Id, newImage: Image()), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.CategoryImages, "categories/abc/old-image.webp", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithNoImageChangeRequested_DoesNotCallFileStorageAtAll()
    {
        var category = SetUpExistingCategory(imagePath: "categories/abc/image.webp");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(category.Id, newImage: null, removeImage: false), CancellationToken.None);

        await _fileStorage.DidNotReceive().UploadAsync(
            Arg.Any<StorageArea>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<StorageArea>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenRemoveImageAndNewImageAreBothSupplied_RemoveImageTakesPrecedence()
    {
        var category = SetUpExistingCategory(imagePath: "categories/abc/image.webp");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(category.Id, newImage: Image(), removeImage: true), CancellationToken.None);

        await _fileStorage.DidNotReceive().UploadAsync(
            Arg.Any<StorageArea>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _categories.Received(1).UpdateAsync(Arg.Is<Category>(c => c.ImagePath == null), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Persistence failure after a new image upload — orphaned-object compensation
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenUpdateAsyncFailsAfterNewImageUpload_DeletesTheOrphanedUpload()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();
        SetUpSuccessfulImageUpload("categories/abc/new-image.png");
        _categories.UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(CategoryErrorKeys.AlreadyExists));

        await CreateSut().Handle(Command(category.Id, newImage: Image()), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.CategoryImages, "categories/abc/new-image.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenUpdateAsyncFailsAfterNewImageUpload_ReturnsUpdateAsyncsFailureKey()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();
        SetUpSuccessfulImageUpload("categories/abc/new-image.png");
        _categories.UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(CategoryErrorKeys.AlreadyExists));

        var result = await CreateSut().Handle(Command(category.Id, newImage: Image()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.AlreadyExists);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenUpdateAsyncFailsAfterNewImageUpload_DoesNotDeleteTheStillCurrentImage()
    {
        var category = SetUpExistingCategory(imagePath: "categories/abc/old-image.webp");
        SetUpNoNameClash();
        SetUpSuccessfulImageUpload("categories/abc/new-image.png");
        _categories.UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(CategoryErrorKeys.AlreadyExists));

        await CreateSut().Handle(Command(category.Id, newImage: Image()), CancellationToken.None);

        await _fileStorage.DidNotReceive().DeleteAsync(StorageArea.CategoryImages, "categories/abc/old-image.webp", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Unexpected technical failure — connection-problem edge case (Category_UpdateFailed)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenUpdateAsyncThrowsUnexpectedly_ReturnsUpdateFailedFailure()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();
        _categories.UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        var result = await CreateSut().Handle(Command(category.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.UpdateFailed);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenUpdateAsyncThrowsUnexpectedlyAfterNewImageUpload_DeletesTheOrphanedUpload()
    {
        var category = SetUpExistingCategory();
        SetUpNoNameClash();
        SetUpSuccessfulImageUpload("categories/abc/new-image.png");
        _categories.UpdateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        await CreateSut().Handle(Command(category.Id, newImage: Image()), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.CategoryImages, "categories/abc/new-image.png", Arg.Any<CancellationToken>());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-8: Handle_WithValidCommand_ReturnsSuccessResult, Handle_WithValidCommand_PersistsTheRenamedRedescribedCategory,
//        Handle_WithValidCommand_ReturnsDtoReflectingTheNewDetails, Handle_WhenIsActiveIsFalse_PersistsAnInactiveCategory,
//        Handle_WhenIsActiveIsTrue_PersistsAnActiveCategory
// AC-9: Handle_WhenNameIsEmpty_ReturnsNameRequiredFailure, Handle_WhenNameIsEmpty_LeavesTheCategoryUnchanged
// AC-10: Handle_WhenRenamedToAnotherCategorysName_ReturnsAlreadyExistsFailure,
//         Handle_WhenRenamedToAnotherCategorysName_DoesNotPersistAnything
// AC-11: Handle_WhenCheckingUniqueness_ExcludesTheCategorysOwnId, Handle_WhenSavedWithNoChangesAtAll_Succeeds
// AC-13: Handle_WhenRemoveImageIsTrue_PersistsTheCategoryWithNoImage,
//         Handle_WhenRemoveImageIsTrue_DeletesThePreviousImageObjectAfterSaving,
//         Handle_WithANewImage_UploadsItToTheCategoryImagesArea, Handle_WithANewImage_PersistsTheCategoryWithTheUploadedKey,
//         Handle_WhenANewImageReplacesAnExistingOne_DeletesTheReplacedImageObject
// AC-23: Handle_WhenCategoryDoesNotExist_ReturnsNotFoundFailure (surfaced to the edit page as "category no longer exists")

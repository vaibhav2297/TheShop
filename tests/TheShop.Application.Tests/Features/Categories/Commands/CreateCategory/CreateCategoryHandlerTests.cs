using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.Commands.CreateCategory;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Commands.CreateCategory;

/// <summary>
/// Tests for <see cref="CreateCategoryHandler"/> — category creation orchestration: the RULE-2
/// uniqueness pre-check, <see cref="Category.Create"/> invariant translation, the optional image
/// upload with its orphaned-object compensation (Decision 5), and persistence
/// (Behavior 4).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class CreateCategoryHandlerTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private CreateCategoryHandler CreateSut() => new(_categories, _fileStorage);

    private static CreateCategoryCommand Command(
        string name = "Disposables",
        string? description = null,
        CategoryImageUpload? image = null,
        bool isActive = true) =>
        new(name, description, image, isActive);

    private void SetUpNoExistingCategory() =>
        _categories.ExistsByNormalizedNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);

    private void SetUpSuccessfulAdd() =>
        _categories.AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());

    private void SetUpSuccessfulImageUpload(string key = "categories/abc/image.png") =>
        _fileStorage.UploadAsync(
            StorageArea.CategoryImages, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(key);

    private static CategoryImageUpload Image() => new([1, 2, 3], "image.png", "image/png");

    // =========================================================================
    // Happy path (AC-6, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithValidNameOnly_ReturnsSuccessResult()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulAdd();

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithValidNameOnly_ReturnsDtoWithSuppliedNameAndDefaults()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulAdd();

        var result = await CreateSut().Handle(Command(name: "Disposables"), CancellationToken.None);

        result.Value.Name.Should().Be("Disposables");
        result.Value.Description.Should().BeNull();
        result.Value.ImageUrl.Should().BeNull();
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithValidCommand_PersistsTheCategory()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulAdd();

        await CreateSut().Handle(Command(), CancellationToken.None);

        await _categories.Received(1).AddAsync(
            Arg.Is<Category>(c => c.Name == "Disposables"), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenIsActiveIsFalse_PersistsAnInactiveCategory()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulAdd();

        var result = await CreateSut().Handle(Command(isActive: false), CancellationToken.None);

        result.Value.IsActive.Should().BeFalse();
    }

    // =========================================================================
    // RULE-2 uniqueness pre-check (AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenNameAlreadyExists_ReturnsAlreadyExistsFailure()
    {
        _categories.ExistsByNormalizedNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.AlreadyExists);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenNameAlreadyExists_DoesNotPersistTheCategory()
    {
        _categories.ExistsByNormalizedNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().Handle(Command(), CancellationToken.None);

        await _categories.DidNotReceive().AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Domain invariant translation — defence in depth behind the validator (RULE-1/RULE-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenNameIsEmpty_ReturnsNameRequiredFailure()
    {
        SetUpNoExistingCategory();

        var result = await CreateSut().Handle(Command(name: ""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.NameRequired);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenNameExceeds100Characters_ReturnsNameTooLongFailure()
    {
        SetUpNoExistingCategory();

        var result = await CreateSut().Handle(Command(name: new string('a', 101)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.NameTooLong);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenDescriptionExceeds250Characters_ReturnsDescriptionTooLongFailure()
    {
        SetUpNoExistingCategory();

        var result = await CreateSut().Handle(
            Command(description: new string('a', 251)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.DescriptionTooLong);
    }

    // =========================================================================
    // Optional image upload (Behavior 4, AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithAnImage_UploadsItToTheCategoryImagesArea()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulAdd();
        SetUpSuccessfulImageUpload();

        await CreateSut().Handle(Command(image: Image()), CancellationToken.None);

        await _fileStorage.Received(1).UploadAsync(
            StorageArea.CategoryImages, Arg.Any<Guid>(), Arg.Any<Stream>(), "image.png", "image/png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithAnImage_AttachesTheUploadedKeyToThePersistedCategory()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulAdd();
        SetUpSuccessfulImageUpload("categories/abc/image.png");

        await CreateSut().Handle(Command(image: Image()), CancellationToken.None);

        await _categories.Received(1).AddAsync(
            Arg.Is<Category>(c => c.ImagePath == "categories/abc/image.png"), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithoutAnImage_DoesNotCallFileStorageUpload()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulAdd();

        await CreateSut().Handle(Command(image: null), CancellationToken.None);

        await _fileStorage.DidNotReceive().UploadAsync(
            Arg.Any<StorageArea>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithoutAnImage_PersistedCategoryHasNoImageUrl()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulAdd();

        var result = await CreateSut().Handle(Command(image: null), CancellationToken.None);

        result.Value.ImageUrl.Should().BeNull();
    }

    // =========================================================================
    // Persistence failure after image upload — orphaned-object compensation (Decision 5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenAddAsyncFailsAfterImageUpload_DeletesTheOrphanedImage()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulImageUpload("categories/abc/image.png");
        _categories.AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(CategoryErrorKeys.AlreadyExists));

        await CreateSut().Handle(Command(image: Image()), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(
            StorageArea.CategoryImages, "categories/abc/image.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenAddAsyncFailsAfterImageUpload_ReturnsAddAsyncsFailureKey()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulImageUpload("categories/abc/image.png");
        _categories.AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(CategoryErrorKeys.AlreadyExists));

        var result = await CreateSut().Handle(Command(image: Image()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.AlreadyExists);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenAddAsyncFailsWithoutAnImage_DoesNotAttemptToDeleteAnything()
    {
        SetUpNoExistingCategory();
        _categories.AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(CategoryErrorKeys.AlreadyExists));

        await CreateSut().Handle(Command(image: null), CancellationToken.None);

        await _fileStorage.DidNotReceive().DeleteAsync(
            Arg.Any<StorageArea>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Unexpected technical failure — connection-problem edge case (Category_CreateFailed)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenAddAsyncThrowsUnexpectedly_ReturnsCreateFailedFailure()
    {
        SetUpNoExistingCategory();
        _categories.AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.CreateFailed);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenAddAsyncThrowsUnexpectedlyAfterImageUpload_DeletesTheOrphanedImage()
    {
        SetUpNoExistingCategory();
        SetUpSuccessfulImageUpload("categories/abc/image.png");
        _categories.AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        await CreateSut().Handle(Command(image: Image()), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(
            StorageArea.CategoryImages, "categories/abc/image.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenTheCompensatingDeleteAlsoThrows_StillReturnsCreateFailedFailure()
    {
        // Best-effort compensation (Decision 5): a failed cleanup must not mask the original failure.
        SetUpNoExistingCategory();
        SetUpSuccessfulImageUpload("categories/abc/image.png");
        _categories.AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));
        _fileStorage.DeleteAsync(StorageArea.CategoryImages, "categories/abc/image.png", Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("storage also unreachable"));

        var result = await CreateSut().Handle(Command(image: Image()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(CategoryErrorKeys.CreateFailed);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: Handle_WithValidNameOnly_ReturnsSuccessResult, Handle_WithValidCommand_PersistsTheCategory,
//        Handle_WithAnImage_UploadsItToTheCategoryImagesArea, Handle_WithAnImage_AttachesTheUploadedKeyToThePersistedCategory
// AC-7: Handle_WithValidNameOnly_ReturnsDtoWithSuppliedNameAndDefaults, Handle_WhenIsActiveIsFalse_PersistsAnInactiveCategory,
//        Handle_WithoutAnImage_DoesNotCallFileStorageUpload, Handle_WithoutAnImage_PersistedCategoryHasNoImageUrl
// AC-10: Handle_WhenNameAlreadyExists_ReturnsAlreadyExistsFailure, Handle_WhenNameAlreadyExists_DoesNotPersistTheCategory

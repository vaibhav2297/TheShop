using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Brands;
using TheShop.Application.Features.Brands.Commands.CreateBrand;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.CreateBrand;

/// <summary>
/// Tests for <see cref="CreateBrandHandler"/> — brand creation orchestration: the RULE-2
/// uniqueness pre-check, <see cref="Brand.Create"/> invariant translation, the optional logo
/// upload with its orphaned-object compensation (Decision 5), and persistence
/// (Behavior 1, Behavior 3).
/// <see href=".specs/add-brand/spec.md"/>
/// </summary>
public class CreateBrandHandlerTests
{
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private CreateBrandHandler CreateSut() => new(_brands, _fileStorage);

    private static CreateBrandCommand Command(
        string name = "Elf Bar",
        string? description = null,
        BrandLogoUpload? logo = null,
        bool isActive = true) =>
        new(name, description, logo, isActive);

    private void SetUpNoExistingBrand() =>
        _brands.ExistsByNormalizedNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);

    private void SetUpSuccessfulAdd() =>
        _brands.AddAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());

    private void SetUpSuccessfulLogoUpload(string key = "brands/abc/logo.png") =>
        _fileStorage.UploadAsync(
            StorageArea.BrandLogos, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(key);

    private static BrandLogoUpload Logo() => new([1, 2, 3], "logo.png", "image/png");

    // =========================================================================
    // Happy path (AC-1, AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WithValidNameOnly_ReturnsSuccessResult()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulAdd();

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WithValidNameOnly_ReturnsDtoWithSuppliedNameAndDefaults()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulAdd();

        var result = await CreateSut().Handle(Command(name: "Elf Bar"), CancellationToken.None);

        result.Value.Name.Should().Be("Elf Bar");
        result.Value.Description.Should().BeNull();
        result.Value.LogoUrl.Should().BeNull();
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WithValidCommand_PersistsTheBrand()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulAdd();

        await CreateSut().Handle(Command(), CancellationToken.None);

        await _brands.Received(1).AddAsync(
            Arg.Is<Brand>(b => b.Name == "Elf Bar"), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenIsActiveIsFalse_PersistsAnInactiveBrand()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulAdd();

        var result = await CreateSut().Handle(Command(isActive: false), CancellationToken.None);

        result.Value.IsActive.Should().BeFalse();
    }

    // =========================================================================
    // RULE-2 uniqueness pre-check (AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenNameAlreadyExists_ReturnsAlreadyExistsFailure()
    {
        _brands.ExistsByNormalizedNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.AlreadyExists);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenNameAlreadyExists_DoesNotPersistTheBrand()
    {
        _brands.ExistsByNormalizedNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().Handle(Command(), CancellationToken.None);

        await _brands.DidNotReceive().AddAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Domain invariant translation — defence in depth behind the validator (RULE-1/RULE-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenNameIsEmpty_ReturnsNameRequiredFailure()
    {
        SetUpNoExistingBrand();

        var result = await CreateSut().Handle(Command(name: ""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.NameRequired);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenNameExceeds100Characters_ReturnsNameTooLongFailure()
    {
        SetUpNoExistingBrand();

        var result = await CreateSut().Handle(Command(name: new string('a', 101)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.NameTooLong);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenDescriptionExceeds250Characters_ReturnsDescriptionTooLongFailure()
    {
        SetUpNoExistingBrand();

        var result = await CreateSut().Handle(
            Command(description: new string('a', 251)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.DescriptionTooLong);
    }

    // =========================================================================
    // Optional logo upload (Behavior 2, AC-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WithALogo_UploadsItToTheBrandLogosArea()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulAdd();
        SetUpSuccessfulLogoUpload();

        await CreateSut().Handle(Command(logo: Logo()), CancellationToken.None);

        await _fileStorage.Received(1).UploadAsync(
            StorageArea.BrandLogos, Arg.Any<Guid>(), Arg.Any<Stream>(), "logo.png", "image/png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WithALogo_AttachesTheUploadedKeyToThePersistedBrand()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulAdd();
        SetUpSuccessfulLogoUpload("brands/abc/logo.png");

        await CreateSut().Handle(Command(logo: Logo()), CancellationToken.None);

        await _brands.Received(1).AddAsync(
            Arg.Is<Brand>(b => b.LogoPath == "brands/abc/logo.png"), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WithoutALogo_DoesNotCallFileStorageUpload()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulAdd();

        await CreateSut().Handle(Command(logo: null), CancellationToken.None);

        await _fileStorage.DidNotReceive().UploadAsync(
            Arg.Any<StorageArea>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WithoutALogo_PersistedBrandHasNoLogoUrl()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulAdd();

        var result = await CreateSut().Handle(Command(logo: null), CancellationToken.None);

        result.Value.LogoUrl.Should().BeNull();
    }

    // =========================================================================
    // Persistence failure after logo upload — orphaned-object compensation (Decision 5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenAddAsyncFailsAfterLogoUpload_DeletesTheOrphanedLogo()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulLogoUpload("brands/abc/logo.png");
        _brands.AddAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(BrandErrorKeys.AlreadyExists));

        await CreateSut().Handle(Command(logo: Logo()), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(
            StorageArea.BrandLogos, "brands/abc/logo.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenAddAsyncFailsAfterLogoUpload_ReturnsAddAsyncsFailureKey()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulLogoUpload("brands/abc/logo.png");
        _brands.AddAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(BrandErrorKeys.AlreadyExists));

        var result = await CreateSut().Handle(Command(logo: Logo()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.AlreadyExists);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenAddAsyncFailsWithoutALogo_DoesNotAttemptToDeleteAnything()
    {
        SetUpNoExistingBrand();
        _brands.AddAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(BrandErrorKeys.AlreadyExists));

        await CreateSut().Handle(Command(logo: null), CancellationToken.None);

        await _fileStorage.DidNotReceive().DeleteAsync(
            Arg.Any<StorageArea>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Unexpected technical failure — connection-problem edge case (Brand_CreateFailed)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenAddAsyncThrowsUnexpectedly_ReturnsCreateFailedFailure()
    {
        SetUpNoExistingBrand();
        _brands.AddAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.CreateFailed);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenAddAsyncThrowsUnexpectedlyAfterLogoUpload_DeletesTheOrphanedLogo()
    {
        SetUpNoExistingBrand();
        SetUpSuccessfulLogoUpload("brands/abc/logo.png");
        _brands.AddAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        await CreateSut().Handle(Command(logo: Logo()), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(
            StorageArea.BrandLogos, "brands/abc/logo.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task Handle_WhenTheCompensatingDeleteAlsoThrows_StillReturnsCreateFailedFailure()
    {
        // Best-effort compensation (Decision 5): a failed cleanup must not mask the original failure.
        SetUpNoExistingBrand();
        SetUpSuccessfulLogoUpload("brands/abc/logo.png");
        _brands.AddAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));
        _fileStorage.DeleteAsync(StorageArea.BrandLogos, "brands/abc/logo.png", Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("storage also unreachable"));

        var result = await CreateSut().Handle(Command(logo: Logo()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.CreateFailed);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Handle_WithValidNameOnly_ReturnsSuccessResult, Handle_WithValidCommand_PersistsTheBrand
// AC-3: Handle_WhenNameAlreadyExists_ReturnsAlreadyExistsFailure,
//        Handle_WhenNameAlreadyExists_DoesNotPersistTheBrand
// AC-4: Handle_WithALogo_UploadsItToTheBrandLogosArea,
//        Handle_WithALogo_AttachesTheUploadedKeyToThePersistedBrand
// AC-6: Handle_WithValidNameOnly_ReturnsDtoWithSuppliedNameAndDefaults,
//        Handle_WithoutALogo_DoesNotCallFileStorageUpload, Handle_WithoutALogo_PersistedBrandHasNoLogoUrl
// AC-7: Handle_WhenIsActiveIsFalse_PersistsAnInactiveBrand

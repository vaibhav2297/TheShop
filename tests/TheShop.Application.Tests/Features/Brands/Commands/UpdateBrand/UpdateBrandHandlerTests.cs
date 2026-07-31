using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Brands;
using TheShop.Application.Features.Brands.Commands.UpdateBrand;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.UpdateBrand;

/// <summary>
/// Tests for <see cref="UpdateBrandHandler"/> — the RULE-2 self-exclusive uniqueness pre-check
/// (AC-9), <c>Brand</c> mutation orchestration (Rename/ChangeDescription/Activate/Deactivate,
/// AC-6/AC-12), the logo remove/replace flows with their orphaned-object compensation
/// (RULE-11, AC-10, AC-11), and persistence-failure handling that leaves the brand unchanged
/// (Behavior 4).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class UpdateBrandHandlerTests
{
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private UpdateBrandHandler CreateSut() => new(_brands, _fileStorage);

    private static UpdateBrandCommand Command(
        Guid id,
        string name = "Elf Bar",
        string? description = null,
        bool isActive = true,
        BrandLogoUpload? newLogo = null,
        bool removeLogo = false) =>
        new(id, name, description, isActive, newLogo, removeLogo);

    private static BrandLogoUpload Logo() => new([1, 2, 3], "logo.png", "image/png");

    private Brand SetUpExistingBrand(
        string name = "Elf Bar", string? description = null, bool isActive = true, string? logoPath = null)
    {
        var brand = Brand.Create(name, description, isActive);
        if (logoPath is not null)
            brand.AttachLogo(logoPath);

        _brands.GetByIdAsync(brand.Id, Arg.Any<CancellationToken>()).Returns(brand);
        return brand;
    }

    private void SetUpNoNameClash() =>
        _brands.ExistsByNormalizedNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);

    private void SetUpSuccessfulUpdate() =>
        _brands.UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());

    private void SetUpSuccessfulLogoUpload(string key = "brands/abc/new-logo.png") =>
        _fileStorage.UploadAsync(
            StorageArea.BrandLogos, Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(key);

    // =========================================================================
    // Happy path — name, description, status all change (AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithValidCommand_ReturnsSuccessResult()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        var result = await CreateSut().Handle(Command(brand.Id, name: "Lost Mary"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithValidCommand_PersistsTheRenamedRedescribedBrand()
    {
        var brand = SetUpExistingBrand(name: "Elf Bar", description: "Old description.");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(
            Command(brand.Id, name: "Lost Mary", description: "New description."), CancellationToken.None);

        await _brands.Received(1).UpdateAsync(
            Arg.Is<Brand>(b => b.Name == "Lost Mary" && b.Description == "New description."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithValidCommand_ReturnsDtoReflectingTheNewDetails()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        var result = await CreateSut().Handle(Command(brand.Id, name: "Lost Mary"), CancellationToken.None);

        result.Value.Id.Should().Be(brand.Id);
        result.Value.Name.Should().Be("Lost Mary");
    }

    // =========================================================================
    // Brand no longer exists (edge case: removed by another staff member, AC-27)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenBrandDoesNotExist_ReturnsNotFoundFailure()
    {
        _brands.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Brand?)null);

        var result = await CreateSut().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.NotFound);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenBrandDoesNotExist_DoesNotAttemptToPersistAnything()
    {
        _brands.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Brand?)null);

        await CreateSut().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await _brands.DidNotReceive().UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // RULE-2 self-exclusive uniqueness check (AC-8, AC-9)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenRenamedToAnotherBrandsName_ReturnsAlreadyExistsFailure()
    {
        var brand = SetUpExistingBrand();
        _brands.ExistsByNormalizedNameAsync(Arg.Any<string>(), brand.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateSut().Handle(Command(brand.Id, name: "Lost Mary"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.AlreadyExists);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenRenamedToAnotherBrandsName_DoesNotPersistAnything()
    {
        var brand = SetUpExistingBrand();
        _brands.ExistsByNormalizedNameAsync(Arg.Any<string>(), brand.Id, Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().Handle(Command(brand.Id, name: "Lost Mary"), CancellationToken.None);

        await _brands.DidNotReceive().UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenCheckingUniqueness_ExcludesTheBrandsOwnId()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(brand.Id, name: "Lost Mary"), CancellationToken.None);

        await _brands.Received(1).ExistsByNormalizedNameAsync("Lost Mary", brand.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenSavedWithNoChangesAtAll_Succeeds()
    {
        // AC-9: excludeBrandId means the brand's own current name never collides with itself.
        var brand = SetUpExistingBrand(name: "Elf Bar", description: "A vape brand.", isActive: true);
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        var result = await CreateSut().Handle(
            Command(brand.Id, name: "Elf Bar", description: "A vape brand.", isActive: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        brand.Name.Should().Be("Elf Bar");
    }

    // =========================================================================
    // Domain invariant translation — defence in depth behind the validator (RULE-1/RULE-3, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenNameIsEmpty_ReturnsNameRequiredFailure()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();

        var result = await CreateSut().Handle(Command(brand.Id, name: ""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.NameRequired);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenNameIsEmpty_LeavesTheBrandUnchanged()
    {
        var brand = SetUpExistingBrand(name: "Elf Bar");
        SetUpNoNameClash();

        await CreateSut().Handle(Command(brand.Id, name: ""), CancellationToken.None);

        brand.Name.Should().Be("Elf Bar");
        await _brands.DidNotReceive().UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenDescriptionExceeds250Characters_ReturnsDescriptionTooLongFailure()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();

        var result = await CreateSut().Handle(
            Command(brand.Id, description: new string('a', 251)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.DescriptionTooLong);
    }

    // =========================================================================
    // Status change (AC-12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenIsActiveIsFalse_PersistsAnInactiveBrand()
    {
        var brand = SetUpExistingBrand(isActive: true);
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(brand.Id, isActive: false), CancellationToken.None);

        await _brands.Received(1).UpdateAsync(Arg.Is<Brand>(b => !b.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenIsActiveIsTrue_PersistsAnActiveBrand()
    {
        var brand = SetUpExistingBrand(isActive: false);
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(brand.Id, isActive: true), CancellationToken.None);

        await _brands.Received(1).UpdateAsync(Arg.Is<Brand>(b => b.IsActive), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Logo removal — discards the outgoing object (RULE-11, AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenRemoveLogoIsTrue_PersistsTheBrandWithNoLogo()
    {
        var brand = SetUpExistingBrand(logoPath: "brands/abc/logo.webp");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(brand.Id, removeLogo: true), CancellationToken.None);

        await _brands.Received(1).UpdateAsync(Arg.Is<Brand>(b => b.LogoPath == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenRemoveLogoIsTrue_DeletesThePreviousLogoObjectAfterSaving()
    {
        var brand = SetUpExistingBrand(logoPath: "brands/abc/logo.webp");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(brand.Id, removeLogo: true), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.BrandLogos, "brands/abc/logo.webp", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenRemoveLogoIsTrueButNoLogoExisted_DoesNotAttemptToDeleteAnything()
    {
        var brand = SetUpExistingBrand(logoPath: null);
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(brand.Id, removeLogo: true), CancellationToken.None);

        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<StorageArea>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Logo replacement — new upload attached, old object discarded (RULE-11, AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithANewLogo_UploadsItToTheBrandLogosArea()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();
        SetUpSuccessfulLogoUpload();

        await CreateSut().Handle(Command(brand.Id, newLogo: Logo()), CancellationToken.None);

        await _fileStorage.Received(1).UploadAsync(
            StorageArea.BrandLogos, brand.Id, Arg.Any<Stream>(), "logo.png", "image/png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithANewLogo_PersistsTheBrandWithTheUploadedKey()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();
        SetUpSuccessfulLogoUpload("brands/abc/new-logo.png");

        await CreateSut().Handle(Command(brand.Id, newLogo: Logo()), CancellationToken.None);

        await _brands.Received(1).UpdateAsync(
            Arg.Is<Brand>(b => b.LogoPath == "brands/abc/new-logo.png"), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenANewLogoReplacesAnExistingOne_DeletesTheReplacedLogoObject()
    {
        var brand = SetUpExistingBrand(logoPath: "brands/abc/old-logo.webp");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();
        SetUpSuccessfulLogoUpload("brands/abc/new-logo.png");

        await CreateSut().Handle(Command(brand.Id, newLogo: Logo()), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.BrandLogos, "brands/abc/old-logo.webp", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithNoLogoChangeRequested_DoesNotCallFileStorageAtAll()
    {
        var brand = SetUpExistingBrand(logoPath: "brands/abc/logo.webp");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(brand.Id, newLogo: null, removeLogo: false), CancellationToken.None);

        await _fileStorage.DidNotReceive().UploadAsync(
            Arg.Any<StorageArea>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<StorageArea>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenRemoveLogoAndNewLogoAreBothSupplied_RemoveLogoTakesPrecedence()
    {
        var brand = SetUpExistingBrand(logoPath: "brands/abc/logo.webp");
        SetUpNoNameClash();
        SetUpSuccessfulUpdate();

        await CreateSut().Handle(Command(brand.Id, newLogo: Logo(), removeLogo: true), CancellationToken.None);

        await _fileStorage.DidNotReceive().UploadAsync(
            Arg.Any<StorageArea>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _brands.Received(1).UpdateAsync(Arg.Is<Brand>(b => b.LogoPath == null), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Persistence failure after a new logo upload — orphaned-object compensation
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenUpdateAsyncFailsAfterNewLogoUpload_DeletesTheOrphanedUpload()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();
        SetUpSuccessfulLogoUpload("brands/abc/new-logo.png");
        _brands.UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(BrandErrorKeys.AlreadyExists));

        await CreateSut().Handle(Command(brand.Id, newLogo: Logo()), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.BrandLogos, "brands/abc/new-logo.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenUpdateAsyncFailsAfterNewLogoUpload_ReturnsUpdateAsyncsFailureKey()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();
        SetUpSuccessfulLogoUpload("brands/abc/new-logo.png");
        _brands.UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(BrandErrorKeys.AlreadyExists));

        var result = await CreateSut().Handle(Command(brand.Id, newLogo: Logo()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.AlreadyExists);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenUpdateAsyncFailsAfterNewLogoUpload_DoesNotDeleteTheStillCurrentLogo()
    {
        var brand = SetUpExistingBrand(logoPath: "brands/abc/old-logo.webp");
        SetUpNoNameClash();
        SetUpSuccessfulLogoUpload("brands/abc/new-logo.png");
        _brands.UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Returns(Result.Fail(BrandErrorKeys.AlreadyExists));

        await CreateSut().Handle(Command(brand.Id, newLogo: Logo()), CancellationToken.None);

        await _fileStorage.DidNotReceive().DeleteAsync(StorageArea.BrandLogos, "brands/abc/old-logo.webp", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Unexpected technical failure — connection-problem edge case (Brand_UpdateFailed)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenUpdateAsyncThrowsUnexpectedly_ReturnsUpdateFailedFailure()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();
        _brands.UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        var result = await CreateSut().Handle(Command(brand.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.UpdateFailed);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenUpdateAsyncThrowsUnexpectedlyAfterNewLogoUpload_DeletesTheOrphanedUpload()
    {
        var brand = SetUpExistingBrand();
        SetUpNoNameClash();
        SetUpSuccessfulLogoUpload("brands/abc/new-logo.png");
        _brands.UpdateAsync(Arg.Any<Brand>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        await CreateSut().Handle(Command(brand.Id, newLogo: Logo()), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.BrandLogos, "brands/abc/new-logo.png", Arg.Any<CancellationToken>());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: Handle_WithValidCommand_ReturnsSuccessResult, Handle_WithValidCommand_PersistsTheRenamedRedescribedBrand,
//        Handle_WithValidCommand_ReturnsDtoReflectingTheNewDetails
// AC-7: Handle_WhenNameIsEmpty_ReturnsNameRequiredFailure, Handle_WhenNameIsEmpty_LeavesTheBrandUnchanged
// AC-8: Handle_WhenRenamedToAnotherBrandsName_ReturnsAlreadyExistsFailure,
//        Handle_WhenRenamedToAnotherBrandsName_DoesNotPersistAnything
// AC-9: Handle_WhenCheckingUniqueness_ExcludesTheBrandsOwnId, Handle_WhenSavedWithNoChangesAtAll_Succeeds
// AC-10: Handle_WhenRemoveLogoIsTrue_PersistsTheBrandWithNoLogo, Handle_WhenRemoveLogoIsTrue_DeletesThePreviousLogoObjectAfterSaving,
//         Handle_WithANewLogo_UploadsItToTheBrandLogosArea, Handle_WithANewLogo_PersistsTheBrandWithTheUploadedKey,
//         Handle_WhenANewLogoReplacesAnExistingOne_DeletesTheReplacedLogoObject
// AC-11: (RULE-4 covered by the validator; the handler's own contribution is not corrupting the
//         existing logo when persistence fails — Handle_WhenUpdateAsyncFailsAfterNewLogoUpload_DoesNotDeleteTheStillCurrentLogo)
// AC-12: Handle_WhenIsActiveIsFalse_PersistsAnInactiveBrand, Handle_WhenIsActiveIsTrue_PersistsAnActiveBrand
// AC-27: Handle_WhenBrandDoesNotExist_ReturnsNotFoundFailure (surfaced to the edit page as "brand no longer exists")

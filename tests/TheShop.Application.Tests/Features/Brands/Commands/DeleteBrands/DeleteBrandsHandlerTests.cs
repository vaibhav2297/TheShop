using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Brands;
using TheShop.Application.Features.Brands.Commands.DeleteBrands;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Roles;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Commands.DeleteBrands;

/// <summary>
/// Tests for <see cref="DeleteBrandsHandler"/> — delegates the atomic partial-success delete to
/// <see cref="IBrandRepository.DeleteManyAsync"/> (RULE-6/RULE-13; single-row AC-13, bulk AC-24/
/// AC-25), best-effort disposes the deleted brands' logo objects afterwards (RULE-11), and
/// translates the RPC's access-denied exception into <see cref="RbacErrorKeys.AccessDenied"/>.
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class DeleteBrandsHandlerTests
{
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private DeleteBrandsHandler CreateSut() => new(_brands, _fileStorage);

    private void SetUpDeletionOutcome(BrandDeletionOutcomeDto outcome, IReadOnlyList<string> deletedLogoPaths) =>
        _brands.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
               .Returns((outcome, deletedLogoPaths));

    // =========================================================================
    // Single brand, no product references it (AC-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenTheBrandIsDeletable_ReturnsSuccessWithDeletedCountOfOne()
    {
        var id = Guid.NewGuid();
        SetUpDeletionOutcome(new BrandDeletionOutcomeDto(1, []), []);

        var result = await CreateSut().Handle(new DeleteBrandsCommand([id]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeletedCount.Should().Be(1);
        result.Value.Blocked.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenTheDeletedBrandHadALogo_DeletesTheLogoObject()
    {
        SetUpDeletionOutcome(new BrandDeletionOutcomeDto(1, []), ["brands/abc/logo.webp"]);

        await CreateSut().Handle(new DeleteBrandsCommand([Guid.NewGuid()]), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.BrandLogos, "brands/abc/logo.webp", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenTheDeletedBrandHadNoLogo_DoesNotAttemptToDeleteAnything()
    {
        SetUpDeletionOutcome(new BrandDeletionOutcomeDto(1, []), []);

        await CreateSut().Handle(new DeleteBrandsCommand([Guid.NewGuid()]), CancellationToken.None);

        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<StorageArea>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Single brand, referenced by a product (FR-13, RULE-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenTheBrandIsInUse_ReturnsZeroDeletedAndTheBlockedBrandWithItsProductCount()
    {
        var id = Guid.NewGuid();
        SetUpDeletionOutcome(new BrandDeletionOutcomeDto(0, [new BlockedBrandDto(id, "Elf Bar", 3)]), []);

        var result = await CreateSut().Handle(new DeleteBrandsCommand([id]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeletedCount.Should().Be(0);
        result.Value.Blocked.Should().ContainSingle(b => b.Id == id && b.Name == "Elf Bar" && b.ProductCount == 3);
    }

    // =========================================================================
    // Bulk deletion — mixed outcome (RULE-13, AC-24)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithAMixOfDeletableAndInUseBrands_ReturnsBothTheDeletedCountAndTheBlockedList()
    {
        var blocked = new[]
        {
            new BlockedBrandDto(Guid.NewGuid(), "Elf Bar", 2),
            new BlockedBrandDto(Guid.NewGuid(), "Lost Mary", 1),
        };
        SetUpDeletionOutcome(new BrandDeletionOutcomeDto(3, blocked), ["a/1.webp", "a/2.webp", "a/3.webp"]);

        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var result = await CreateSut().Handle(new DeleteBrandsCommand(ids), CancellationToken.None);

        result.Value.DeletedCount.Should().Be(3);
        result.Value.Blocked.Should().BeEquivalentTo(blocked);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithAMixOfDeletableAndInUseBrands_DeletesOnlyTheDeletedOnesLogos()
    {
        SetUpDeletionOutcome(
            new BrandDeletionOutcomeDto(2, [new BlockedBrandDto(Guid.NewGuid(), "Elf Bar", 1)]),
            ["a/1.webp", "a/2.webp"]);

        await CreateSut().Handle(new DeleteBrandsCommand([Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]), CancellationToken.None);

        await _fileStorage.Received(1).DeleteAsync(StorageArea.BrandLogos, "a/1.webp", Arg.Any<CancellationToken>());
        await _fileStorage.Received(1).DeleteAsync(StorageArea.BrandLogos, "a/2.webp", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Bulk deletion — every selected brand is in use (RULE-13, AC-25)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenEverySelectedBrandIsInUse_ReturnsZeroDeletedAndEveryBrandBlocked()
    {
        var blocked = new[]
        {
            new BlockedBrandDto(Guid.NewGuid(), "Elf Bar", 2),
            new BlockedBrandDto(Guid.NewGuid(), "Lost Mary", 5),
        };
        SetUpDeletionOutcome(new BrandDeletionOutcomeDto(0, blocked), []);

        var result = await CreateSut().Handle(
            new DeleteBrandsCommand(blocked.Select(b => b.Id).ToList()), CancellationToken.None);

        result.Value.DeletedCount.Should().Be(0);
        result.Value.Blocked.Should().HaveCount(2);
    }

    // =========================================================================
    // Best-effort logo cleanup — a storage failure never fails the request (RULE-11 compensation)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenDeletingTheLogoObjectThrows_StillReturnsSuccess()
    {
        SetUpDeletionOutcome(new BrandDeletionOutcomeDto(1, []), ["a/1.webp"]);
        _fileStorage.DeleteAsync(StorageArea.BrandLogos, "a/1.webp", Arg.Any<CancellationToken>())
                    .Throws(new InvalidOperationException("storage unreachable"));

        var result = await CreateSut().Handle(new DeleteBrandsCommand([Guid.NewGuid()]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // =========================================================================
    // Access-denied translation — the RPC re-checks brands.delete itself (RULE-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenTheRepositoryThrowsAnAccessDeniedException_ReturnsRbacAccessDeniedFailure()
    {
        _brands.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("access denied"));

        var result = await CreateSut().Handle(new DeleteBrandsCommand([Guid.NewGuid()]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RbacErrorKeys.AccessDenied);
    }

    // =========================================================================
    // Unexpected technical failure (edge case: connection problem)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenTheRepositoryThrowsUnexpectedly_ReturnsDeleteFailedFailure()
    {
        _brands.DeleteManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
               .Throws(new InvalidOperationException("connection lost"));

        var result = await CreateSut().Handle(new DeleteBrandsCommand([Guid.NewGuid()]), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(BrandErrorKeys.DeleteFailed);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-13: Handle_WhenTheBrandIsDeletable_ReturnsSuccessWithDeletedCountOfOne,
//         Handle_WhenTheDeletedBrandHadALogo_DeletesTheLogoObject
// AC-15: Handle_WhenTheBrandIsInUse_ReturnsZeroDeletedAndTheBlockedBrandWithItsProductCount
// AC-24: Handle_WithAMixOfDeletableAndInUseBrands_ReturnsBothTheDeletedCountAndTheBlockedList,
//         Handle_WithAMixOfDeletableAndInUseBrands_DeletesOnlyTheDeletedOnesLogos
// AC-25: Handle_WhenEverySelectedBrandIsInUse_ReturnsZeroDeletedAndEveryBrandBlocked
// AC-26: Handle_WhenTheRepositoryThrowsAnAccessDeniedException_ReturnsRbacAccessDeniedFailure

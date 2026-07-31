using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Queries.GetBrandsPage;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Brands.Queries.GetBrandsPage;

/// <summary>
/// Tests for <see cref="GetBrandsPageHandler"/> — normalizes the requested pagination to the fixed
/// page size (spec constraint) and delegates the filtered/sorted/paged read, including the
/// authoritative per-brand product counts, to <see cref="IBrandRepository"/> (Behaviors 1-3,
/// AC-1 through AC-5).
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class GetBrandsPageHandlerTests
{
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();

    private GetBrandsPageHandler CreateSut() => new(_brands);

    private static BrandListItemDto Item(string name = "Elf Bar") =>
        new(Guid.NewGuid(), name, null, "https://example.com/logo.webp", true, 0);

    // =========================================================================
    // Happy path — delegates and wraps the repository's page (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_Always_ReturnsSuccessResultWrappingTheRepositoryPage()
    {
        var page = new PagedResult<BrandListItemDto>([Item()], 1, 10, 1);
        _brands.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<BrandStatusFilter?>(), Arg.Any<BrandSortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>()).Returns(page);

        var result = await CreateSut().Handle(
            new GetBrandsPageQuery(null, null, BrandSortOption.NameAToZ, new PaginationRequest(1, 10)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(page);
    }

    // =========================================================================
    // Passes search/status/sort through unchanged (AC-3, AC-4, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithSearchStatusAndSort_PassesThemToTheRepositoryUnchanged()
    {
        _brands.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<BrandStatusFilter?>(), Arg.Any<BrandSortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<BrandListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(
            new GetBrandsPageQuery("aur", BrandStatusFilter.Inactive, BrandSortOption.NameZToA, new PaginationRequest(1, 10)),
            CancellationToken.None);

        await _brands.Received(1).GetPageAsync(
            "aur", BrandStatusFilter.Inactive, BrandSortOption.NameZToA, Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WithNoStatusFilter_PassesNullThroughAsEveryStatus()
    {
        _brands.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<BrandStatusFilter?>(), Arg.Any<BrandSortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<BrandListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(
            new GetBrandsPageQuery(null, null, BrandSortOption.NameAToZ, new PaginationRequest(1, 10)),
            CancellationToken.None);

        await _brands.Received(1).GetPageAsync(
            null, null, Arg.Any<BrandSortOption>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Pagination normalization to the fixed page size (spec constraint, AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenPageSizeExceedsTheFixedLimit_ClampsItToTen()
    {
        _brands.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<BrandStatusFilter?>(), Arg.Any<BrandSortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<BrandListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(
            new GetBrandsPageQuery(null, null, BrandSortOption.NameAToZ, new PaginationRequest(1, 50)),
            CancellationToken.None);

        await _brands.Received(1).GetPageAsync(
            Arg.Any<string?>(), Arg.Any<BrandStatusFilter?>(), Arg.Any<BrandSortOption>(),
            Arg.Is<PaginationRequest>(p => p.PageSize == 10), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Handle_WhenRequestedPageIsBelowOne_ClampsToPageOne()
    {
        _brands.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<BrandStatusFilter?>(), Arg.Any<BrandSortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<BrandListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(
            new GetBrandsPageQuery(null, null, BrandSortOption.NameAToZ, new PaginationRequest(0, 10)),
            CancellationToken.None);

        await _brands.Received(1).GetPageAsync(
            Arg.Any<string?>(), Arg.Any<BrandStatusFilter?>(), Arg.Any<BrandSortOption>(),
            Arg.Is<PaginationRequest>(p => p.Page == 1), Arg.Any<CancellationToken>());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Handle_Always_ReturnsSuccessResultWrappingTheRepositoryPage
// AC-2: Handle_WhenPageSizeExceedsTheFixedLimit_ClampsItToTen, Handle_WhenRequestedPageIsBelowOne_ClampsToPageOne
// AC-3: Handle_WithSearchStatusAndSort_PassesThemToTheRepositoryUnchanged
// AC-4: Handle_WithSearchStatusAndSort_PassesThemToTheRepositoryUnchanged, Handle_WithNoStatusFilter_PassesNullThroughAsEveryStatus
// AC-5: Handle_WithSearchStatusAndSort_PassesThemToTheRepositoryUnchanged

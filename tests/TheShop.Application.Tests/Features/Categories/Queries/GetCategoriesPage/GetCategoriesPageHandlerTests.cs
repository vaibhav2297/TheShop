using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Queries.GetCategoriesPage;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Categories.Queries.GetCategoriesPage;

/// <summary>
/// Tests for <see cref="GetCategoriesPageHandler"/> — normalizes the requested pagination to the
/// fixed page size (spec constraint) and delegates the filtered/sorted/paged read, including the
/// authoritative per-category product counts, to <see cref="ICategoryRepository"/>
/// (AC-1 through AC-5, AC-32).
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class GetCategoriesPageHandlerTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();

    private GetCategoriesPageHandler CreateSut() => new(_categories);

    private static CategoryListItemDto Item(string name = "Disposables") =>
        new(Guid.NewGuid(), name, null, "https://example.com/image.webp", true, 0);

    // =========================================================================
    // Happy path — delegates and wraps the repository's page (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_Always_ReturnsSuccessResultWrappingTheRepositoryPage()
    {
        var page = new PagedResult<CategoryListItemDto>([Item()], 1, 10, 1);
        _categories.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<CategoryStatusFilter?>(), Arg.Any<CategorySortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>()).Returns(page);

        var result = await CreateSut().Handle(
            new GetCategoriesPageQuery(null, null, CategorySortOption.NameAToZ, new PaginationRequest(1, 10)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(page);
    }

    // =========================================================================
    // Passes search/status/sort through unchanged (AC-3, AC-4, AC-5, AC-32)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithSearchStatusAndSort_PassesThemToTheRepositoryUnchanged()
    {
        _categories.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<CategoryStatusFilter?>(), Arg.Any<CategorySortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CategoryListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(
            new GetCategoriesPageQuery("dis", CategoryStatusFilter.Inactive, CategorySortOption.NameZToA, new PaginationRequest(1, 10)),
            CancellationToken.None);

        await _categories.Received(1).GetPageAsync(
            "dis", CategoryStatusFilter.Inactive, CategorySortOption.NameZToA, Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithNoStatusFilter_PassesNullThroughAsEveryStatus()
    {
        _categories.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<CategoryStatusFilter?>(), Arg.Any<CategorySortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CategoryListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(
            new GetCategoriesPageQuery(null, null, CategorySortOption.NameAToZ, new PaginationRequest(1, 10)),
            CancellationToken.None);

        await _categories.Received(1).GetPageAsync(
            null, null, Arg.Any<CategorySortOption>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(CategorySortOption.NewestFirst)]
    [InlineData(CategorySortOption.OldestFirst)]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WithNewestOrOldestSort_PassesItToTheRepositoryUnchanged(CategorySortOption sort)
    {
        _categories.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<CategoryStatusFilter?>(), Arg.Any<CategorySortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CategoryListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(
            new GetCategoriesPageQuery(null, null, sort, new PaginationRequest(1, 10)),
            CancellationToken.None);

        await _categories.Received(1).GetPageAsync(
            null, null, sort, Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Pagination normalization to the fixed page size (spec constraint, AC-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenPageSizeExceedsTheFixedLimit_ClampsItToTen()
    {
        _categories.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<CategoryStatusFilter?>(), Arg.Any<CategorySortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CategoryListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(
            new GetCategoriesPageQuery(null, null, CategorySortOption.NameAToZ, new PaginationRequest(1, 50)),
            CancellationToken.None);

        await _categories.Received(1).GetPageAsync(
            Arg.Any<string?>(), Arg.Any<CategoryStatusFilter?>(), Arg.Any<CategorySortOption>(),
            Arg.Is<PaginationRequest>(p => p.PageSize == 10), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Handle_WhenRequestedPageIsBelowOne_ClampsToPageOne()
    {
        _categories.GetPageAsync(
            Arg.Any<string?>(), Arg.Any<CategoryStatusFilter?>(), Arg.Any<CategorySortOption>(),
            Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<CategoryListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(
            new GetCategoriesPageQuery(null, null, CategorySortOption.NameAToZ, new PaginationRequest(0, 10)),
            CancellationToken.None);

        await _categories.Received(1).GetPageAsync(
            Arg.Any<string?>(), Arg.Any<CategoryStatusFilter?>(), Arg.Any<CategorySortOption>(),
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
// AC-32: Handle_WithNewestOrOldestSort_PassesItToTheRepositoryUnchanged

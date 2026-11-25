using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Tests for <see cref="GetAdminProductsPageHandler"/>: the fixed page size is enforced
/// regardless of what the caller requests (AC-1), a blank search collapses to no narrowing
/// (AC-3), and the built <see cref="AdminProductCriteria"/> carries every criterion through to
/// the repository verbatim (AC-4).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class GetAdminProductsPageHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();

    private GetAdminProductsPageHandler CreateSut() => new(_products);

    private static ProductListItemDto Item(bool isPublished) =>
        new(Guid.NewGuid(), "Elf Bar BC5000", "ELF-BC5000", null, "Elf Bar", "Disposables",
            24.99m, 24.99m, 0, "CAD", isPublished);

    private static GetAdminProductsPageQuery Query(
        string? search = null,
        ProductStatusFilter? status = null,
        IReadOnlyList<Guid>? brandIds = null,
        IReadOnlyList<Guid>? categoryIds = null,
        decimal? priceMin = null,
        decimal? priceMax = null,
        AdminProductSortOption sort = AdminProductSortOption.NameAToZ,
        PaginationRequest? pagination = null) =>
        new(search, status, brandIds, categoryIds, priceMin, priceMax, sort, pagination ?? new PaginationRequest(1, 10));

    // =========================================================================
    // Happy path — every product, published and unpublished (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_ReturnsBothPublishedAndUnpublishedProducts()
    {
        var page = new PagedResult<ProductListItemDto>([Item(true), Item(false)], 1, 10, 2);
        _products.GetAdminPageAsync(Arg.Any<AdminProductCriteria>(), Arg.Any<CancellationToken>()).Returns(page);

        var result = await CreateSut().Handle(Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().Contain(i => !i.IsPublished);
    }

    // =========================================================================
    // Fixed page size (AC-1) — the caller's requested size is clamped, never honoured as-is
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenTheCallerRequestsADifferentPageSize_ClampsItToTheFixedTen()
    {
        _products.GetAdminPageAsync(Arg.Any<AdminProductCriteria>(), Arg.Any<CancellationToken>())
            .Returns(call => new PagedResult<ProductListItemDto>(
                [], call.Arg<AdminProductCriteria>().Pagination.Page, call.Arg<AdminProductCriteria>().Pagination.PageSize, 0));

        await CreateSut().Handle(Query(pagination: new PaginationRequest(2, 50)), CancellationToken.None);

        await _products.Received(1).GetAdminPageAsync(
            Arg.Is<AdminProductCriteria>(c => c.Pagination.PageSize == 10 && c.Pagination.Page == 2),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Blank search collapses to no narrowing (AC-3)
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenSearchIsBlank_BuildsCriteriaWithNullSearch(string blank)
    {
        _products.GetAdminPageAsync(Arg.Any<AdminProductCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ProductListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(Query(search: blank), CancellationToken.None);

        await _products.Received(1).GetAdminPageAsync(
            Arg.Is<AdminProductCriteria>(c => c.Search == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenSearchHasSurroundingWhitespace_TrimsItInTheCriteria()
    {
        _products.GetAdminPageAsync(Arg.Any<AdminProductCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ProductListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(Query(search: "  elf bar  "), CancellationToken.None);

        await _products.Received(1).GetAdminPageAsync(
            Arg.Is<AdminProductCriteria>(c => c.Search == "elf bar"), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Every criterion travels to the repository as one immutable criteria record (AC-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_BuildsCriteriaCarryingEveryFilterAndSortVerbatim()
    {
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        _products.GetAdminPageAsync(Arg.Any<AdminProductCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ProductListItemDto>([], 1, 10, 0));

        await CreateSut().Handle(
            Query(
                search: "bar",
                status: ProductStatusFilter.Active,
                brandIds: [brandId],
                categoryIds: [categoryId],
                priceMin: 10m,
                priceMax: 25m,
                sort: AdminProductSortOption.LowestVariantPriceAsc),
            CancellationToken.None);

        await _products.Received(1).GetAdminPageAsync(
            Arg.Is<AdminProductCriteria>(c =>
                c.Search == "bar"
                && c.Status == ProductStatusFilter.Active
                && c.BrandIds!.Single() == brandId
                && c.CategoryIds!.Single() == categoryId
                && c.PriceMin == 10m
                && c.PriceMax == 25m
                && c.Sort == AdminProductSortOption.LowestVariantPriceAsc),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Edge case — no products match
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_WhenNoProductsMatch_ReturnsAnEmptyPage()
    {
        _products.GetAdminPageAsync(Arg.Any<AdminProductCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ProductListItemDto>([], 1, 10, 0));

        var result = await CreateSut().Handle(Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1 (published and unpublished, fixed page size): Handle_ReturnsBothPublishedAndUnpublishedProducts,
//        Handle_WhenTheCallerRequestsADifferentPageSize_ClampsItToTheFixedTen
// AC-3 (trimmed, blank-collapsing search): Handle_WhenSearchIsBlank_BuildsCriteriaWithNullSearch,
//        Handle_WhenSearchHasSurroundingWhitespace_TrimsItInTheCriteria
// AC-4 (search + status + brand + category + price combine): Handle_BuildsCriteriaCarryingEveryFilterAndSortVerbatim
// AC-21 (empty results): Handle_WhenNoProductsMatch_ReturnsAnEmptyPage

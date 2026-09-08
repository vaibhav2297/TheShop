using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Tests for <see cref="GetAdminProductsPageHandler"/>: the fixed page size is enforced
/// regardless of what the caller requests (AC-1, AC-2), and the repository's page is returned
/// verbatim — published and unpublished products alike (RULE-17).
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class GetAdminProductsPageHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();

    private GetAdminProductsPageHandler CreateSut() => new(_products);

    private static ProductListItemDto Item(bool isPublished) =>
        new(Guid.NewGuid(), "Elf Bar BC5000", "ELF-BC5000", null, "Elf Bar", "Disposables",
            24.99m, null, false, "CAD", isPublished);

    // =========================================================================
    // Happy path — every product, published and unpublished (AC-1, RULE-17)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_ReturnsBothPublishedAndUnpublishedProducts()
    {
        var page = new PagedResult<ProductListItemDto>([Item(true), Item(false)], 1, 10, 2);
        _products.GetAdminPageAsync(Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>()).Returns(page);

        var result = await CreateSut().Handle(new GetAdminProductsPageQuery(new PaginationRequest(1, 10)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().Contain(i => !i.IsPublished);
    }

    // =========================================================================
    // Fixed page size (AC-2) — the caller's requested size is clamped, never honoured as-is
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenTheCallerRequestsADifferentPageSize_ClampsItToTheFixedTen()
    {
        _products.GetAdminPageAsync(Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new PagedResult<ProductListItemDto>([], call.Arg<PaginationRequest>().Page, call.Arg<PaginationRequest>().PageSize, 0));

        await CreateSut().Handle(new GetAdminProductsPageQuery(new PaginationRequest(2, 50)), CancellationToken.None);

        await _products.Received(1).GetAdminPageAsync(
            Arg.Is<PaginationRequest>(p => p.PageSize == 10 && p.Page == 2), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Edge case — no products exist yet (AC-35)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public async Task Handle_WhenNoProductsExist_ReturnsAnEmptyPage()
    {
        _products.GetAdminPageAsync(Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ProductListItemDto>([], 1, 10, 0));

        var result = await CreateSut().Handle(new GetAdminProductsPageQuery(new PaginationRequest(1, 10)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1 (list shows published and unpublished products): Handle_ReturnsBothPublishedAndUnpublishedProducts
// AC-2 (fixed page size, not chosen by the caller): Handle_WhenTheCallerRequestsADifferentPageSize_ClampsItToTheFixedTen
// AC-35 (empty list state): Handle_WhenNoProductsExist_ReturnsAnEmptyPage
// RULE-17 (unpublished products stay visible to staff): Handle_ReturnsBothPublishedAndUnpublishedProducts

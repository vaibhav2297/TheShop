using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetProductCataloguePage;
using TheShop.Domain.Entities;
using TheShop.Domain.Enums;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Tests for <see cref="GetProductCataloguePageHandler"/> — normalizes pagination, forwards
/// filter/sort/page criteria to <see cref="IProductRepository"/>, and maps the result page to
/// <see cref="ProductSummaryDto"/> (FR-1, FR-6, FR-7, FR-8, AC-1, AC-2, AC-6, AC-7, AC-8, AC-10).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class GetProductCataloguePageHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();

    private GetProductCataloguePageHandler CreateSut() => new(_products);

    private static Category ExampleCategory() =>
        Category.Create(Guid.NewGuid(), "Disposables", "disposables");

    private static Brand ExampleBrand() =>
        Brand.Create(Guid.NewGuid(), "Elf Bar", "elf-bar");

    private static Product BuildProduct(
        string name = "Elf Bar BC5000",
        ProductPricing? pricing = null,
        int stockQuantity = 10) =>
        Product.Create(
            name, "desc", "https://example.com/photo.webp",
            pricing ?? ProductPricing.Create(Money.Create(24.99m)),
            stockQuantity, true, ExampleCategory(), ExampleBrand(), null, null);

    private static GetProductCataloguePageQuery DefaultQuery(
        IReadOnlyList<AppliedFilterDto>? selectedFilters = null,
        decimal? priceMin = null,
        decimal? priceMax = null,
        ProductSortOption sort = ProductSortOption.NewestFirst,
        int page = 1,
        int pageSize = 12) =>
        new(selectedFilters ?? [], priceMin, priceMax, sort, new PaginationRequest(page, pageSize));

    // =========================================================================
    // Happy path — mapping (AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_WithValidQuery_ReturnsSuccessResultWithMappedProducts()
    {
        var product = BuildProduct();
        _products.GetPageAsync(Arg.Any<ProductCatalogueCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([product], 1, 12, 1));

        var result = await CreateSut().Handle(DefaultQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(x => x.Id == product.Id && x.Name == product.Name);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_WithValidQuery_PreservesPagingMetadataOnTheResult()
    {
        _products.GetPageAsync(Arg.Any<ProductCatalogueCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([BuildProduct()], 2, 12, 25));

        var result = await CreateSut().Handle(DefaultQuery(page: 2), CancellationToken.None);

        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(12);
        result.Value.TotalCount.Should().Be(25);
        result.Value.TotalPages.Should().Be(3);
    }

    // =========================================================================
    // Empty result (AC-10 — no products match the active filters)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_WhenRepositoryReturnsNoMatches_ReturnsEmptyPagedResult()
    {
        _products.GetPageAsync(Arg.Any<ProductCatalogueCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([], 1, 12, 0));

        var result = await CreateSut().Handle(DefaultQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    // =========================================================================
    // Discount / stock mapping carried through (AC-2, AC-11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_WithDiscountedProduct_MapsSalePriceAndIsDiscountedTrue()
    {
        var pricing = ProductPricing.Create(Money.Create(24.99m), Money.Create(19.99m));
        var product = BuildProduct(pricing: pricing);
        _products.GetPageAsync(Arg.Any<ProductCatalogueCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([product], 1, 12, 1));

        var result = await CreateSut().Handle(DefaultQuery(), CancellationToken.None);

        var dto = result.Value.Items.Single();
        dto.IsDiscounted.Should().BeTrue();
        dto.SalePrice.Should().Be(19.99m);
        dto.OriginalPrice.Should().Be(24.99m);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_WithOutOfStockProduct_MapsIsInStockFalse()
    {
        var product = BuildProduct(stockQuantity: 0);
        _products.GetPageAsync(Arg.Any<ProductCatalogueCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([product], 1, 12, 1));

        var result = await CreateSut().Handle(DefaultQuery(), CancellationToken.None);

        result.Value.Items.Single().IsInStock.Should().BeFalse();
    }

    // =========================================================================
    // Criteria forwarding — filters, price range, sort (AC-6, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_ForwardsSelectedFiltersToRepositoryCriteria()
    {
        _products.GetPageAsync(Arg.Any<ProductCatalogueCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([], 1, 12, 0));

        var filters = new List<AppliedFilterDto> { new(ProductFilterKeys.Brand, ["elf-bar-id"]) };
        await CreateSut().Handle(DefaultQuery(selectedFilters: filters), CancellationToken.None);

        await _products.Received(1).GetPageAsync(
            Arg.Is<ProductCatalogueCriteria>(c => c.SelectedFilters == filters),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_ForwardsPriceRangeToRepositoryCriteria()
    {
        _products.GetPageAsync(Arg.Any<ProductCatalogueCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([], 1, 12, 0));

        await CreateSut().Handle(DefaultQuery(priceMin: 10m, priceMax: 50m), CancellationToken.None);

        await _products.Received(1).GetPageAsync(
            Arg.Is<ProductCatalogueCriteria>(c => c.PriceMin == 10m && c.PriceMax == 50m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_ForwardsSortToRepositoryCriteria()
    {
        _products.GetPageAsync(Arg.Any<ProductCatalogueCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([], 1, 12, 0));

        await CreateSut().Handle(DefaultQuery(sort: ProductSortOption.PriceLowToHigh), CancellationToken.None);

        await _products.Received(1).GetPageAsync(
            Arg.Is<ProductCatalogueCriteria>(c => c.Sort == ProductSortOption.PriceLowToHigh),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Pagination normalization (AC-8: 12 products per page)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_WhenPageSizeExceedsMax_ClampsPageSizeBeforeQueryingRepository()
    {
        _products.GetPageAsync(Arg.Any<ProductCatalogueCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([], 1, 12, 0));

        await CreateSut().Handle(
            DefaultQuery(pageSize: 1000), CancellationToken.None);

        await _products.Received(1).GetPageAsync(
            Arg.Is<ProductCatalogueCriteria>(c => c.Pagination.PageSize == GetProductCataloguePageQueryValidator.MaxPageSize),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_ForwardsRequestedPageToRepositoryCriteria()
    {
        _products.GetPageAsync(Arg.Any<ProductCatalogueCriteria>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([], 3, 12, 0));

        await CreateSut().Handle(DefaultQuery(page: 3), CancellationToken.None);

        await _products.Received(1).GetPageAsync(
            Arg.Is<ProductCatalogueCriteria>(c => c.Pagination.Page == 3),
            Arg.Any<CancellationToken>());
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Handle_WithValidQuery_ReturnsSuccessResultWithMappedProducts
// AC-2: Handle_WithDiscountedProduct_MapsSalePriceAndIsDiscountedTrue
// AC-6: Handle_ForwardsSelectedFiltersToRepositoryCriteria
// AC-7: Handle_ForwardsSortToRepositoryCriteria
// AC-8: Handle_WithValidQuery_PreservesPagingMetadataOnTheResult,
//        Handle_WhenPageSizeExceedsMax_ClampsPageSizeBeforeQueryingRepository,
//        Handle_ForwardsRequestedPageToRepositoryCriteria
// AC-10: Handle_WhenRepositoryReturnsNoMatches_ReturnsEmptyPagedResult
// AC-11: Handle_WithOutOfStockProduct_MapsIsInStockFalse

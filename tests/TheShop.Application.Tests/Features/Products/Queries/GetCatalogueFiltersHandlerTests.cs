using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Queries.GetCatalogueFilters;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Tests for <see cref="GetCatalogueFiltersHandler"/> — surfaces the backend-driven filter
/// groups the sidebar renders (FR-6, AC-6; plan §5 decision 4: filters are dynamic and
/// backend-driven, so the UI hard-codes no filter set).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class GetCatalogueFiltersHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();

    private GetCatalogueFiltersHandler CreateSut() => new(_products);

    // =========================================================================
    // Happy path
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_ReturnsSuccessResultWrappingRepositoryFilterGroups()
    {
        var groups = new List<FilterGroupDto>
        {
            new(ProductFilterKeys.Category, "Filter_Category", FilterKind.MultiSelect,
                [new FilterOptionDto("cat-1", "Disposables", 5)], null),
            new(ProductFilterKeys.Price, "Filter_Price", FilterKind.Range, [], new RangeFilterDto(6.99m, 54.99m)),
        };
        _products.GetFilterGroupsAsync(Arg.Any<CancellationToken>()).Returns(groups);

        var result = await CreateSut().Handle(new GetCatalogueFiltersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Groups.Should().BeEquivalentTo(groups);
    }

    // =========================================================================
    // Edge case: catalogue has no products at all → no filter options
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task Handle_WhenRepositoryReturnsNoGroups_ReturnsEmptyGroupsList()
    {
        _products.GetFilterGroupsAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateSut().Handle(new GetCatalogueFiltersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Groups.Should().BeEmpty();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: Handle_ReturnsSuccessResultWrappingRepositoryFilterGroups (filter groups feed the
//        panel that narrows the catalogue)

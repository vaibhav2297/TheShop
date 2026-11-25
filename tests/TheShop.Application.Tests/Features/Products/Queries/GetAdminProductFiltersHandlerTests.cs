using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductFilters;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

/// <summary>
/// Tests for <see cref="GetAdminProductFiltersHandler"/> — a thin pass-through to
/// <see cref="IProductRepository.GetAdminFiltersAsync"/> (plan §5 Decision 7).
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class GetAdminProductFiltersHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();

    private GetAdminProductFiltersHandler CreateSut() => new(_products);

    [Fact]
    [Trait("Feature", "manage-product")]
    public async Task Handle_ReturnsTheRepositoriesFiltersVerbatim()
    {
        var filters = new AdminProductFiltersDto(
            [new FilterOptionDto("brand-1", "Elf Bar", null)],
            [new FilterOptionDto("cat-1", "Disposables", null)],
            new RangeFilterDto(5m, 50m));
        _products.GetAdminFiltersAsync(Arg.Any<CancellationToken>()).Returns(filters);

        var result = await CreateSut().Handle(new GetAdminProductFiltersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(filters);
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-4 (brand/category filter options, price bounds sourced for the panel): Handle_ReturnsTheRepositoriesFiltersVerbatim

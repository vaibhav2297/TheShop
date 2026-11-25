using FluentAssertions;
using MediatR;
using NSubstitute;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductListing;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

public class GetAdminProductListingHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private static FilterGroupDto Colour => new("variant:colour", "Filter_Variant", FilterKind.MultiSelect,
        [new("blue", "Blue", null)], null, "Colour");
    private static GetAdminProductsPageQuery Query => new("search", ProductStatusFilter.Inactive,
        [Guid.Parse("00000000-0000-0000-0000-000000000001")], [], 10, 50,
        AdminProductSortOption.NameAToZ, new(2, 10), [new("variant:colour", ["blue", "gone"])]);
    private GetAdminProductListingHandler Sut => new(_products, _sender);

    public GetAdminProductListingHandlerTests()
    {
        _products.GetAdminFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(new AdminProductFiltersDto([], [], new(0, 100)) { VariantGroups = [Colour] });
        _sender.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(PagedResult<ProductListItemDto>.Empty(new(2, 10))));
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Handle_AdminChoices_ReconcilesUsingManagementMetadataAndPreservesCriteria()
    {
        var result = await Sut.Handle(new(Query), CancellationToken.None);

        result.Value.EffectiveVariantFilters.Single().Values.Should().Equal("blue");
        result.Value.VariantGroups.Should().BeEquivalentTo(new[] { Colour });
        result.Value.RemovedVariantSelections.Should().BeTrue();
        await _sender.Received(1).Send(Arg.Is<GetAdminProductsPageQuery>(q =>
            q.Search == Query.Search && q.Status == Query.Status && q.PriceMin == 10 && q.PriceMax == 50
            && q.BrandIds!.SequenceEqual(Query.BrandIds!) && q.CategoryIds!.Count == 0
            && q.Pagination == Query.Pagination && q.Sort == Query.Sort
            && q.VariantFilters!.Single().Values.SequenceEqual(new[] { "blue" })), Arg.Any<CancellationToken>());
        await _products.DidNotReceive().GetFilterGroupsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Handle_MetadataFailure_DoesNotDispatchPage()
    {
        _products.GetAdminFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AdminProductFiltersDto>(new InvalidOperationException("metadata unavailable")));

        var act = () => Sut.Handle(new(Query), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _sender.DidNotReceive().Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Handle_PageFailure_ReturnsFailureWithoutEmptySuccess()
    {
        _sender.Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<PagedResult<ProductListItemDto>>("failed", ["detail"], [new("PriceMin", "invalid")]));

        var result = await Sut.Handle(new(Query), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("failed");
        result.ErrorArgs.Should().Equal("detail");
        result.ValidationErrors.Should().ContainSingle();
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Handle_CancelledPage_DiscardsResponseAndForwardsToken()
    {
        using var cts = new CancellationTokenSource();
        _sender.Send(Arg.Any<GetAdminProductsPageQuery>(), cts.Token).Returns(_ =>
        {
            cts.Cancel();
            return Result.Ok(PagedResult<ProductListItemDto>.Empty(new(1, 10)));
        });

        var act = () => Sut.Handle(new(Query), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _products.Received(1).GetAdminFiltersAsync(cts.Token);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Authorization_WithoutProductsView_DeniesBeforeMetadataOrPage()
    {
        var user = Substitute.For<ICurrentUserService>();
        var behavior = new AuthorizationBehavior<GetAdminProductListingQuery, Result<ProductListingPageDto<ProductListItemDto>>>(user);

        var result = await behavior.Handle(new(Query), ct => Sut.Handle(new(Query), ct), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _products.DidNotReceive().GetAdminFiltersAsync(Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<GetAdminProductsPageQuery>(), Arg.Any<CancellationToken>());
    }
}

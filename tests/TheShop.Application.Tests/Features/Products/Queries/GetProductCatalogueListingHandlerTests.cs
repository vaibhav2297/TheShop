using FluentAssertions;
using MediatR;
using NSubstitute;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetProductCatalogueListing;
using TheShop.Application.Features.Products.Queries.GetProductCataloguePage;
using TheShop.Domain.Enums;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

public class GetProductCatalogueListingHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private static FilterGroupDto Colour => new("variant:colour", "Filter_Variant", FilterKind.MultiSelect,
        [new("red", "Red", null), new("blue", "Blue", null)], null, "Colour");
    private static GetProductCataloguePageQuery Query => new(
        [new("brand", ["brand-id"]), new("variant:colour", ["red", "gone"])],
        10, 50, ProductSortOption.PriceLowToHigh, new(3, 12));
    private GetProductCatalogueListingHandler Sut => new(_products, _sender);

    public GetProductCatalogueListingHandlerTests()
    {
        _products.GetFilterGroupsAsync(Arg.Any<CancellationToken>()).Returns([Colour]);
        _sender.Send(Arg.Any<GetProductCataloguePageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new PagedResult<ProductSummaryDto>([], 3, 12, 25)));
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Handle_StaleSelections_ReconcilesBeforePagingAndPreservesFixedCriteria()
    {
        using var cts = new CancellationTokenSource();
        var result = await Sut.Handle(new(Query), cts.Token);

        result.Value.RemovedVariantSelections.Should().BeTrue();
        result.Value.VariantGroups.Should().BeEquivalentTo(new[] { Colour });
        result.Value.EffectiveVariantFilters.Single().Values.Should().Equal("red");
        result.Value.Page.TotalCount.Should().Be(25);
        await _sender.Received(1).Send(Arg.Is<GetProductCataloguePageQuery>(q =>
            q.SelectedFilters.Count == 2 && q.SelectedFilters[0].Key == "brand"
            && q.SelectedFilters[1].Values.SequenceEqual(new[] { "red" })
            && q.PriceMin == 10 && q.PriceMax == 50 && q.Sort == Query.Sort && q.Pagination == Query.Pagination), cts.Token);
        await _products.Received(1).GetFilterGroupsAsync(cts.Token);
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Handle_ChangedMetadata_ReloadsAllChoicesBeforeEveryPage()
    {
        await Sut.Handle(new(Query), CancellationToken.None);
        _products.GetFilterGroupsAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await Sut.Handle(new(Query), CancellationToken.None);

        result.Value.VariantGroups.Should().BeEmpty();
        result.Value.EffectiveVariantFilters.Should().BeEmpty();
        result.Value.RemovedVariantSelections.Should().BeTrue();
        await _products.Received(2).GetFilterGroupsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Handle_MetadataFailure_DoesNotDispatchPageOrPretendEmptySuccess()
    {
        _products.GetFilterGroupsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<FilterGroupDto>>(new InvalidOperationException("metadata unavailable")));

        var act = () => Sut.Handle(new(Query), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("metadata unavailable");
        await _sender.DidNotReceive().Send(Arg.Any<GetProductCataloguePageQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Handle_PageFailure_PreservesErrorDetails()
    {
        _sender.Send(Arg.Any<GetProductCataloguePageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<PagedResult<ProductSummaryDto>>("failed", ["detail"], [new("PriceMin", "invalid")]));

        var result = await Sut.Handle(new(Query), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("failed");
        result.ErrorArgs.Should().Equal("detail");
        result.ValidationErrors.Should().ContainSingle().Which.PropertyName.Should().Be("PriceMin");
    }

    [Fact]
    [Trait("Feature", "002_variant-filters")]
    public async Task Handle_SupersededPage_DoesNotAdoptResponse()
    {
        using var cts = new CancellationTokenSource();
        _sender.Send(Arg.Any<GetProductCataloguePageQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cts.Cancel();
            return Result.Ok(PagedResult<ProductSummaryDto>.Empty(new(1, 12)));
        });

        var act = () => Sut.Handle(new(Query), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

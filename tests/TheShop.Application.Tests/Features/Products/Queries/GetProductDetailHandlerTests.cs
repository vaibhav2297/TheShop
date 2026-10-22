using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Queries.GetProductDetail;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Queries;

public class GetProductDetailHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Handle_PublishedProduct_ReturnsPublicDetailAndPropagatesCancellation()
    {
        var product = ProductDetailTestData.Build();
        using var cancellation = new CancellationTokenSource();
        _products.GetPublishedDetailAsync(product.Id, cancellation.Token).Returns(product);
        var result = await new GetProductDetailHandler(_products, _storage)
            .Handle(new(product.Id), cancellation.Token);
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Peach Ice");
        result.Value.CurrentPrice.Should().BeNull();
        result.Value.MinVariantPrice.Should().Be(20);
        await _products.Received(1).GetPublishedDetailAsync(product.Id, cancellation.Token);
        await _products.DidNotReceive().GetForEditAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory, InlineData(true), InlineData(false)]
    [Trait("Feature", "001_product-detail")]
    public async Task Handle_MissingOrUnpublishedProduct_ReturnsSameUnavailableKey(bool missing)
    {
        _products.GetPublishedDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(missing ? (Product?)null : ProductDetailTestData.Build(published: false));
        var result = await new GetProductDetailHandler(_products, _storage)
            .Handle(new(ProductDetailTestData.Id(1)), CancellationToken.None);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrorKeys.DetailUnavailable);
        _storage.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Handle_RepositoryFailure_DoesNotInventProductData()
    {
        _products.GetPublishedDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Product?>(new InvalidOperationException("provider failure")));
        var act = () => new GetProductDetailHandler(_products, _storage)
            .Handle(new(ProductDetailTestData.Id(1)), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("provider failure");
    }
}

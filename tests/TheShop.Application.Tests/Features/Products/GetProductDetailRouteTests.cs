using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetProductDetail;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Products;

public class GetProductDetailRouteTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly Product _product = ProductDetailTestData.Build();

    public GetProductDetailRouteTests()
    {
        _products.GetPublishedDetailBySlugAsync("peach-ice", Arg.Any<CancellationToken>())
            .Returns(_product);
        _products.GetPublishedDetailAsync(_product.Id, Arg.Any<CancellationToken>())
            .Returns(_product);
        _products.GetAddressesAsync(_product.Id, Arg.Any<CancellationToken>())
            .Returns(new ProductAddressDto("peach-ice", new Dictionary<Guid, string>
            {
                [ProductDetailTestData.Id(40)] = "mango",
                [ProductDetailTestData.Id(41)] = "peach"
            }));
        _storage.GetPublicUrl(StorageArea.ProductImages, Arg.Any<string>())
            .Returns(call => "/" + call.ArgAt<string>(1));
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task Handle_ExactVariantSlug_SelectsItsIdentityAndGallery()
    {
        var result = await new GetProductDetailHandler(_products, _storage)
            .Handle(new(null, "peach-ice", "peach"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SelectedVariantId.Should().Be(ProductDetailTestData.Id(41));
        result.Value.Variants.Single(v => v.Id == ProductDetailTestData.Id(41)).ImageIds.Should().BeEmpty();
        result.Value.Variants.Single(v => v.Id == ProductDetailTestData.Id(40)).ImageIds
            .Should().Equal(ProductDetailTestData.Id(10));
        result.Value.ProductSlug.Should().Be("peach-ice");
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task Handle_ParentOrLegacyGuid_SelectsFirstAvailableVariant()
    {
        var handler = new GetProductDetailHandler(_products, _storage);

        var parent = await handler.Handle(new(null, "peach-ice"), CancellationToken.None);
        var legacy = await handler.Handle(new(_product.Id), CancellationToken.None);

        parent.Value!.SelectedVariantId.Should().Be(ProductDetailTestData.Id(40));
        legacy.Value!.SelectedVariantId.Should().Be(ProductDetailTestData.Id(40));
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task Handle_UnknownVariantSlug_DoesNotSubstituteDefault()
    {
        var result = await new GetProductDetailHandler(_products, _storage)
            .Handle(new(null, "peach-ice", "deleted"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task Handle_WrongParentSlug_RejectsContent()
    {
        _products.GetPublishedDetailBySlugAsync("other-product", Arg.Any<CancellationToken>())
            .Returns(_product);

        var result = await new GetProductDetailHandler(_products, _storage)
            .Handle(new(null, "other-product", "mango"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task Handle_UnavailableExactVariant_KeepsItsIdentityAndImages()
    {
        var unavailable = ProductVariant.Create(ProductDetailTestData.Id(40),
            TheShop.Domain.ValueObjects.Sku.Create("DETAIL-40"),
            TheShop.Domain.ValueObjects.ProductPricing.Create(TheShop.Domain.ValueObjects.Money.Create(30)),
            false, [ProductDetailTestData.Id(10)],
            new HashSet<Guid> { ProductDetailTestData.Id(21) }, 0);
        _products.GetPublishedDetailBySlugAsync("peach-ice", Arg.Any<CancellationToken>())
            .Returns(ProductDetailTestData.Build(variantRows: [unavailable]));

        var result = await new GetProductDetailHandler(_products, _storage)
            .Handle(new(null, "peach-ice", "mango"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SelectedVariantId.Should().Be(unavailable.Id);
        result.Value.Variants.Single().IsAvailable.Should().BeFalse();
        result.Value.Variants.Single().ImageIds.Should().Equal(ProductDetailTestData.Id(10));
    }
}

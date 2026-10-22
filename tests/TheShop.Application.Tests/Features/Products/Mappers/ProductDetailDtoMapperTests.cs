using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.Mappers;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Products.Mappers;

public class ProductDetailDtoMapperTests
{
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();

    public ProductDetailDtoMapperTests() => _storage.GetPublicUrl(StorageArea.ProductImages, Arg.Any<string>())
        .Returns(call => "https://images.example/" + call.ArgAt<string>(1));

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDto_VariantProductWithoutOwnPrice_UsesEffectiveVariantRangeAndPinnedOrPrimaryImages()
    {
        var dto = ProductDetailDtoMapper.ToDto(ProductDetailTestData.Build(), _storage);
        dto.BrandName.Should().Be("Test Brand");
        dto.HasVariants.Should().BeTrue();
        dto.CurrentPrice.Should().BeNull();
        dto.MinVariantPrice.Should().Be(20);
        dto.MaxVariantPrice.Should().Be(25);
        dto.Images.Select(i => i.Id).Should().Equal(ProductDetailTestData.Id(11), ProductDetailTestData.Id(10));
        dto.Images[0].Url.Should().Be("https://images.example/primary.png");
        dto.Variants[0].CurrentPrice.Should().Be(20);
        dto.Variants[0].OriginalPrice.Should().Be(30);
        dto.Variants[0].TargetImageId.Should().Be(ProductDetailTestData.Id(10));
        dto.Variants[1].TargetImageId.Should().Be(dto.PrimaryImageId);
        dto.Variants.Select(v => v.Label).Should().Equal("Mango", "Peach");
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDto_NoVariants_UsesProductSalePrice()
    {
        var dto = ProductDetailDtoMapper.ToDto(ProductDetailTestData.Build(variants: false), _storage);
        dto.HasVariants.Should().BeFalse();
        dto.CurrentPrice.Should().Be(25);
        dto.OriginalPrice.Should().Be(30);
        dto.Variants.Should().BeEmpty();
        dto.MinVariantPrice.Should().BeNull();
        dto.MaxVariantPrice.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDto_NoPrimary_UsesFirstImageByPositionThenIdentity()
    {
        var images = new[] { ProductImage.Rehydrate(ProductDetailTestData.Id(12), "late.png", 2, false),
            ProductImage.Rehydrate(ProductDetailTestData.Id(11), "tie.png", 0, false),
            ProductImage.Rehydrate(ProductDetailTestData.Id(10), "first.png", 0, false) };
        var dto = ProductDetailDtoMapper.ToDto(ProductDetailTestData.Build(images: images), _storage);
        dto.Images.Select(i => i.Id).Should().Equal(ProductDetailTestData.Id(10), ProductDetailTestData.Id(11), ProductDetailTestData.Id(12));
        dto.PrimaryImageId.Should().Be(ProductDetailTestData.Id(10));
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDto_EmptyGallery_DoesNotInventImageOrVariantTarget()
    {
        var dto = ProductDetailDtoMapper.ToDto(ProductDetailTestData.Build(images: []), _storage);
        dto.Images.Should().BeEmpty();
        dto.PrimaryImageId.Should().BeNull();
        dto.Variants.Should().OnlyContain(v => v.TargetImageId == null);
        _storage.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDto_StalePinnedImage_FallsBackToPrimary()
    {
        var dto = ProductDetailDtoMapper.ToDto(ProductDetailTestData.Build(images:
            [ProductImage.Rehydrate(ProductDetailTestData.Id(11), "only.png", 0, true)]), _storage);
        dto.Variants.Should().OnlyContain(v => v.TargetImageId == dto.PrimaryImageId);
    }

    [Theory, InlineData(""), InlineData("   "), InlineData("<p><br></p>"),
     InlineData("<script>alert(1)</script>"), InlineData("<p onclick=\"alert(1)\">Bad</p>"),
     Trait("Feature", "001_product-detail")]
    public void ToDto_EmptyOrUnsafeLegacyDescription_OmitsContent(string html)
    {
        ProductDetailDtoMapper.ToDto(ProductDetailTestData.Build(description: html), _storage)
            .DescriptionHtml.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDto_PopulatedContent_PreservesSafeHtmlAndSpecificationOrder()
    {
        var dto = ProductDetailDtoMapper.ToDto(ProductDetailTestData.Build(specifications:
            [ProductSpecification.Create(ProductDetailTestData.Id(32), "Material", "Steel", 2),
             ProductSpecification.Create(ProductDetailTestData.Id(31), "Capacity", "2 ml", 0)]), _storage);
        dto.DescriptionHtml.Should().Be("<p>Product details.</p>");
        dto.Specifications.Select(s => s.Name).Should().Equal("Capacity", "Material");
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDto_BlankBrand_OmitsBrandWithoutInventingText()
    {
        ProductDetailDtoMapper.ToDto(ProductDetailTestData.Build(brand: " "), _storage).BrandName.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDto_EqualEffectivePrices_ProvidesEqualRangeEndpoints()
    {
        var dto = ProductDetailDtoMapper.ToDto(ProductDetailTestData.Build(variantRows:
            [ProductDetailTestData.Variant(40, 21, 30, 25, null), ProductDetailTestData.Variant(41, 22, 25, null, null)]), _storage);
        dto.MinVariantPrice.Should().Be(25);
        dto.MaxVariantPrice.Should().Be(25);
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDto_UnpricedVariants_DoesNotInventRangeOrCurrency()
    {
        var dto = ProductDetailDtoMapper.ToDto(ProductDetailTestData.Build(variantRows:
            [ProductVariant.Create(ProductDetailTestData.Id(40), TheShop.Domain.ValueObjects.Sku.Create("UNPRICED"),
                null, false, null, new HashSet<Guid> { ProductDetailTestData.Id(21) }, 0)]), _storage);
        dto.HasVariants.Should().BeTrue();
        dto.MinVariantPrice.Should().BeNull();
        dto.MaxVariantPrice.Should().BeNull();
        dto.Currency.Should().Be("CAD");
        dto.Variants.Single().CurrentPrice.Should().BeNull();
        dto.Variants.Single().IsAvailable.Should().BeFalse();
    }
}

using FluentAssertions;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.DTOs;
using Xunit;

namespace TheShop.Application.Tests.Features.Products;

public class ProductDetailVariantResolverTests
{
    private static Guid Id(int value) => ProductDetailTestData.Id(value);
    private static ProductDetailDto Product() => new(Id(1), "Peach Ice", null, true, "CAD", null, null, null,
        20, 25, null, null, [],
        [new(Id(10), "Flavour", 0, [new(Id(11), "Mango", 0), new(Id(12), "Peach", 1)]),
         new(Id(20), "Nicotine", 1, [new(Id(21), "10mg", 0), new(Id(22), "20mg", 1)])], [],
        [new(Id(30), "Mango / 10mg", 25, 20, 20, true, null, [Id(11), Id(21)]),
         new(Id(31), "Peach / 20mg", 25, null, 25, true, null, [Id(12), Id(22)])]);

    [Theory, InlineData(""), InlineData("11"), InlineData("11,12"), InlineData("11,11"),
     InlineData("11,22"), InlineData("11,99"), InlineData("11,21,22")]
    [Trait("Feature", "001_product-detail")]
    public void Resolve_IncompleteDuplicateUnknownOrUnconfiguredChoices_DoesNotSelectVariant(string choices)
    {
        var values = choices.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => Id(int.Parse(v))).ToArray();
        ProductDetailVariantResolver.Resolve(Product(), values).Should().BeNull();
    }

    [Theory, InlineData(11, 21, 30), InlineData(22, 12, 31)]
    [Trait("Feature", "001_product-detail")]
    public void Resolve_CompleteConfiguredChoices_ReturnsExactVariantRegardlessOfOrder(int first, int second, int expected)
    {
        var product = Product();
        ProductDetailVariantResolver.Resolve(product, [Id(first), Id(second)])
            .Should().BeSameAs(product.Variants.Single(v => v.Id == Id(expected)));
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Resolve_ProductWithoutVariants_DoesNotSelectVariant()
    {
        ProductDetailVariantResolver.Resolve(Product() with { HasVariants = false }, [Id(11), Id(21)]).Should().BeNull();
    }
}

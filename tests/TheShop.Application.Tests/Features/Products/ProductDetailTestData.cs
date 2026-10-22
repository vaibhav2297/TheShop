using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Tests.Features.Products;

internal static class ProductDetailTestData
{
    internal static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    internal static Product Build(bool published = true, bool variants = true,
        IReadOnlyList<ProductImage>? images = null, string description = "<p>Product details.</p>",
        IReadOnlyList<ProductSpecification>? specifications = null, string brand = "Test Brand",
        IReadOnlyList<ProductVariant>? variantRows = null) =>
        Product.Rehydrate(Id(1), "Peach Ice", ProductDescription.Rehydrate(description), null,
            Sku.Create("DETAIL-1"), variants ? null : ProductPricing.Create(Money.Create(30), Money.Create(25)),
            published, Category.Rehydrate(Id(2), "Vapes"), Brand.Rehydrate(Id(3), brand),
            new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero),
            images ?? [ProductImage.Rehydrate(Id(10), "first.png", 0, false),
                ProductImage.Rehydrate(Id(11), "primary.png", 5, true)],
            variants ? [ProductOptionType.Create(Id(20), "Flavour", 0,
                [new(Id(21), "Mango"), new(Id(22), "Peach")])] : [],
            specifications ?? [ProductSpecification.Create(Id(30), "Capacity", "2 ml", 0)],
            variantRows ?? (variants ? [Variant(40, 21, 30, 20, Id(10)), Variant(41, 22, 25, null, null)] : []));

    internal static ProductVariant Variant(int id, int value, decimal original, decimal? sale, Guid? image) =>
        ProductVariant.Create(Id(id), Sku.Create($"DETAIL-{id}"),
            ProductPricing.Create(Money.Create(original), sale is { } amount ? Money.Create(amount) : null),
            true, image, new HashSet<Guid> { Id(value) }, id);
}

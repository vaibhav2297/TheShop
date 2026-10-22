using FluentAssertions;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public class ProductDetailImageMapperTests
{
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
    private static ProductRecord Record() => new()
    {
        Id = Id(1),
        Name = "Peach Ice",
        Sku = "PEACH-1",
        Currency = "CAD",
        OriginalPrice = 25,
        IsPublished = true,
        CreatedAt = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero),
        Category = new() { Id = Id(2), Name = "Vapes" },
        Brand = new() { Id = Id(3), Name = "Test Brand" }
    };

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDomain_GalleryPrimary_TakesPrecedenceOverPositionAndLegacy()
    {
        var row = Record(); row.ImagePath = "legacy.png";
        row.Images = [new() { Id = Id(10), ObjectKey = "first.png", Position = 0 },
            new() { Id = Id(11), ObjectKey = "primary.png", Position = 5, IsPrimary = true }];
        row.ToDomain(key => "https://images.example/" + key).ImageUrl.Should().Be("https://images.example/primary.png");
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDomain_GalleryWithoutPrimary_UsesPositionThenIdentity()
    {
        var row = Record();
        row.Images = [new() { Id = Id(12), ObjectKey = "late.png", Position = 2 },
            new() { Id = Id(11), ObjectKey = "tie.png", Position = 0 },
            new() { Id = Id(10), ObjectKey = "first.png", Position = 0 }];
        row.ToDomain(key => "https://images.example/" + key).ImageUrl.Should().Be("https://images.example/first.png");
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDomain_NoGallery_RetainsLegacyThumbnail()
    {
        var row = Record(); row.ImagePath = "legacy.png";
        row.ToDomain(key => "https://images.example/" + key).ImageUrl.Should().Be("https://images.example/legacy.png");
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void ToDomain_NoImagesOrProductPrice_PreservesVariantProductWithoutInventingPrice()
    {
        var row = Record(); row.OriginalPrice = null; row.MinVariantPrice = 20; row.HasSellableVariant = true;
        var product = row.ToDomain(key => key);
        product.HasVariants.Should().BeTrue();
        product.Pricing.Should().BeNull();
        product.MinVariantPrice!.Amount.Should().Be(20);
        product.ImageUrl.Should().Contain("Peach%20Ice");
    }
}

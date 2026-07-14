using FluentAssertions;
using TheShop.Infrastructure.Storage;
using Xunit;

namespace TheShop.Infrastructure.Tests.Storage;

/// <summary>
/// Unit tests for <see cref="ProductImageKey"/> — the Supabase Storage object-key convention for
/// admin-uploaded product images (migration 0004).
/// </summary>
public class ProductImageKeyTests
{
    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void For_NamespacesKeyUnderProduct()
    {
        var key = ProductImageKey.For(ProductId, "photo.webp");

        key.Should().StartWith($"products/{ProductId}/");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void For_PreservesFileExtension()
    {
        var key = ProductImageKey.For(ProductId, "photo.PNG");

        key.Should().EndWith(".png");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void For_WhenNoExtension_OmitsExtension()
    {
        var key = ProductImageKey.For(ProductId, "photo");

        key.Should().NotEndWith(".");
        key.Should().StartWith($"products/{ProductId}/");
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void For_ProducesUniqueKeysAcrossCalls()
    {
        var first = ProductImageKey.For(ProductId, "photo.webp");
        var second = ProductImageKey.For(ProductId, "photo.webp");

        first.Should().NotBe(second);
    }
}

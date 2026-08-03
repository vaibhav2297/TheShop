using FluentAssertions;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="ProductMapper"/>'s image resolution: a product's <c>image_path</c>
/// Storage object key is resolved to a public URL, and a row without an <c>image_path</c> falls
/// back to a name-based placeholder image URL.
/// </summary>
public class ProductMapperTests
{
    private const string ResolvedPrefix = "https://cdn.example/storage/product-images/";

    // Stand-in for Supabase's GetPublicUrl — deterministic so the test can assert the key was
    // passed through the resolver rather than used verbatim.
    private static string ResolvePublicUrl(string path) => ResolvedPrefix + path;

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void ToDomain_WhenImagePathSet_ResolvesToPublicUrl()
    {
        var record = BuildRecord(imagePath: "products/abc/photo.webp");

        var product = record.ToDomain(ResolvePublicUrl);

        product.ImageUrl.Should().Be(ResolvedPrefix + "products/abc/photo.webp");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "product-catalogue")]
    public void ToDomain_WhenImagePathBlank_FallsBackToPlaceholder(string? blankPath)
    {
        var record = BuildRecord(imagePath: blankPath);

        var product = record.ToDomain(ResolvePublicUrl);

        product.ImageUrl.Should().StartWith("https://placehold.co/");
        product.ImageUrl.Should().NotStartWith(ResolvedPrefix);
    }

    // =========================================================================
    // manage-categories: Decision 13 — brands_read no longer hides an Inactive brand, so a
    // published product's embedded BrandRecord is never null on that account. ToDomain doesn't
    // inspect IsActive at all (Brand.Rehydrate is only ever given the id/name here), so an
    // embedded Inactive brand maps exactly like an Active one.
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ToDomain_WhenEmbeddedBrandIsInactive_MapsWithoutThrowing()
    {
        var record = BuildRecord(imagePath: null, brand: new BrandRecord { Id = Guid.NewGuid(), Name = "Elf Bar", IsActive = false });

        var act = () => record.ToDomain(ResolvePublicUrl);

        act.Should().NotThrow();
    }

    private static ProductRecord BuildRecord(string? imagePath, BrandRecord? brand = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Elf Bar BC5000",
        Description = "A long-lasting disposable vape.",
        ImagePath = imagePath,
        OriginalPrice = 24.99m,
        SalePrice = null,
        Currency = "CAD",
        StockQuantity = 40,
        IsPublished = true,
        Flavour = "Blue Razz Ice",
        NicotineStrengthMg = 50,
        CreatedAt = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
        Category = new CategoryRecord { Id = Guid.NewGuid(), Name = "Disposables" },
        Brand = brand ?? new BrandRecord { Id = Guid.NewGuid(), Name = "Elf Bar" },
    };
}

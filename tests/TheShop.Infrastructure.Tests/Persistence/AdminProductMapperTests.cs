using FluentAssertions;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="AdminProductMapper"/> — the flattened <c>admin_products_page</c> /
/// <c>get_admin_product_filters</c> RPC rows carry their own price range, variant count, and
/// brand/category names already rolled up server-side, so mapping is a direct
/// record-to-DTO projection with only the primary image key needing resolution to a public URL.
/// <see href=".specs/manage-product/spec.md"/>
/// </summary>
public class AdminProductMapperTests
{
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private static AdminProductRowRecord BuildRow(string? primaryImageKey = "products/abc/photo.webp") => new()
    {
        Id = Guid.NewGuid(),
        Name = "Elf Bar BC5000",
        Sku = "ELF-BAR-BC5000",
        BrandId = Guid.NewGuid(),
        BrandName = "Elf Bar",
        CategoryId = Guid.NewGuid(),
        CategoryName = "Disposables",
        Currency = "CAD",
        IsPublished = true,
        PrimaryImageKey = primaryImageKey,
        VariantCount = 2,
        MinPrice = 10m,
        MaxPrice = 30m,
        TotalCount = 11,
    };

    // =========================================================================
    // ToDto(AdminProductRowRecord) — the row projection (AC-1, FR-17)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToDto_MapsEveryFieldVerbatim()
    {
        var record = BuildRow();
        _fileStorage.GetPublicUrl(StorageArea.ProductImages, "products/abc/photo.webp")
                    .Returns("https://cdn.example/products/abc/photo.webp");

        var dto = record.ToDto(_fileStorage);

        dto.Id.Should().Be(record.Id);
        dto.Name.Should().Be("Elf Bar BC5000");
        dto.Sku.Should().Be("ELF-BAR-BC5000");
        dto.BrandName.Should().Be("Elf Bar");
        dto.CategoryName.Should().Be("Disposables");
        dto.Currency.Should().Be("CAD");
        dto.IsPublished.Should().BeTrue();
        dto.MinPrice.Should().Be(10m);
        dto.MaxPrice.Should().Be(30m);
        dto.VariantCount.Should().Be(2);
        dto.PrimaryImageUrl.Should().Be("https://cdn.example/products/abc/photo.webp");
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToDto_WhenPrimaryImageKeyIsNull_MapsANullImageUrlWithoutResolving()
    {
        var record = BuildRow(primaryImageKey: null);

        var dto = record.ToDto(_fileStorage);

        dto.PrimaryImageUrl.Should().BeNull();
        _fileStorage.DidNotReceiveWithAnyArgs().GetPublicUrl(default, default!);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToDto_WhenPrimaryImageKeyIsEmpty_MapsANullImageUrlWithoutResolving()
    {
        var record = BuildRow(primaryImageKey: "");

        var dto = record.ToDto(_fileStorage);

        dto.PrimaryImageUrl.Should().BeNull();
        _fileStorage.DidNotReceiveWithAnyArgs().GetPublicUrl(default, default!);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToDto_WithEquallyPricedRange_KeepsBothEndsEqual()
    {
        // Decision 13 / AC-25: the mapper does not collapse the range — that is a Web-layer
        // rendering choice — it carries whatever the RPC computed.
        var record = BuildRow();
        record.MinPrice = 15m;
        record.MaxPrice = 15m;

        var dto = record.ToDto(_fileStorage);

        dto.MinPrice.Should().Be(15m);
        dto.MaxPrice.Should().Be(15m);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToDto_WhenUnpublished_MapsIsPublishedFalse()
    {
        var record = BuildRow();
        record.IsPublished = false;

        var dto = record.ToDto(_fileStorage);

        dto.IsPublished.Should().BeFalse();
    }

    // =========================================================================
    // ToDto(AdminProductFiltersRecord) — the filter panel's options (plan §5 Decision 7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToDto_Filters_MapsEachBrandAndCategoryAsAFilterOption()
    {
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var record = new AdminProductFiltersRecord
        {
            Brands = [new FilterLookupRecord { Id = brandId, Name = "Elf Bar" }],
            Categories = [new FilterLookupRecord { Id = categoryId, Name = "Disposables" }],
            PriceMin = 5m,
            PriceMax = 50m,
        };

        var dto = record.ToDto();

        dto.Brands.Should().ContainSingle(b => b.Value == brandId.ToString() && b.Label == "Elf Bar");
        dto.Categories.Should().ContainSingle(c => c.Value == categoryId.ToString() && c.Label == "Disposables");
        dto.PriceRange.Min.Should().Be(5m);
        dto.PriceRange.Max.Should().Be(50m);
    }

    [Fact]
    [Trait("Feature", "manage-product")]
    public void ToDto_Filters_WhenNoProductOwnsAnyBrandOrCategory_MapsEmptyLists()
    {
        var record = new AdminProductFiltersRecord { Brands = [], Categories = [], PriceMin = 0, PriceMax = 0 };

        var dto = record.ToDto();

        dto.Brands.Should().BeEmpty();
        dto.Categories.Should().BeEmpty();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1, AC-24 (row content, price range, currency, publish status): ToDto_MapsEveryFieldVerbatim,
//        ToDto_WhenUnpublished_MapsIsPublishedFalse
// AC-25 (equal-price range carried through, collapsed later by the Web layer): ToDto_WithEquallyPricedRange_KeepsBothEndsEqual
// (image resolution): ToDto_WhenPrimaryImageKeyIsNull_MapsANullImageUrlWithoutResolving,
//        ToDto_WhenPrimaryImageKeyIsEmpty_MapsANullImageUrlWithoutResolving
// AC-4 (filter options sourced for the panel): ToDto_Filters_MapsEachBrandAndCategoryAsAFilterOption,
//        ToDto_Filters_WhenNoProductOwnsAnyBrandOrCategory_MapsEmptyLists

using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps the admin manage-products RPC responses (<see cref="AdminProductRowRecord"/>,
/// <see cref="AdminProductFiltersRecord"/>) directly to their Application DTOs — the rows are
/// already flattened server-side, so there is no domain <c>Product</c> to rehydrate through.
/// </summary>
internal static class AdminProductMapper
{
    public static ProductListItemDto ToDto(this AdminProductRowRecord record, IFileStorage fileStorage) =>
        new(
            record.Id,
            record.Name,
            record.Sku,
            record.PrimaryImageKey is { Length: > 0 } key
                ? fileStorage.GetPublicUrl(StorageArea.ProductImages, key)
                : null,
            record.BrandName,
            record.CategoryName,
            record.MinPrice,
            record.MaxPrice,
            record.VariantCount,
            record.Currency,
            record.IsPublished);

    public static AdminProductFiltersDto ToDto(this AdminProductFiltersRecord record) =>
        new(
            [.. record.Brands.Select(b => new FilterOptionDto(b.Id.ToString(), b.Name, null))],
            [.. record.Categories.Select(c => new FilterOptionDto(c.Id.ToString(), c.Name, null))],
            new RangeFilterDto(record.PriceMin, record.PriceMax));
}

using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Products.Mappers;

/// <summary>
/// Hand-written <see cref="Product"/> → admin DTO mapping (<see cref="AdminProductDto"/>,
/// <see cref="ProductListItemDto"/>). A dedicated static mapper rather than an AutoMapper
/// profile — the projection is small and one-directional.
/// </summary>
public static class AdminProductDtoMapper
{
    public static AdminProductDto ToAdminDto(Product product, IFileStorage fileStorage, string rowVersion) =>
        new(
            product.Id,
            product.Name,
            product.Description,
            product.Sku.Value,
            product.Category.Id,
            product.Category.Name,
            product.Brand.Id,
            product.Brand.Name,
            product.Pricing?.OriginalPrice.Amount,
            product.Pricing?.SalePrice?.Amount,
            product.IsPublished,
            [.. product.Images.OrderBy(i => i.Position).Select(i => ToImageDto(i, fileStorage))],
            [.. product.OptionTypes.OrderBy(t => t.Position).Select(ToOptionTypeDto)],
            [.. product.Variants.OrderBy(v => v.Position).Select(v => ToVariantDto(product, v))],
            rowVersion);

    public static ProductListItemDto ToListItemDto(Product product, IFileStorage fileStorage)
    {
        var primary = product.Images.FirstOrDefault(i => i.IsPrimary) ?? product.Images.FirstOrDefault();
        var (minPrice, maxPrice) = ResolvePriceRange(product);

        return new ProductListItemDto(
            product.Id,
            product.Name,
            product.Sku.Value,
            primary is not null ? fileStorage.GetPublicUrl(StorageArea.ProductImages, primary.ObjectKey) : null,
            product.Brand.Name,
            product.Category.Name,
            minPrice?.Amount,
            maxPrice?.Amount,
            product.Variants.Count,
            ResolveCurrency(minPrice),
            product.IsPublished);
    }

    /// <summary>
    /// The row's displayed price range (plan §5 Decision 13): the lowest/highest priced variant
    /// when the product has variants, or its own effective price for both ends when it has none.
    /// </summary>
    private static (Money? Min, Money? Max) ResolvePriceRange(Product product)
    {
        if (product.Variants.Count == 0)
        {
            var effective = product.Pricing?.Effective;
            return (effective, effective);
        }

        var prices = product.Variants
            .Select(v => v.Pricing?.Effective)
            .Where(price => price is not null)
            .Select(price => price!)
            .ToList();

        if (prices.Count == 0)
            return (null, null);

        return (prices.MinBy(p => p.Amount), prices.MaxBy(p => p.Amount));
    }

    /// <summary>
    /// The currency the row's displayed price range is denominated in, falling back to the
    /// storefront default for an unpriced product.
    /// </summary>
    private static string ResolveCurrency(Money? minPrice) =>
        minPrice?.Currency ?? Money.DefaultCurrency;

    private static ProductImageDto ToImageDto(ProductImage image, IFileStorage fileStorage) =>
        new(image.Id, fileStorage.GetPublicUrl(StorageArea.ProductImages, image.ObjectKey), image.Position, image.IsPrimary);

    private static ProductOptionTypeDto ToOptionTypeDto(ProductOptionType type) =>
        new(
            type.Id,
            type.Name,
            type.Position,
            [.. type.Values.OrderBy(v => v.Position).Select(v => new ProductOptionValueDto(v.Id, v.Value, v.Position))]);

    private static ProductVariantDto ToVariantDto(Product product, ProductVariant variant) =>
        new(
            variant.Id,
            variant.Sku.Value,
            variant.Pricing?.OriginalPrice.Amount,
            variant.Pricing?.SalePrice?.Amount,
            variant.IsAvailable,
            variant.PinnedImageId,
            [.. variant.OptionValueIds],
            BuildVariantLabel(product, variant));

    private static string BuildVariantLabel(Product product, ProductVariant variant)
    {
        var labels = product.OptionTypes
            .OrderBy(t => t.Position)
            .Select(t => t.Values.FirstOrDefault(v => variant.OptionValueIds.Contains(v.Id))?.Value)
            .Where(value => value is not null);

        return string.Join(" / ", labels);
    }
}

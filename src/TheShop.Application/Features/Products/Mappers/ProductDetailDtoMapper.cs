using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Products.Mappers;

/// <summary>Projects a published aggregate into safe storefront content and deterministic gallery/price data.</summary>
public static class ProductDetailDtoMapper
{
    /// <summary>Orders the shared library and preserves each variant's explicit image order without a fallback.</summary>
    public static ProductDetailDto ToDto(Product product, IFileStorage fileStorage, ProductAddressDto? addresses = null)
    {
        var images = product.Images.OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.Position).ThenBy(i => i.Id)
            .DistinctBy(i => i.Id)
            .Select(i => new ProductImageDto(i.Id,
                fileStorage.GetPublicUrl(StorageArea.ProductImages, i.ObjectKey), i.Position, i.IsPrimary))
            .ToArray();
        var primaryId = images.FirstOrDefault()?.Id;
        var options = product.OptionTypes.OrderBy(t => t.Position).ThenBy(t => t.Id)
            .Select(t => new ProductOptionTypeDto(t.Id, t.Name, t.Position,
                t.Values.OrderBy(v => v.Position).ThenBy(v => v.Id)
                    .Select(v => new ProductOptionValueDto(v.Id, v.Value, v.Position)).ToArray()))
            .ToArray();
        var variants = product.Variants.OrderBy(v => v.Position).ThenBy(v => v.Id)
            .Select(v => new ProductDetailVariantDto(v.Id,
                string.Join(" / ", options.SelectMany(t => t.Values.Where(o => v.OptionValueIds.Contains(o.Id)))
                    .Select(o => o.Value)),
                v.Pricing?.OriginalPrice.Amount, v.Pricing?.SalePrice?.Amount, v.Pricing?.Effective.Amount,
                v.IsAvailable,
                Array.AsReadOnly(v.ImageIds.Where(id => images.Any(image => image.Id == id)).ToArray()),
                v.OptionValueIds.Order().ToArray(),
                addresses?.VariantSlugs.GetValueOrDefault(v.Id) ?? "", v.Sku.Value))
            .ToArray();
        var prices = variants.Where(v => v.CurrentPrice.HasValue).Select(v => v.CurrentPrice!.Value).ToArray();
        var currency = product.HasVariants
            ? product.Variants.FirstOrDefault(v => v.Pricing is not null)?.Pricing?.Effective.Currency
            : product.Pricing?.Effective.Currency;

        return new ProductDetailDto(product.Id, product.Name,
            string.IsNullOrWhiteSpace(product.Brand.Name) ? null : product.Brand.Name,
            product.HasVariants, currency ?? Money.DefaultCurrency,
            product.Pricing?.OriginalPrice.Amount, product.Pricing?.SalePrice?.Amount,
            product.Pricing?.Effective.Amount,
            prices.Length == 0 ? null : prices.Min(), prices.Length == 0 ? null : prices.Max(),
            SafeDescription(product.Description), primaryId, images, options,
            product.Specifications.OrderBy(s => s.Position).ThenBy(s => s.Id)
                .Where(s => !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.Value))
                .Select(s => new ProductSpecificationDto(s.Id, s.Name, s.Value, s.Position)).ToArray(),
            variants, addresses?.ProductSlug ?? "");
    }

    private static string? SafeDescription(ProductDescription description)
    {
        if (string.IsNullOrWhiteSpace(description.PlainText))
            return null;
        try
        {
            return ProductDescription.Create(description.Html).Html;
        }
        catch (ProductDescriptionUnsupportedContentException) { return null; }
        catch (ProductDescriptionTooLongException) { return null; }
    }
}

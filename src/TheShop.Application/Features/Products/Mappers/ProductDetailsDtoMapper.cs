using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Products.Mappers;

/// <summary>
/// Hand-written <see cref="Product"/> → <see cref="ProductDetailsDto"/> mapping for the customer
/// product-details page. A dedicated static mapper rather than an AutoMapper profile — the
/// projection is small and one-directional.
/// </summary>
public static class ProductDetailsDtoMapper
{
    /// <summary>
    /// Maps <paramref name="product"/> to its customer details DTO, resolving gallery URLs through
    /// <paramref name="fileStorage"/>, ordering the gallery primary image first (RULE-1), and
    /// exposing description markup only when it still satisfies the description grammar.
    /// </summary>
    public static ProductDetailsDto ToDetailsDto(Product product, IFileStorage fileStorage)
    {
        var currency = product.Pricing?.OriginalPrice.Currency ?? Money.DefaultCurrency;
        var (descriptionHtml, descriptionText) = ResolveDescription(product.Description);

        return new ProductDetailsDto(
            product.Id,
            product.Name,
            string.IsNullOrWhiteSpace(product.Brand.Name) ? null : product.Brand.Name,
            descriptionHtml,
            descriptionText,
            product.HasVariants ? null : product.Pricing?.OriginalPrice.Amount,
            product.HasVariants ? null : product.Pricing?.SalePrice?.Amount,
            currency,
            [.. product.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.Position)
                .ThenBy(i => i.Id)
                .Select(i => ToImageDto(i, fileStorage))],
            [.. product.OptionTypes.OrderBy(t => t.Position).Select(ToOptionTypeDto)],
            [.. product.Specifications.OrderBy(s => s.Position).Select(ToSpecificationDto)],
            [.. product.Variants
                .OrderBy(v => v.Position)
                .ThenBy(v => v.Id)
                .Select(v => ToVariantDto(v, currency))]);
    }

    /// <summary>
    /// Re-validates persisted markup against the description grammar. Markup that no longer
    /// passes is never exposed; its plain text remains as an encoded fallback.
    /// </summary>
    private static (string Html, string Text) ResolveDescription(ProductDescription description)
    {
        try
        {
            var validated = ProductDescription.Create(description.Html);
            return (validated.Html, validated.PlainText);
        }
        catch (Exception ex) when (ex is ProductDescriptionUnsupportedContentException or ProductDescriptionTooLongException)
        {
            return (string.Empty, description.PlainText);
        }
    }

    private static ProductImageDto ToImageDto(ProductImage image, IFileStorage fileStorage) =>
        new(image.Id, fileStorage.GetPublicUrl(StorageArea.ProductImages, image.ObjectKey), image.Position, image.IsPrimary);

    private static ProductOptionTypeDto ToOptionTypeDto(ProductOptionType type) =>
        new(
            type.Id,
            type.Name,
            type.Position,
            [.. type.Values.OrderBy(v => v.Position).Select(v => new ProductOptionValueDto(v.Id, v.Value, v.Position))]);

    private static ProductSpecificationDto ToSpecificationDto(ProductSpecification specification) =>
        new(specification.Id, specification.Name, specification.Value, specification.Position);

    private static ProductDetailsVariantDto ToVariantDto(ProductVariant variant, string productCurrency) =>
        new(
            variant.Id,
            variant.Position,
            variant.Pricing?.OriginalPrice.Amount,
            variant.Pricing?.SalePrice?.Amount,
            variant.Pricing?.OriginalPrice.Currency ?? productCurrency,
            variant.IsAvailable,
            variant.PinnedImageId,
            [.. variant.OptionValueIds]);
}

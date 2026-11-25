using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Products.Mappers;

/// <summary>
/// Hand-written <see cref="Product"/> → <see cref="ProductSummaryDto"/> mapping. A dedicated
/// static mapper rather than an AutoMapper profile — the projection is small and one-directional.
/// </summary>
public static class ProductDtoMapper
{
    public static ProductSummaryDto ToSummaryDto(Product product)
    {
        // A variant product has no single "original vs sale" price of its own (RULE-19); the
        // catalogue tile shows its lowest variant price undiscounted until the product-catalogue
        // feature builds a full range display (spec Section 4 cross-feature note, AC-17). Either
        // side can come back empty — a draft with nothing priced yet — and the tile says so rather
        // than showing a price of zero, which would read as "free".
        var originalPrice = product.HasVariants
            ? product.MinVariantPrice?.Amount
            : product.Pricing?.OriginalPrice.Amount;
        var salePrice = product.HasVariants ? null : product.Pricing?.SalePrice?.Amount;
        var isDiscounted = !product.HasVariants && product.IsDiscounted;
        var currency = (product.HasVariants
            ? product.MinVariantPrice?.Currency
            : product.Pricing?.OriginalPrice.Currency) ?? Money.DefaultCurrency;

        return new ProductSummaryDto(
            product.Id,
            product.Name,
            product.ImageUrl,
            originalPrice,
            salePrice,
            isDiscounted,
            currency,
            product.IsInStock,
            product.Brand.Name,
            product.MinVariantPrice?.Amount,
            product.HasSellableVariant);
    }
}

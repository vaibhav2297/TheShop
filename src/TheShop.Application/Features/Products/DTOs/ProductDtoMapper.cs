using TheShop.Domain.Entities;

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// Hand-written <see cref="Product"/> → <see cref="ProductSummaryDto"/> mapping. A dedicated
/// static mapper rather than an AutoMapper profile — the projection is small and one-directional.
/// </summary>
public static class ProductDtoMapper
{
    public static ProductSummaryDto ToSummaryDto(Product product) =>
        new(
            product.Id,
            product.Name,
            product.ImageUrl,
            product.Pricing.OriginalPrice.Amount,
            product.Pricing.SalePrice?.Amount,
            product.IsDiscounted,
            product.Pricing.OriginalPrice.Currency,
            product.IsInStock,
            product.Brand.Name,
            product.Flavour,
            product.NicotineStrengthMg);
}

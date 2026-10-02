namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// The customer's current choice on the product-details page: the selected variant (if the
/// product has any), the pricing that variant — or the product itself — displays, and the
/// gallery image shown large. Pricing and image always describe the same selection.
/// </summary>
public sealed record ProductDetailsSelectionDto(
    Guid? VariantId,
    decimal? OriginalPrice,
    decimal? SalePrice,
    string Currency,
    Guid? SelectedImageId)
{
    /// <summary>
    /// <c>true</c> when a sale price is present and below the original price.
    /// </summary>
    public bool IsDiscounted => SalePrice is { } sale && OriginalPrice is { } original && sale < original;

    /// <summary>
    /// The price to display prominently: the sale price when discounted, otherwise the original
    /// price, or <c>null</c> when the selection carries no price.
    /// </summary>
    public decimal? EffectivePrice => IsDiscounted ? SalePrice : OriginalPrice ?? SalePrice;
}

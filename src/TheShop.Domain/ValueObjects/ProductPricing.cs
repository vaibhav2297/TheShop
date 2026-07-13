using TheShop.Domain.Exceptions;

namespace TheShop.Domain.ValueObjects;

/// <summary>
/// The pricing of a product: an original (MRP) price and an optional sale price.
/// Enforces that a sale price, when present, never exceeds the original price.
/// </summary>
public sealed class ProductPricing : IEquatable<ProductPricing>
{
    private const string SaleAboveOriginalKey = "Product_Pricing_SaleAboveOriginal";

    public Money OriginalPrice { get; }
    public Money? SalePrice { get; }

    /// <summary>
    /// <c>true</c> when a sale price is present, i.e. the product is currently discounted.
    /// </summary>
    public bool IsDiscounted => SalePrice is not null;

    /// <summary>
    /// The price to charge and display prominently: the sale price when discounted, otherwise the original price.
    /// </summary>
    public Money Effective => SalePrice ?? OriginalPrice;

    private ProductPricing(Money originalPrice, Money? salePrice)
    {
        OriginalPrice = originalPrice;
        SalePrice = salePrice;
    }

    /// <summary>
    /// Creates a <see cref="ProductPricing"/> after validating that, when present,
    /// <paramref name="salePrice"/> does not exceed <paramref name="originalPrice"/>.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="salePrice"/> is greater than <paramref name="originalPrice"/>.
    /// Carries <c>MessageKey = nameof(Strings.Product_Pricing_SaleAboveOriginal)</c>.
    /// </exception>
    public static ProductPricing Create(Money originalPrice, Money? salePrice = null)
    {
        if (salePrice is not null && salePrice.Amount > originalPrice.Amount)
            throw new DomainException(SaleAboveOriginalKey);

        return new ProductPricing(originalPrice, salePrice);
    }

    public bool Equals(ProductPricing? other) =>
        other is not null &&
        OriginalPrice.Equals(other.OriginalPrice) &&
        Equals(SalePrice, other.SalePrice);

    public override bool Equals(object? obj) => Equals(obj as ProductPricing);

    public override int GetHashCode() => HashCode.Combine(OriginalPrice, SalePrice);
}

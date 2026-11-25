namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised by <c>Product.EnsurePublishable</c> when a product is missing a value required to
/// publish (RULE-14). <see cref="Missing"/> carries machine-readable tokens — <c>"Price"</c> or
/// <c>"VariantPrice:{variantId}"</c> — so the Web layer can flag the exact field or variant row,
/// rather than prose the resource string would need to embed.
/// </summary>
public sealed class ProductNotPublishableException : DomainException
{
    public const string MessageResourceKey = "Product_NotPublishable";

    /// <summary>
    /// The token used when the product's own price is missing.
    /// </summary>
    public const string PriceToken = "Price";

    /// <summary>
    /// The prefix of the token used when a variant's price is missing; the variant's identifier
    /// follows it verbatim.
    /// </summary>
    public const string VariantPriceTokenPrefix = "VariantPrice:";

    /// <summary>
    /// The missing parts, as machine-readable tokens (see remarks above).
    /// </summary>
    public IReadOnlyList<string> Missing { get; }

    public ProductNotPublishableException(IReadOnlyList<string> missing)
        : base(MessageResourceKey)
    {
        Missing = missing;
    }
}

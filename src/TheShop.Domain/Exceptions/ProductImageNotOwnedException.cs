namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a primary-image or variant-pin selection names an image outside the product's
/// own gallery (RULE-13).
/// </summary>
public sealed class ProductImageNotOwnedException : DomainException
{
    public const string MessageResourceKey = "Product_VariantImageNotInGallery";

    public ProductImageNotOwnedException()
        : base(MessageResourceKey)
    {
    }
}

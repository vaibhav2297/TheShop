namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a product's plain-text description exceeds 20,000 characters (RULE-2).
/// </summary>
public sealed class ProductDescriptionTooLongException : DomainException
{
    public const string MessageResourceKey = "Product_DescriptionTooLong";

    public ProductDescriptionTooLongException()
        : base(MessageResourceKey)
    {
    }
}

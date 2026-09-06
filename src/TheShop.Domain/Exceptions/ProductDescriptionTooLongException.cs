namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a product's trimmed description exceeds 2000 characters (RULE-1).
/// </summary>
public sealed class ProductDescriptionTooLongException : DomainException
{
    public const string MessageResourceKey = "Product_DescriptionTooLong";

    public ProductDescriptionTooLongException()
        : base(MessageResourceKey)
    {
    }
}

namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a product's trimmed name exceeds 150 characters (RULE-1).
/// </summary>
public sealed class ProductNameTooLongException : DomainException
{
    public const string MessageResourceKey = "Product_NameTooLong";

    public ProductNameTooLongException()
        : base(MessageResourceKey)
    {
    }
}

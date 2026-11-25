namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a product is created or renamed with an empty or whitespace-only name (RULE-1).
/// </summary>
public sealed class ProductNameRequiredException : DomainException
{
    public const string MessageResourceKey = "Product_NameRequired";

    public ProductNameRequiredException()
        : base(MessageResourceKey)
    {
    }
}

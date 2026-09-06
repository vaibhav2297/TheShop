namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a SKU is created from an empty or whitespace-only value (RULE-8).
/// </summary>
public sealed class SkuRequiredException : DomainException
{
    public const string MessageResourceKey = "Product_SkuRequired";

    public SkuRequiredException()
        : base(MessageResourceKey)
    {
    }
}

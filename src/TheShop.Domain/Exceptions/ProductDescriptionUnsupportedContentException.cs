namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a product description contains a tag or attribute outside the whitelisted
/// markup grammar (RULE-5).
/// </summary>
public sealed class ProductDescriptionUnsupportedContentException : DomainException
{
    public const string MessageResourceKey = "Product_DescriptionUnsupportedContent";

    public ProductDescriptionUnsupportedContentException()
        : base(MessageResourceKey)
    {
    }
}

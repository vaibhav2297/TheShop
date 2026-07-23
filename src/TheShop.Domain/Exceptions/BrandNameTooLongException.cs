namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a brand name exceeds 100 characters (RULE-3).
/// </summary>
public sealed class BrandNameTooLongException : DomainException
{
    public const string MessageResourceKey = "Brand_NameTooLong";

    public BrandNameTooLongException()
        : base(MessageResourceKey)
    {
    }
}

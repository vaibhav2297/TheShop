namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a brand description exceeds 250 characters (RULE-3).
/// </summary>
public sealed class BrandDescriptionTooLongException : DomainException
{
    public const string MessageResourceKey = "Brand_DescriptionTooLong";

    public BrandDescriptionTooLongException()
        : base(MessageResourceKey)
    {
    }
}

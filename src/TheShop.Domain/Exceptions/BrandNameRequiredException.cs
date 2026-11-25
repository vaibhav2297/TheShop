namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a brand is created with an empty or whitespace-only name (RULE-1).
/// </summary>
public sealed class BrandNameRequiredException : DomainException
{
    public const string MessageResourceKey = "Brand_NameRequired";

    public BrandNameRequiredException()
        : base(MessageResourceKey)
    {
    }
}

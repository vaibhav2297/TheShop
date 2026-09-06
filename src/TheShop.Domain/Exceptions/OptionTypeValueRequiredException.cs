namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when an option type has no values, or one of its values is empty or
/// whitespace-only (RULE-9).
/// </summary>
public sealed class OptionTypeValueRequiredException : DomainException
{
    public const string MessageResourceKey = "Product_OptionValueRequired";

    public OptionTypeValueRequiredException()
        : base(MessageResourceKey)
    {
    }
}

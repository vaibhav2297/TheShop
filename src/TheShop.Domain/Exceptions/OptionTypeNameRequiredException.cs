namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when an option type is created or renamed with an empty or whitespace-only name (RULE-9).
/// </summary>
public sealed class OptionTypeNameRequiredException : DomainException
{
    public const string MessageResourceKey = "Product_OptionNameRequired";

    public OptionTypeNameRequiredException()
        : base(MessageResourceKey)
    {
    }
}

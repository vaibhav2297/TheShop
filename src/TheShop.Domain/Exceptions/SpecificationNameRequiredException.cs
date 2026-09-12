namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a retained specification row's trimmed name is empty (RULE-3).
/// </summary>
public sealed class SpecificationNameRequiredException : DomainException
{
    public const string MessageResourceKey = "Product_SpecificationNameRequired";

    public SpecificationNameRequiredException()
        : base(MessageResourceKey)
    {
    }
}

namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a retained specification row's trimmed value is empty (RULE-3).
/// </summary>
public sealed class SpecificationValueRequiredException : DomainException
{
    public const string MessageResourceKey = "Product_SpecificationValueRequired";

    public SpecificationValueRequiredException()
        : base(MessageResourceKey)
    {
    }
}

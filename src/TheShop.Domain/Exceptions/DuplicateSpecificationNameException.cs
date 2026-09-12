namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a product is given two specification rows with the same name, ignoring case
/// and surrounding whitespace (RULE-4).
/// </summary>
public sealed class DuplicateSpecificationNameException : DomainException
{
    public const string MessageResourceKey = "Product_SpecificationNameDuplicated";

    /// <summary>
    /// The specification name that was already in use.
    /// </summary>
    public string Name { get; }

    public DuplicateSpecificationNameException(string name)
        : base(MessageResourceKey)
    {
        Name = name;
    }
}

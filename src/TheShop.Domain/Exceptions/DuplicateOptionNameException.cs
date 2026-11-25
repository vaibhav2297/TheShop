namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a product is given two option types with the same name, ignoring case and
/// surrounding whitespace (RULE-9).
/// </summary>
public sealed class DuplicateOptionNameException : DomainException
{
    public const string MessageResourceKey = "Product_OptionNameDuplicated";

    /// <summary>
    /// The option type name that was already in use.
    /// </summary>
    public string Name { get; }

    public DuplicateOptionNameException(string name)
        : base(MessageResourceKey)
    {
        Name = name;
    }
}

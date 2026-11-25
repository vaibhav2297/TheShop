namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when an option type is given two values that are the same, ignoring case and
/// surrounding whitespace (RULE-9).
/// </summary>
public sealed class DuplicateOptionValueException : DomainException
{
    public const string MessageResourceKey = "Product_OptionValueDuplicated";

    /// <summary>
    /// The option value that was already in use within the option type.
    /// </summary>
    public string Value { get; }

    public DuplicateOptionValueException(string value)
        : base(MessageResourceKey)
    {
        Value = value;
    }
}

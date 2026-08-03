namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a category name exceeds 100 characters (RULE-3).
/// </summary>
public sealed class CategoryNameTooLongException : DomainException
{
    public const string MessageResourceKey = "Category_NameTooLong";

    public CategoryNameTooLongException()
        : base(MessageResourceKey)
    {
    }
}

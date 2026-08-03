namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a category description exceeds 250 characters (RULE-3).
/// </summary>
public sealed class CategoryDescriptionTooLongException : DomainException
{
    public const string MessageResourceKey = "Category_DescriptionTooLong";

    public CategoryDescriptionTooLongException()
        : base(MessageResourceKey)
    {
    }
}

namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when a category is created or renamed with an empty or whitespace-only name (RULE-1).
/// </summary>
public sealed class CategoryNameRequiredException : DomainException
{
    public const string MessageResourceKey = "Category_NameRequired";

    public CategoryNameRequiredException()
        : base(MessageResourceKey)
    {
    }
}

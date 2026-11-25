namespace TheShop.Domain.Enums;

/// <summary>
/// Sort orders offered on the manage-categories admin page. A use-case concept — not a domain rule.
/// </summary>
public enum CategorySortOption
{
    NameAToZ = 0,
    NameZToA = 1,
    NewestFirst = 2,
    OldestFirst = 3,
}

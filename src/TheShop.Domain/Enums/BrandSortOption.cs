namespace TheShop.Domain.Enums;

/// <summary>
/// Sort orders offered on the manage-brands admin page. A use-case concept — not a domain rule.
/// </summary>
public enum BrandSortOption
{
    NameAToZ = 0,
    NameZToA = 1,
    NewestFirst = 2,
}

namespace TheShop.Domain.Enums;

/// <summary>
/// Sort orders offered on the product catalogue page. A use-case concept — not a domain rule.
/// </summary>
public enum ProductSortOption
{
    NewestFirst = 0,
    PriceLowToHigh = 1,
    PriceHighToLow = 2,
    NameAToZ = 3,
    NameZToA = 4,
}

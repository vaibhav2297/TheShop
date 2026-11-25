namespace TheShop.Domain.Enums;

/// <summary>
/// Sort orders offered on the manage-products admin page. A use-case concept — not a domain rule.
/// Separate from <see cref="ProductSortOption"/> (storefront): the admin list sorts on the lowest
/// variant price, the storefront on its own display price.
/// </summary>
public enum AdminProductSortOption
{
    NameAToZ = 0,
    NameZToA = 1,
    NewestFirst = 2,
    OldestFirst = 3,
    LowestVariantPriceAsc = 4,
    LowestVariantPriceDesc = 5,
}

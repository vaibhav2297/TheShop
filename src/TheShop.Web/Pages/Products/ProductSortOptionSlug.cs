using TheShop.Domain.Enums;

namespace TheShop.Web.Pages.Products;

/// <summary>
/// The single stable mapping between <see cref="ProductSortOption"/> and its URL slug. Slugs keep
/// the brittle numeric enum out of shareable links, so reordering the enum never changes an
/// existing URL's meaning. An unknown or missing slug resolves to the default,
/// <see cref="ProductSortOption.NewestFirst"/>.
/// </summary>
public static class ProductSortOptionSlug
{
    private const string Newest = "newest";
    private const string PriceAsc = "price-asc";
    private const string PriceDesc = "price-desc";
    private const string NameAsc = "name-asc";
    private const string NameDesc = "name-desc";

    /// <summary>Returns the stable URL slug for <paramref name="sort"/>.</summary>
    public static string ToSlug(this ProductSortOption sort) => sort switch
    {
        ProductSortOption.PriceLowToHigh => PriceAsc,
        ProductSortOption.PriceHighToLow => PriceDesc,
        ProductSortOption.NameAToZ => NameAsc,
        ProductSortOption.NameZToA => NameDesc,
        _ => Newest,
    };

    /// <summary>
    /// Resolves a URL slug back to a <see cref="ProductSortOption"/>, falling back to
    /// <see cref="ProductSortOption.NewestFirst"/> for an unknown or <c>null</c> slug.
    /// </summary>
    public static ProductSortOption FromSlug(string? slug) => slug switch
    {
        PriceAsc => ProductSortOption.PriceLowToHigh,
        PriceDesc => ProductSortOption.PriceHighToLow,
        NameAsc => ProductSortOption.NameAToZ,
        NameDesc => ProductSortOption.NameZToA,
        _ => ProductSortOption.NewestFirst,
    };
}

namespace TheShop.Web.Common.Sorting;

/// <summary>
/// The canonical URL slug for every sort concept the app offers, declared once so a single concept
/// carries a single token everywhere it appears. Sorting by name descending means the same thing on
/// the storefront and in the admin console, so <c>name-desc</c> must not become <c>name-za</c> in
/// one of them — a reader editing a shared link by hand, and any future feature adopting the same
/// order, both depend on the token being predictable.
/// </summary>
/// <remarks>
/// These are a URL contract, not an implementation detail: once a link carrying one has been shared,
/// changing the value silently changes what that link means. Treat them as append-only.
/// </remarks>
public static class SortSlugs
{
    /// <summary>Most recently created first.</summary>
    public const string Newest = "newest";

    /// <summary>Name, ascending (A→Z).</summary>
    public const string NameAsc = "name-asc";

    /// <summary>Name, descending (Z→A).</summary>
    public const string NameDesc = "name-desc";

    /// <summary>Least recently created first.</summary>
    public const string Oldest = "oldest";

    /// <summary>Price, ascending (low to high).</summary>
    public const string PriceAsc = "price-asc";

    /// <summary>Price, descending (high to low).</summary>
    public const string PriceDesc = "price-desc";
}

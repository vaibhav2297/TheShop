namespace TheShop.Application.Features.Products;

/// <summary>
/// The known filter group keys (<c>FilterGroupDto.Key</c> / <c>AppliedFilterDto.Key</c>) for
/// the product catalogue. Infrastructure's filter-definition registry is expected to key its
/// entries off these same constants so the Application-side validator and the backend-driven
/// filter groups never drift apart. Flavour/Nicotine are retired (RULE-19, Decision 3) —
/// <c>get_catalogue_filters()</c> now publishes generic per-product option types instead, which
/// the product-catalogue feature will later build dynamic filter controls from.
/// </summary>
public static class ProductFilterKeys
{
    public const string Category = "category";
    public const string Brand = "brand";
    public const string Price = "price";

    /// <summary>
    /// Keys valid on an incoming <c>AppliedFilterDto.Key</c> — the multi-select groups only.
    /// <see cref="Price"/> is not selectable this way; it arrives via the query's explicit
    /// <c>PriceMin</c>/<c>PriceMax</c> fields instead.
    /// </summary>
    public static readonly IReadOnlySet<string> SelectableKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Category, Brand };
}

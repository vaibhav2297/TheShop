using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Deserialization target for the <c>get_admin_product_filters()</c> RPC — the admin
/// manage-products page's brand/category options and variant-aware price bounds. Not a table
/// model.
/// </summary>
internal sealed class AdminProductFiltersRecord
{
    [JsonProperty("brands")]
    public IReadOnlyList<FilterLookupRecord> Brands { get; set; } = [];

    [JsonProperty("categories")]
    public IReadOnlyList<FilterLookupRecord> Categories { get; set; } = [];

    [JsonProperty("price_min")]
    public decimal PriceMin { get; set; }

    [JsonProperty("price_max")]
    public decimal PriceMax { get; set; }
}

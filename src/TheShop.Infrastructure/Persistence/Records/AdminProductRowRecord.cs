using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// One row of the <c>admin_products_page(...)</c> RPC response — a fully-flattened admin
/// manage-products list row, its variant price range/count already rolled up server-side, plus
/// the total match count carried on every row (<c>count(*) OVER ()</c>) so the caller reads the
/// page total without a second round-trip.
/// </summary>
internal sealed class AdminProductRowRecord
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("sku")]
    public string Sku { get; set; } = string.Empty;

    [JsonProperty("brand_id")]
    public Guid BrandId { get; set; }

    [JsonProperty("brand_name")]
    public string BrandName { get; set; } = string.Empty;

    [JsonProperty("category_id")]
    public Guid CategoryId { get; set; }

    [JsonProperty("category_name")]
    public string CategoryName { get; set; } = string.Empty;

    [JsonProperty("currency")]
    public string Currency { get; set; } = "CAD";

    [JsonProperty("is_published")]
    public bool IsPublished { get; set; }

    [JsonProperty("primary_image_key")]
    public string? PrimaryImageKey { get; set; }

    [JsonProperty("variant_count")]
    public int VariantCount { get; set; }

    [JsonProperty("min_price")]
    public decimal? MinPrice { get; set; }

    [JsonProperty("max_price")]
    public decimal? MaxPrice { get; set; }

    [JsonProperty("total_count")]
    public long TotalCount { get; set; }
}

using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// One row of the <c>brand_product_counts(uuid[])</c> RPC response — the authoritative
/// per-brand product count (published or not, RULE-6) that RLS alone cannot answer.
/// </summary>
internal sealed class BrandProductCountRecord
{
    [JsonProperty("brand_id")]
    public Guid BrandId { get; set; }

    [JsonProperty("product_count")]
    public long ProductCount { get; set; }
}

using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// One row of the <c>category_product_counts(uuid[])</c> RPC response — the authoritative
/// per-category product count (published or not, RULE-6) that RLS alone cannot answer.
/// </summary>
internal sealed class CategoryProductCountRecord
{
    [JsonProperty("category_id")]
    public Guid CategoryId { get; set; }

    [JsonProperty("product_count")]
    public long ProductCount { get; set; }
}

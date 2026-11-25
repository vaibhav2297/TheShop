using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>Published card returned by the authorized product RPC.</summary>
internal sealed class FreshDropsProductRecord
{
    [JsonProperty("id")]
    public Guid Id { get; set; }
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    [JsonProperty("brand_name")]
    public string BrandName { get; set; } = string.Empty;
    [JsonProperty("display_price")]
    public decimal DisplayPrice { get; set; }
    [JsonProperty("currency")]
    public string Currency { get; set; } = "CAD";
    [JsonProperty("image_key")]
    public string? ImageKey { get; set; }
}

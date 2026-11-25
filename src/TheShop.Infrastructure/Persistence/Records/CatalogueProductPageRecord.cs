using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

internal sealed class CatalogueProductPageRecord
{
    [JsonProperty("items")]
    public IReadOnlyList<CatalogueProductRowRecord> Items { get; set; } = [];

    [JsonProperty("total_count")]
    public int TotalCount { get; set; }
}

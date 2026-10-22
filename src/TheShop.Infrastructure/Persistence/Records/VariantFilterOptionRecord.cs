using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

internal sealed class VariantFilterOptionRecord
{
    [JsonProperty("value")]
    public string Value { get; set; } = string.Empty;

    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;
}

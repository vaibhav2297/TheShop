using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Deserialization target for the <c>save_product</c> RPC's single-row result: the product's id
/// (echoed back) and its new <c>updated_at</c> concurrency token.
/// </summary>
internal sealed class SaveProductResultRecord
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

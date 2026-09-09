using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// One row of the <c>delete_products(uuid[])</c> RPC response — one entry per requested id,
/// telling the caller whether that product was actually deleted (RULE-3/RULE-4 partial success)
/// and, when it was, the image storage keys to dispose (RULE-8).
/// </summary>
internal sealed class DeleteProductsResultRecord
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("reference_count")]
    public long ReferenceCount { get; set; }

    [JsonProperty("deleted")]
    public bool Deleted { get; set; }

    [JsonProperty("image_keys")]
    public IReadOnlyList<string> ImageKeys { get; set; } = [];
}

using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// One row of the <c>delete_brands(uuid[])</c> RPC response — one entry per requested id, telling
/// the caller whether that brand was actually deleted (RULE-13 partial success) and, when it was,
/// the logo storage key to dispose (RULE-11).
/// </summary>
internal sealed class DeleteBrandsResultRecord
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("logo_path")]
    public string? LogoPath { get; set; }

    [JsonProperty("product_count")]
    public long ProductCount { get; set; }

    [JsonProperty("deleted")]
    public bool Deleted { get; set; }
}

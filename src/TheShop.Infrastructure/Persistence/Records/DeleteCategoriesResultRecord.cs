using Newtonsoft.Json;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// One row of the <c>delete_categories(uuid[])</c> RPC response — one entry per requested id,
/// telling the caller whether that category was actually deleted (RULE-15 partial success) and,
/// when it was, the image storage key to dispose (RULE-11).
/// </summary>
internal sealed class DeleteCategoriesResultRecord
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("image_path")]
    public string? ImagePath { get; set; }

    [JsonProperty("product_count")]
    public long ProductCount { get; set; }

    [JsonProperty("deleted")]
    public bool Deleted { get; set; }
}

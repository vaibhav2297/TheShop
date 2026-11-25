using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>Authorized Fresh Drops row returned by the admin RPC.</summary>
[Table("fresh_drops")]
internal sealed class FreshDropsRecord : BaseModel
{
    [PrimaryKey("id", true), JsonProperty("id")]
    public short Id { get; set; } = 1;
    [Column("title"), JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
    [Column("button_label"), JsonProperty("button_label")]
    public string? ButtonLabel { get; set; }
    [Column("button_link"), JsonProperty("button_link")]
    public string? ButtonLink { get; set; }
    [Column("selection_mode"), JsonProperty("selection_mode")]
    public string SelectionMode { get; set; } = "newest";
    [Column("manual_product_ids"), JsonProperty("manual_product_ids")]
    public List<Guid> ManualProductIds { get; set; } = [];
    [Column("is_visible"), JsonProperty("is_visible")]
    public bool IsVisible { get; set; }
}

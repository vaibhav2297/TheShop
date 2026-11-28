using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>The singleton homepage brand row.</summary>
[Table("homepage_brand_section")]
internal sealed class HomepageBrandRecord : BaseModel
{
    [PrimaryKey("id", true), JsonProperty("id")]
    public short Id { get; set; } = 1;
    [Column("title"), JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
    [Column("button_label"), JsonProperty("button_label")]
    public string ButtonLabel { get; set; } = string.Empty;
    [Column("button_link"), JsonProperty("button_link")]
    public string ButtonLink { get; set; } = string.Empty;
    [Column("brand_ids"), JsonProperty("brand_ids")]
    public List<Guid> BrandIds { get; set; } = [];
    [Column("is_visible"), JsonProperty("is_visible")]
    public bool IsVisible { get; set; }
}

using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>The singleton homepage category row.</summary>
[Table("homepage_category_section")]
internal sealed class HomepageCategoryRecord : BaseModel
{
    [PrimaryKey("id", true), JsonProperty("id")]
    public short Id { get; set; } = 1;
    [Column("title"), JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
    [Column("button_label"), JsonProperty("button_label")]
    public string ButtonLabel { get; set; } = string.Empty;
    [Column("button_link"), JsonProperty("button_link")]
    public string ButtonLink { get; set; } = string.Empty;
    [Column("category_ids"), JsonProperty("category_ids")]
    public List<Guid> CategoryIds { get; set; } = [];
    [Column("is_visible"), JsonProperty("is_visible")]
    public bool IsVisible { get; set; }
}

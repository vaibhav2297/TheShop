using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>Private singleton persistence row, also returned by the authorized read RPC.</summary>
[Table("hero_banners")]
internal sealed class HeroBannerRecord : BaseModel
{
    [PrimaryKey("id", true), JsonProperty("id")]
    public short Id { get; set; } = 1;
    [Column("heading"), JsonProperty("heading")]
    public string Heading { get; set; } = string.Empty;
    [Column("description"), JsonProperty("description")]
    public string? Description { get; set; }
    [Column("button_label"), JsonProperty("button_label")]
    public string? ButtonLabel { get; set; }
    [Column("button_link"), JsonProperty("button_link")]
    public string? ButtonLink { get; set; }
    [Column("desktop_image_key"), JsonProperty("desktop_image_key")]
    public string DesktopImageKey { get; set; } = string.Empty;
    [Column("mobile_image_key"), JsonProperty("mobile_image_key")]
    public string? MobileImageKey { get; set; }
    [Column("is_visible"), JsonProperty("is_visible")]
    public bool IsVisible { get; set; }
}


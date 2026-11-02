using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>Private fixed-column Featured row returned by the administrative RPC.</summary>
[Table("featured_sections")]
internal sealed class FeaturedSectionRecord : BaseModel
{
    [PrimaryKey("id", true), JsonProperty("id")]
    public short Id { get; set; } = 1;
    [Column("title"), JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
    [Column("tile1_label"), JsonProperty("tile1_label")]
    public string? Tile1Label { get; set; }
    [Column("tile1_title"), JsonProperty("tile1_title")]
    public string Tile1Title { get; set; } = string.Empty;
    [Column("tile1_button_label"), JsonProperty("tile1_button_label")]
    public string? Tile1ButtonLabel { get; set; }
    [Column("tile1_button_link"), JsonProperty("tile1_button_link")]
    public string? Tile1ButtonLink { get; set; }
    [Column("tile1_image_key"), JsonProperty("tile1_image_key")]
    public string Tile1ImageKey { get; set; } = string.Empty;
    [Column("tile2_label"), JsonProperty("tile2_label")]
    public string? Tile2Label { get; set; }
    [Column("tile2_title"), JsonProperty("tile2_title")]
    public string Tile2Title { get; set; } = string.Empty;
    [Column("tile2_button_label"), JsonProperty("tile2_button_label")]
    public string? Tile2ButtonLabel { get; set; }
    [Column("tile2_button_link"), JsonProperty("tile2_button_link")]
    public string? Tile2ButtonLink { get; set; }
    [Column("tile2_image_key"), JsonProperty("tile2_image_key")]
    public string Tile2ImageKey { get; set; } = string.Empty;
    [Column("is_visible"), JsonProperty("is_visible")]
    public bool IsVisible { get; set; }
}

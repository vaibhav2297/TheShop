using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>Stable public slug assigned to a live product.</summary>
[Table("product_routes")]
internal sealed class ProductRouteRecord : BaseModel
{
    [PrimaryKey("slug", false)] public string Slug { get; set; } = string.Empty;
    [Column("product_id")] public Guid? ProductId { get; set; }
}

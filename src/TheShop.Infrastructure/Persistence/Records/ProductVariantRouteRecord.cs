using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>Stable slug assigned to a live variant under its parent product slug.</summary>
[Table("product_variant_routes")]
internal sealed class ProductVariantRouteRecord : BaseModel
{
    [Column("product_slug")] public string ProductSlug { get; set; } = string.Empty;
    [Column("slug")] public string Slug { get; set; } = string.Empty;
    [Column("variant_id")] public Guid? VariantId { get; set; }
}

using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>One saved, ordered image assignment for a product variant.</summary>
[Table("product_variant_images")]
internal sealed class ProductVariantImageRecord : BaseModel
{
    [Column("product_id")] public Guid ProductId { get; set; }
    [Column("variant_id")] public Guid VariantId { get; set; }
    [Column("image_id")] public Guid ImageId { get; set; }
    [Column("position")] public int Position { get; set; }
}

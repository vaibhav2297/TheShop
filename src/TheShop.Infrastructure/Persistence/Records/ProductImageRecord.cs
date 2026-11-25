using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>product_images</c> table. Read-only from Postgrest's
/// perspective — every write goes through the <c>save_product</c> RPC.
/// </summary>
[Table("product_images")]
internal sealed class ProductImageRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("object_key")]
    public string ObjectKey { get; set; } = string.Empty;

    [Column("position")]
    public int Position { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }
}

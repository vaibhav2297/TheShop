using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>product_variants</c> table. Read-only from Postgrest's
/// perspective — every write goes through the <c>save_product</c> RPC.
/// </summary>
[Table("product_variants")]
internal sealed class ProductVariantRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("sku")]
    public string Sku { get; set; } = string.Empty;

    [Column("original_price")]
    public decimal? OriginalPrice { get; set; }

    [Column("sale_price")]
    public decimal? SalePrice { get; set; }

    [Column("is_available")]
    public bool IsAvailable { get; set; }

    [Column("image_id")]
    public Guid? ImageId { get; set; }

    [Column("position")]
    public int Position { get; set; }
}

using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>product_sku_registry</c> table — the one shared SKU namespace
/// spanning products and variants (RULE-8). Read-only from Postgrest's perspective; every write
/// goes through the <c>save_product</c> RPC.
/// </summary>
[Table("product_sku_registry")]
internal sealed class ProductSkuRegistryRecord : BaseModel
{
    [PrimaryKey("sku_normalized", false)]
    public string SkuNormalized { get; set; } = string.Empty;

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("variant_id")]
    public Guid? VariantId { get; set; }
}

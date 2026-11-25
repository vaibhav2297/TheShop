using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>product_variant_option_values</c> join table. Read-only from
/// Postgrest's perspective — every write goes through the <c>save_product</c> RPC.
/// </summary>
[Table("product_variant_option_values")]
internal sealed class ProductVariantOptionValueRecord : BaseModel
{
    [Column("variant_id")]
    public Guid VariantId { get; set; }

    [Column("option_value_id")]
    public Guid OptionValueId { get; set; }
}

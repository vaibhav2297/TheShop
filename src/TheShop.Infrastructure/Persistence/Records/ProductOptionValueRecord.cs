using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>product_option_values</c> table. Read-only from Postgrest's
/// perspective — every write goes through the <c>save_product</c> RPC.
/// </summary>
[Table("product_option_values")]
internal sealed class ProductOptionValueRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("option_type_id")]
    public Guid OptionTypeId { get; set; }

    [Column("value")]
    public string Value { get; set; } = string.Empty;

    [Column("position")]
    public int Position { get; set; }
}

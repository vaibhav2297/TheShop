using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>product_option_types</c> table. Read-only from Postgrest's
/// perspective — every write goes through the <c>save_product</c> RPC.
/// </summary>
[Table("product_option_types")]
internal sealed class ProductOptionTypeRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("position")]
    public int Position { get; set; }
}

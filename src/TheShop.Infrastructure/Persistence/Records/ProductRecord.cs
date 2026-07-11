using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>products</c> table. Embeds <see cref="Category"/> and
/// <see cref="Brand"/> via <see cref="ReferenceAttribute"/> resource embedding so a single
/// query returns the joined data the domain <c>Product</c> aggregate needs.
/// Used only by <see cref="Repositories.SupabaseProductRepository"/> and
/// <see cref="Mappers.ProductMapper"/>.
/// </summary>
[Table("products")]
internal sealed class ProductRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("image_path")]
    public string? ImagePath { get; set; }

    [Column("original_price")]
    public decimal OriginalPrice { get; set; }

    [Column("sale_price")]
    public decimal? SalePrice { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "CAD";

    [Column("category_id")]
    public Guid CategoryId { get; set; }

    [Column("brand_id")]
    public Guid BrandId { get; set; }

    [Column("flavour")]
    public string? Flavour { get; set; }

    [Column("nicotine_strength_mg")]
    public int? NicotineStrengthMg { get; set; }

    [Column("stock_quantity")]
    public int StockQuantity { get; set; }

    [Column("is_published")]
    public bool IsPublished { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Reference(typeof(CategoryRecord))]
    public CategoryRecord? Category { get; set; }

    [Reference(typeof(BrandRecord))]
    public BrandRecord? Brand { get; set; }
}

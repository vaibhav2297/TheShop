using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>products</c> table. Embeds <see cref="Category"/> and
/// <see cref="Brand"/> via <see cref="ReferenceAttribute"/> resource embedding so a single
/// query returns the joined data the domain <c>Product</c> aggregate needs. Read-only — every
/// write goes through the <c>save_product</c> RPC (plan Decision 1), never a Postgrest
/// insert/update on this record. Used only by <see cref="Repositories.SupabaseProductRepository"/>
/// and <see cref="Mappers.ProductMapper"/>.
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

    [Column("sku")]
    public string Sku { get; set; } = string.Empty;

    [Column("original_price")]
    public decimal? OriginalPrice { get; set; }

    [Column("sale_price")]
    public decimal? SalePrice { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "CAD";

    [Column("category_id")]
    public Guid CategoryId { get; set; }

    [Column("brand_id")]
    public Guid BrandId { get; set; }

    [Column("is_published")]
    public bool IsPublished { get; set; }

    [Column("min_variant_price")]
    public decimal? MinVariantPrice { get; set; }

    [Column("has_sellable_variant")]
    public bool HasSellableVariant { get; set; }

    // DateTimeOffset, not DateTime: Postgrest's DateTime converter silently reinterprets the
    // timestamptz value in the local runtime's time zone and drops sub-second precision, which
    // corrupts UpdatedAt's round trip as the save_product RPC's optimistic-concurrency token
    // (GetForEditAsync -> ToString("O") -> expected_updated_at) — every UPDATE's WHERE clause
    // failed to match, so it raised "concurrent modification" unconditionally. DateTimeOffset
    // preserves the real instant and full precision.
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Reference(typeof(CategoryRecord))]
    public CategoryRecord? Category { get; set; }

    [Reference(typeof(BrandRecord))]
    public BrandRecord? Brand { get; set; }
}

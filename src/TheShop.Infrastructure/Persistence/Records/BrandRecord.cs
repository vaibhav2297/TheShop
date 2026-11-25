using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>brands</c> table. Used by
/// <see cref="Repositories.SupabaseProductRepository"/>/<see cref="Mappers.ProductMapper"/> for
/// reads, and by <see cref="Repositories.SupabaseBrandRepository"/>/<see cref="Mappers.BrandMapper"/>
/// for writes.
/// </summary>
[Table("brands")]
internal sealed class BrandRecord : BaseModel
{
    // Brand.Create generates the Id in-memory (Domain), so it must be sent on insert —
    // unlike the read-only reference-data era of this record, where the DB always generated it.
    [PrimaryKey("id", true)]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("logo_path")]
    public string? LogoPath { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

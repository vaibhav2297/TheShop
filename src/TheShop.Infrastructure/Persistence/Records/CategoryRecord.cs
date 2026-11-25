using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>categories</c> table. Used by
/// <see cref="Repositories.SupabaseProductRepository"/>/<see cref="Mappers.ProductMapper"/> for
/// reads, and by <see cref="Repositories.SupabaseCategoryRepository"/>/
/// <see cref="Mappers.CategoryMapper"/> for writes.
/// </summary>
[Table("categories")]
internal sealed class CategoryRecord : BaseModel
{
    // Category.Create generates the Id in-memory (Domain), so it must be sent on insert —
    // unlike the read-only reference-data era of this record, where the DB always generated it.
    [PrimaryKey("id", true)]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("image_path")]
    public string? ImagePath { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

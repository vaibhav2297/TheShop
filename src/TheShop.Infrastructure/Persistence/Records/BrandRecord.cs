using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>brands</c> table. Used only by
/// <see cref="Repositories.SupabaseProductRepository"/> and <see cref="Mappers.ProductMapper"/>.
/// </summary>
[Table("brands")]
internal sealed class BrandRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("slug")]
    public string Slug { get; set; } = string.Empty;
}

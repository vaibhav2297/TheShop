using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>categories</c> table. Used only by
/// <see cref="Repositories.SupabaseProductRepository"/> and <see cref="Mappers.ProductMapper"/>.
/// </summary>
[Table("categories")]
internal sealed class CategoryRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("slug")]
    public string Slug { get; set; } = string.Empty;
}

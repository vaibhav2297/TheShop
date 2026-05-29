using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheShop.Infrastructure.Persistence.Records;

/// <summary>
/// Postgrest row model for the <c>customers</c> table. Used only by
/// <see cref="Repositories.SupabaseCustomerRepository"/> and <see cref="Mappers.CustomerMapper"/>.
/// </summary>
[Table("customers")]
public sealed class CustomerRecord : BaseModel
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [Column("date_of_birth")]
    public DateTime DateOfBirth { get; set; }

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

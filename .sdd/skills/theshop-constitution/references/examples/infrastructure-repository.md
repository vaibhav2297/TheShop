# Example — Infrastructure repository

Canonical Supabase repository. Shows the **implement Application interface → query records → map to entity** pattern enforced by Rule 3.

```csharp
namespace TheShop.Infrastructure.Persistence.Records;

[Table("carts")]
internal sealed class CartRecord : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("customer_id")] public Guid CustomerId { get; set; }
    [Column("items")] public string ItemsJson { get; set; } = "[]";   // serialized — provider concern only
}
```

```csharp
namespace TheShop.Infrastructure.Persistence.Mappers;

internal static class CartMapper
{
    public static Cart ToDomain(this CartRecord record) =>
        Cart.Rehydrate(record.Id, record.CustomerId, DeserializeItems(record.ItemsJson));

    public static CartRecord ToRecord(this Cart cart) => new()
    {
        Id = cart.Id,
        CustomerId = cart.CustomerId,
        ItemsJson = SerializeItems(cart.Items),
    };

    private static List<CartItem> DeserializeItems(string json) { /* ... */ }
    private static string SerializeItems(IReadOnlyList<CartItem> items) { /* ... */ }
}
```

```csharp
namespace TheShop.Infrastructure.Persistence.Repositories;

public sealed class SupabaseCartRepository(Supabase.Client client) : ICartRepository
{
    public async Task<Cart?> GetForUserAsync(Guid customerId, CancellationToken ct)
    {
        var response = await client
            .From<CartRecord>()
            .Where(x => x.CustomerId == customerId)
            .Single(ct);

        return response?.ToDomain();
    }

    public async Task SaveAsync(Cart cart, CancellationToken ct)
    {
        var record = cart.ToRecord();
        await client.From<CartRecord>().Upsert(record, cancellationToken: ct);
    }
}
```

Highlights:
- `CartRecord` is **`internal sealed`** and lives in **Infrastructure** — Supabase attributes (`[Table]`, `[PrimaryKey]`, `[Column]`) never appear in Domain (Rule 2). Records stay `internal` so they cannot leak out of the layer; `From<CartRecord>()` still compiles because the generic is instantiated inside the Infrastructure assembly.
- Mapper extension methods translate between the record (provider shape) and the entity (domain shape). Domain never sees the record.
- Repository is `sealed`, primary constructor takes the SDK client via DI.
- Concrete repository name embeds the provider (`SupabaseCartRepository`); the Application interface (`ICartRepository`) is provider-neutral.
- `CancellationToken` is accepted and propagated to the SDK call.
- `Result<T>` is **not** typically used inside repository methods — repositories throw on infrastructural failure and return entities or `null`. Result wrapping happens at the handler boundary.

# Example — Domain entity

Canonical Domain entity. Shows the **validate → mutate → expose-readonly** pattern enforced by Rule 6. No external SDKs, no persistence concerns, no setters — only methods that enforce invariants.

```csharp
namespace TheShop.Domain.Entities;

public class Cart
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    private readonly List<CartItem> _items = [];
    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    private Cart() { }                                       // EF / record-rehydration ctor

    public static Cart CreateFor(Guid customerId) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
    };

    public void AddItem(Product product, int quantity)
    {
        if (quantity <= 0)
            throw new DomainException(nameof(Strings.Quantity_MustBePositive));

        if (product.Stock < quantity)
            throw new InsufficientStockException(product.Id);

        if (_items.Count >= 20 && _items.All(i => i.ProductId != product.Id))
            throw new CartCapacityExceededException();

        var existing = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing is not null)
            existing.IncreaseQuantity(quantity);
        else
            _items.Add(new CartItem(product.Id, product.Price, quantity));
    }

    public Money TotalPrice() =>
        _items.Aggregate(Money.Zero, (sum, item) => sum + item.Subtotal);
}
```

Highlights:
- `private set` on identity properties; mutation happens only through `AddItem` / similar methods.
- `_items` is `private readonly List<T>` exposed as `IReadOnlyList<T>`.
- Invariants live on the entity — handlers never re-check them.
- Domain exceptions carry resource keys via `nameof(Strings.{Key})` (Rule 12). Where the exception type already encodes the meaning (`InsufficientStockException`, `CartCapacityExceededException`), the type itself is the message key.
- Collection expression `[]` per Rule 9 / coding standards.
- No `using Supabase;`, no `[Column]`, no SDK types anywhere.

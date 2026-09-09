### 6. Report the produced API surface

End your response with this exact structured summary so the orchestrator can pass your output to the Application agent:

```
## Domain implementation summary — {feature_name}

**Plan section read:** Sections 4 (Data Model), 5 (Design Decisions), 9 (Domain exceptions) of `.specs/{feature_name}/plan.md`

**Files created/modified:**
- `src/TheShop.Domain/Entities/Cart.cs` (new)
- `src/TheShop.Domain/Entities/CartItem.cs` (new)
- `src/TheShop.Domain/Exceptions/CartCapacityExceededException.cs` (new)

**Public API produced (signatures only — paste these into the Application agent's prompt):**

```csharp
namespace TheShop.Domain.Entities;

public class Cart
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public IReadOnlyList<CartItem> Items { get; }
    public static Cart CreateFor(Guid customerId);
    public void AddItem(Product product, int quantity);
    public void RemoveItem(Guid productId);
    public Money TotalPrice();
}

public class CartItem
{
    public Guid ProductId { get; }
    public Money UnitPrice { get; }
    public int Quantity { get; private set; }
    public Money Subtotal { get; }
    public void IncreaseQuantity(int delta);
}

namespace TheShop.Domain.Exceptions;

public class CartCapacityExceededException : DomainException
{
    public CartCapacityExceededException();
    // MessageKey = nameof(Strings.CartCapacityExceeded)
}
```

**Build status:** ✅ `dotnet build TheShop.Domain` succeeded with 0 warnings / 0 errors.

**Open questions / TODOs:**
- {Anything ambiguous in the plan that you guessed at. List each. If none, write "None."}
```

The "Public API produced" block is the contract the next layer reads. Be exact — paste real signatures, not approximations.

---

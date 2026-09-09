### 8. Report the produced API surface

End your response with this structured summary:

```
## Application implementation summary — {feature_name}

**Plan sections read:** 3, 4 (DTOs), 6, 7 (Phase 2), 9 of `.specs/{feature_name}/plan.md`

**Files created/modified:**
- `src/TheShop.Application/Features/Cart/Commands/AddToCart/AddToCartCommand.cs` (new)
- `src/TheShop.Application/Features/Cart/Commands/AddToCart/AddToCartHandler.cs` (new)
- `src/TheShop.Application/Features/Cart/Commands/AddToCart/AddToCartCommandValidator.cs` (new)
- `src/TheShop.Application/Features/Cart/DTOs/CartDto.cs` (new)
- `src/TheShop.Application/Common/Interfaces/ICartRepository.cs` (new)
- `src/TheShop.Web/Resources/Strings.resx` (4 keys added)
- `src/TheShop.Web/Resources/Strings.fr.resx` (4 keys added with [TODO])

**Interfaces produced (Infrastructure agent implements these):**

```csharp
namespace TheShop.Application.Common.Interfaces;

public interface ICartRepository
{
    Task<Cart?> GetForUserAsync(Guid customerId, CancellationToken ct);
    Task SaveAsync(Cart cart, CancellationToken ct);
}
```

**DTOs and Commands produced (Web agent consumes these):**

```csharp
namespace TheShop.Application.Features.Cart.Commands;
public record AddToCartCommand(Guid ProductId, int Quantity) : IRequest<Result<CartDto>>;

namespace TheShop.Application.Features.Cart.DTOs;
public record CartDto(Guid Id, IReadOnlyList<CartItemDto> Items, decimal Subtotal);
public record CartItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);
```

**Error keys added to Strings.resx:**
- `ProductNotFound` — "Product not found."
- `CartCapacityExceeded` — "Your cart is full. Remove an item to add another."
- `Quantity_OutOfRange` — "Please choose a quantity between 1 and 99."
- `InsufficientStock` — "Not enough stock available."

**Build status:** ✅ `dotnet build TheShop.Application` succeeded with 0 warnings / 0 errors.

**Open questions / TODOs:**
- {Anything ambiguous. If none, write "None."}
```

The "Interfaces produced" and "DTOs and Commands produced" blocks are what the orchestrator passes to the Infrastructure and Web agents — be exact.

---

# XML Documentation Conventions — The Shop

> Companion to `ARCHITECTURE.md` and `DESIGN.md`. This document is the source of truth for **what to document** and **how to phrase it** in this codebase. Loaded by the `shop-code-documenter` agent on every run.

The project rule from `CLAUDE.md` is: **default to no comments**. XML doc comments are the exception — they exist for tooling (IntelliSense, generated docs, MCP component pickers) and tell readers what the *contract* is, not what the code does line-by-line. Inline `//` comments are still discouraged.

---

## What to document

| Where | Required? |
|---|---|
| Public types in any layer (class, record, interface, enum, struct) | ✅ Required — `<summary>` |
| Public methods, properties, events on public types | ✅ Required — `<summary>`, with `<param>` / `<returns>` / `<exception>` where they add information |
| MediatR Commands and Queries (`record ... : IRequest<...>`) | ✅ Required — `<summary>` on the record describing the use case |
| MediatR Handlers | ✅ Required — `<summary>` on the class + on `Handle(...)` |
| Repository and service interfaces (`I*Repository`, `I*Service`) | ✅ Required — `<summary>` on the interface + on every method |
| Domain entities — every public behavior-bearing method | ✅ Required |
| Domain entity factory methods (`Create`, `Register`, `CreateFor`) | ✅ Required |
| Value object factories and public methods | ✅ Required |
| Domain exceptions | ✅ Required — `<summary>` on the type only |
| DTOs (`record CustomerDto(...)`) | 🟡 Optional — only if the DTO's purpose or constraints aren't obvious from the name |
| Internal members | ❌ Skip — not part of the public contract |
| Private members | ❌ Skip |
| Obvious accessors on records (`public Guid Id { get; init; }`) | ❌ Skip — name is the doc |
| Test classes (anything under `tests/`) | ❌ Skip |
| Auto-generated code (`*.g.cs`, `*.Designer.cs`) | ❌ Skip |
| `.razor` markup blocks (the `@code { }` block in a single-file component) | ❌ Skip |

---

## How to phrase it

### The golden rule

**The summary describes the *contract*, not the *implementation*.** A future maintainer reading your `<summary>` should know what the method *promises* — not how it currently delivers on that promise.

```csharp
// ✅ Good — contract
/// <summary>
/// Returns the customer's cart, creating a new empty one if they don't have one yet.
/// </summary>

// ❌ Bad — implementation detail (rots when implementation changes)
/// <summary>
/// Queries the carts table by customer ID, and if no row is found, inserts a new one.
/// </summary>
```

### The anti-restatement test

If your `<summary>` is the method name converted to prose, delete the summary. Either the name needs to be better, or the doc adds nothing.

```csharp
// ❌ Bad — restates the name
/// <summary>Adds an item.</summary>
public void AddItem(...) { }

// ✅ Good — adds information about the contract
/// <summary>
/// Adds an item to the cart, enforcing the 20-distinct-item invariant.
/// </summary>
public void AddItem(...) { }
```

### Sentence shape

- One sentence if you can; two short ones max.
- Active voice. "Returns the cart" — not "The cart is returned".
- Present tense. "Adds an item" — not "Will add an item".
- No "this method", "this property". The XML tag already says that.

### `<param>` — only when it adds information

If the parameter name is self-explanatory, skip `<param>`. Document params where the *constraint*, *unit*, or *meaning* isn't obvious from the name:

```csharp
// ✅ Worth documenting — the constraint isn't obvious from the type
/// <param name="quantity">How many units to add. Must be positive.</param>
/// <param name="cooldownSeconds">Seconds the caller must wait before re-requesting.</param>

// ❌ Noise — the name says it all
/// <param name="customerId">The customer ID.</param>
/// <param name="email">The email.</param>
```

### `<returns>` — only when it adds information

For methods returning a domain type that matches the method name, skip `<returns>`. Document when the return value has nullability rules, special states, or units the type alone doesn't convey:

```csharp
// ✅ Worth documenting — nullability matters
/// <returns>The cart for this customer, or <c>null</c> if no cart exists yet.</returns>
Task<Cart?> GetForUserAsync(Guid customerId, CancellationToken ct);

// ❌ Noise
/// <returns>A Task.</returns>
Task SaveAsync(Cart cart, CancellationToken ct);
```

### `<exception>` — document the throw contract

Document every exception the method **deliberately** throws (typically `DomainException` subtypes). Don't list theoretical exceptions (`InvalidOperationException`, `ArgumentNullException` from the framework). One `<exception>` per documented throw case.

```csharp
/// <summary>
/// Increases the line quantity for an existing item.
/// </summary>
/// <exception cref="DomainException">
/// Thrown when <paramref name="delta"/> is zero or negative.
/// </exception>
public void IncreaseQuantity(int delta) { ... }
```

### `<see cref="..."/>` and `<seealso>`

Use sparingly. Link to a sibling type when the relationship is non-obvious (e.g., an exception type a method throws, a DTO a Command returns). Don't link for the sake of linking.

```csharp
/// <summary>
/// Adds a product to the cart and returns the updated cart.
/// </summary>
/// <seealso cref="CartDto"/>
public record AddToCartCommand(Guid ProductId, int Quantity) : IRequest<Result<CartDto>>;
```

---

## Canonical examples by layer

### Domain — entity with invariants

```csharp
namespace TheShop.Domain.Entities;

/// <summary>
/// A customer's persistent shopping cart. Items hold their unit price at add-time
/// to prevent price-change surprise at checkout.
/// </summary>
public class Cart
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public IReadOnlyList<CartItem> Items { get; }

    /// <summary>
    /// Creates an empty cart for the given customer.
    /// </summary>
    public static Cart CreateFor(Guid customerId) { ... }

    /// <summary>
    /// Adds an item to the cart, or increases the line quantity if the product
    /// is already present.
    /// </summary>
    /// <param name="quantity">Units to add. Must be positive.</param>
    /// <exception cref="CartCapacityExceededException">
    /// Thrown when adding a new line would exceed 20 distinct items.
    /// </exception>
    /// <exception cref="InsufficientStockException">
    /// Thrown when the requested quantity exceeds the product's available stock.
    /// </exception>
    public void AddItem(Product product, int quantity) { ... }
}
```

### Domain — value object factory

```csharp
namespace TheShop.Domain.ValueObjects;

/// <summary>
/// An RFC-5322-validated email address.
/// </summary>
public sealed record Email
{
    public string Value { get; }

    /// <summary>
    /// Creates an <see cref="Email"/> after validating format.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the input is empty or not a valid email format.
    /// Carries <c>MessageKey = nameof(Strings.Email_Invalid)</c>.
    /// </exception>
    public static Email Create(string value) { ... }
}
```

### Domain — exception

```csharp
namespace TheShop.Domain.Exceptions;

/// <summary>
/// Raised when adding a new line would push the cart above the 20-distinct-item limit.
/// </summary>
public sealed class CartCapacityExceededException : DomainException
{
    public CartCapacityExceededException()
        : base(nameof(Strings.CartCapacityExceeded)) { }
}
```

### Application — Command

```csharp
namespace TheShop.Application.Features.Cart.Commands;

/// <summary>
/// Adds a product to the authenticated customer's cart and returns the updated cart.
/// </summary>
public record AddToCartCommand(Guid ProductId, int Quantity)
    : IRequest<Result<CartDto>>;
```

### Application — Handler

```csharp
/// <summary>
/// Handles <see cref="AddToCartCommand"/>. Loads or creates the customer's cart,
/// applies the domain invariants via <see cref="Cart.AddItem"/>, and persists the result.
/// </summary>
public sealed class AddToCartHandler(
    IProductRepository products,
    ICartRepository carts,
    ICurrentUserService user,
    IMapper mapper) : IRequestHandler<AddToCartCommand, Result<CartDto>>
{
    /// <summary>
    /// Returns <see cref="Result.Ok"/> with the updated cart on success, or
    /// <see cref="Result.Fail"/> with a resource key when the product is missing,
    /// the user is unauthenticated, or a domain invariant is violated.
    /// </summary>
    public async Task<Result<CartDto>> Handle(
        AddToCartCommand cmd, CancellationToken ct) { ... }
}
```

### Application — Repository interface

```csharp
namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Cart"/>. Implementations live in the
/// Infrastructure layer.
/// </summary>
public interface ICartRepository
{
    /// <summary>
    /// Returns the cart for the given customer, or <c>null</c> if none exists.
    /// </summary>
    Task<Cart?> GetForUserAsync(Guid customerId, CancellationToken ct);

    /// <summary>
    /// Inserts a new cart or updates an existing one in place.
    /// </summary>
    Task SaveAsync(Cart cart, CancellationToken ct);
}
```

### Infrastructure — repository

```csharp
namespace TheShop.Infrastructure.Persistence.Repositories;

/// <summary>
/// Supabase-backed implementation of <see cref="ICartRepository"/>.
/// Maps between <see cref="CartRecord"/> rows and <see cref="Cart"/> entities.
/// </summary>
public sealed class SupabaseCartRepository(Supabase.Client client)
    : ICartRepository { ... }
```

### Web — page code-behind

```csharp
namespace TheShop.Web.Pages.Products;

/// <summary>
/// Product detail page. Loads a product by slug, lets the customer choose a
/// quantity, and dispatches <see cref="AddToCartCommand"/>.
/// </summary>
[Route(Routes.Products.Detail)]
public partial class ProductDetail(
    IMediator mediator,
    CartState cart,
    ISnackbar snackbar,
    IStringLocalizer<Strings> localizer) { ... }
```

---

## DTOs — when to document

Most DTOs are self-explanatory. Document only when:

- The DTO encodes a specific shape that isn't obvious (e.g., "this is the projection used by the admin list view, not the customer-facing one").
- One of the properties has a unit, constraint, or special state worth surfacing.

```csharp
// ✅ Worth a summary — purpose isn't obvious from the name alone
/// <summary>
/// Lightweight cart projection sent to the cart icon in the header. Excludes
/// per-item details — use <see cref="CartDto"/> for the full cart page.
/// </summary>
public record CartBadgeDto(int ItemCount, decimal Subtotal);

// ❌ Don't document — purpose is the name
public record CustomerProfileDto(Guid Id, string FirstName, string LastName, string Email);
```

---

## Things to NOT do

- ❌ **Don't document private members.** Even if asked.
- ❌ **Don't add `<remarks>` blocks with narrative explanation.** If it's worth saying, fit it into the `<summary>`. If it doesn't fit, it's probably not worth saying.
- ❌ **Don't add `<example>` blocks.** Code examples belong in tests and docs, not in XML comments.
- ❌ **Don't write `<summary>This class represents a ...</summary>`.** Just describe what it is or does.
- ❌ **Don't add `<copyright>`, `<author>`, or other ceremony tags.** Not used in this codebase.
- ❌ **Don't translate resource keys.** Document the throw using the key name (`MessageKey = nameof(Strings.X)`), not the localized English text.
- ❌ **Don't add inline `//` comments alongside the XML.** Pick one. XML doc covers the contract; inline comments are reserved for non-obvious *why*.

---

## When in doubt

If you can't decide whether a member needs a doc comment, ask: **would a future reader find the contract harder to understand without it?** If yes, write the doc. If no, leave the name to do the work.

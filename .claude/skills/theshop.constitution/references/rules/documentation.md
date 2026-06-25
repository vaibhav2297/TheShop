# Documentation — XML Doc Comments

> Implementation guide for Rule 30 from `SKILL.md`. Tells you **what** to document, **how** to phrase it, and **what not to do**. Loaded by `shop-code-documenter`. The rule statement itself lives in `SKILL.md` — this file does not restate it.

The project default (per `CLAUDE.md`) is **no comments**. XML doc comments are the exception — they exist for tooling (IntelliSense, generated docs, MCP component pickers) and tell readers what the *contract* is, not what the code does line-by-line. Inline `//` comments remain discouraged.

---

## Principles

- Write documentation only for public, protected, internal, and extension APIs unless explicitly requested otherwise.
- Keep documentation concise, precise, and professional.
- Prefer short summaries (1–2 sentences maximum).
- Avoid repeating information already obvious from method, property, class, or parameter names.
- Focus on purpose, behavior, intent, side effects, and important usage notes.
- Preserve meaning and developer value while minimizing verbosity.

---

## What to document

> **Surface rule.** Document the API surface — **public, protected, internal, and extension** members. Private members stay undocumented unless explicitly requested.

| Where | Required? |
|---|---|
| Public, protected, and internal types in any layer (class, record, interface, enum, struct) | ✅ `<summary>` |
| Public, protected, and internal methods, properties, events on those types | ✅ `<summary>` + `<param>` / `<typeparam>` / `<returns>` / `<exception>` where they add information |
| Extension methods | ✅ `<summary>` stating the behaviour added to the extended type |
| Enum members | ✅ `<summary>` on each value whose meaning isn't obvious from its name |
| MediatR Commands and Queries (`record ... : IRequest<...>`) | ✅ `<summary>` on the record describing the use case |
| MediatR Handlers | ✅ `<summary>` on the class + on `Handle(...)` |
| Repository and service interfaces (`I*Repository`, `I*Service`) | ✅ `<summary>` on the interface + on every method |
| Domain entities — every public behaviour-bearing method | ✅ |
| Domain entity factory methods (`Create`, `Register`, `CreateFor`) | ✅ |
| Value object factories and public methods | ✅ |
| Domain exceptions | ✅ `<summary>` on the type only |
| DTOs (`record CustomerDto(...)`) | 🟡 Only when the DTO's purpose or constraints aren't obvious from the name |
| Private members | ❌ Not part of the API surface (unless explicitly requested) |
| Obvious accessors on records (`public Guid Id { get; init; }`) | ❌ Name is the doc |
| Test classes (under `tests/`) | ❌ |
| Auto-generated code (`*.g.cs`, `*.Designer.cs`) | ❌ |
| `.razor` markup blocks (the `@code { }` block in a single-file component) | ❌ |

---

## How to phrase it

### Golden rule

**The summary describes the *contract*, not the *implementation*.** A future maintainer reading your `<summary>` should know what the method *promises* — not how it currently delivers on that promise.

```csharp
// ✅ Contract
/// <summary>
/// Returns the customer's cart, creating a new empty one if they don't have one yet.
/// </summary>

// ❌ Implementation detail (rots when implementation changes)
/// <summary>
/// Queries the carts table by customer ID, and if no row is found, inserts a new one.
/// </summary>
```

### Anti-restatement test

If your `<summary>` is the method name converted to prose, delete the summary. Either the name needs to be better, or the doc adds nothing.

```csharp
// ❌ Restates the name
/// <summary>Adds an item.</summary>
public void AddItem(...) { }

// ✅ Adds information about the contract
/// <summary>
/// Adds an item to the cart, enforcing the 20-distinct-item invariant.
/// </summary>
public void AddItem(...) { }
```

### Sentence shape

- One sentence if you can; two short ones max.
- Active voice. *"Returns the cart"* — not *"The cart is returned"*.
- Present tense. *"Adds an item"* — not *"Will add an item"*.
- No *"this method"*, *"this property"*. The XML tag already says that.

### `<param>` — only when it adds information

Skip when the parameter name is self-explanatory. Document where the *constraint*, *unit*, or *meaning* isn't obvious from the name:

```csharp
// ✅ Worth documenting — constraint isn't obvious from the type
/// <param name="quantity">How many units to add. Must be positive.</param>
/// <param name="cooldownSeconds">Seconds the caller must wait before re-requesting.</param>

// ❌ Noise — name says it all
/// <param name="customerId">The customer ID.</param>
/// <param name="email">The email.</param>
```

### `<returns>` — only when it adds information

Skip when the return type and method name say it. Document nullability rules, special states, or units the type alone doesn't convey:

```csharp
// ✅ Nullability matters
/// <returns>The cart for this customer, or <c>null</c> if no cart exists yet.</returns>
Task<Cart?> GetForUserAsync(Guid customerId, CancellationToken ct);

// ❌ Noise
/// <returns>A Task.</returns>
Task SaveAsync(Cart cart, CancellationToken ct);
```

### `<exception>` — document the throw contract

Document every exception the method **deliberately** throws (typically `DomainException` subtypes). Don't list theoretical framework exceptions (`InvalidOperationException`, `ArgumentNullException`). One `<exception>` per documented throw case.

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

Use sparingly. Link to a sibling type when the relationship is non-obvious (an exception type a method throws, a DTO a Command returns). Don't link for the sake of linking.

```csharp
/// <summary>
/// Adds a product to the cart and returns the updated cart.
/// </summary>
/// <seealso cref="CartDto"/>
public record AddToCartCommand(Guid ProductId, int Quantity) : IRequest<Result<CartDto>>;
```

### Supported XML tags

Reach for these tags; use each only when it adds information. Prefer active voice and don't restate what a tag already implies (no *"Gets or sets…"*, *"Represents…"*, *"This method…"*).

| Tag | Use for |
|---|---|
| `<summary>` | What the member is or does — the contract. Always. |
| `<param>` | A parameter's meaning, unit, or constraint when the name doesn't carry it. |
| `<typeparam>` | A generic type parameter's role when non-obvious. |
| `<returns>` | The return value, nullability, or special states the type alone doesn't convey. |
| `<exception>` | Each exception the member deliberately throws. |
| `<remarks>` | An important usage note, side effect, or caveat that doesn't fit the `<summary>`. Sparingly. |
| `<inheritdoc/>` | A member that overrides/implements documented base or interface docs and adds nothing new. |
| `<see cref="..."/>` | An inline cross-reference to a related type or member. |
| `<seealso cref="..."/>` | A "related — go look at this" pointer to a sibling type. |
| `<value>` | What a property's value represents, when the `<summary>` doesn't already say it. Rarely needed. |

### Async methods

Describe the *result*, not the plumbing. Never mention `Task`, `await`, or "asynchronously" — those are implementation, not contract.

```csharp
// ✅ Describes the awaited result
/// <summary>
/// Sends a verification email.
/// </summary>
/// <param name="email">Recipient email address.</param>
/// <param name="cancellationToken">Cancels the operation.</param>
/// <returns>The delivery result.</returns>
/// <exception cref="ArgumentException">
/// Thrown when the email address is invalid.
/// </exception>
Task<DeliveryResult> SendVerificationAsync(string email, CancellationToken cancellationToken);

// ❌ Implementation noise
/// <summary>Asynchronously returns a Task that awaits the delivery.</summary>
```

### Boolean members

When `true` / `false` aren't obvious from the name, say what each represents.

```csharp
/// <summary>
/// Validates the supplied verification code.
/// </summary>
/// <param name="code">Code to validate.</param>
/// <returns><c>true</c> when the code is valid; otherwise, <c>false</c>.</returns>
bool Validate(string code);
```

### Enums

Put a `<summary>` on the type describing what the enumeration selects, and one on each member whose meaning isn't obvious from its name.

```csharp
/// <summary>
/// How an order's payment was captured.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Paid online via Stripe card checkout.</summary>
    Card,

    /// <summary>Paid in person at store pickup.</summary>
    InStore,
}
```

### Extension methods

State the behaviour the method *adds* to the type it extends — the `this` receiver is the subject.

```csharp
/// <summary>
/// Formats the amount as a Canadian-dollar currency string.
/// </summary>
public static string ToCadString(this decimal amount);
```

### Interfaces and abstractions

Document the *contract and expected behaviour*, never a single implementation's mechanics. Implementers that add nothing new inherit the docs with `<inheritdoc/>`.

```csharp
/// <summary>
/// Persistence contract for <see cref="Cart"/>. Implementations live in the
/// Infrastructure layer.
/// </summary>
public interface ICartRepository { ... }
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
public record AddToCartCommand(Guid ProductId, int Quantity) : IRequest<Result<CartDto>>;
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
    public async Task<Result<CartDto>> Handle(AddToCartCommand cmd, CancellationToken ct) { ... }
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
public sealed class SupabaseCartRepository(Supabase.Client client) : ICartRepository { ... }
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

- The DTO encodes a specific shape that isn't obvious (e.g. "the projection used by the admin list view, not the customer-facing one").
- One of the properties has a unit, constraint, or special state worth surfacing.

```csharp
// ✅ Purpose isn't obvious from the name
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

- ❌ **Document private members by default.** They're outside the API surface — only document them when explicitly requested.
- ❌ **Invent behaviour the code doesn't have.** Document only what's actually there; never describe a contract the implementation doesn't honour.
- ⚠️ **Don't pad `<remarks>` with narrative.** It's allowed for a genuine usage note, side effect, or caveat that doesn't fit the `<summary>` — but if it just restates the summary in prose, drop it.
- ❌ **Add `<example>` blocks.** Code examples belong in tests and docs, not in XML comments.
- ❌ **Write `<summary>This class represents a ...</summary>`.** Just describe what it is or does.
- ❌ **Add `<copyright>`, `<author>`, or other ceremony tags.** Not used in this codebase.
- ❌ **Translate resource keys.** Document the throw using the key name (`MessageKey = nameof(Strings.X)`), not the localized English text.
- ❌ **Add inline `//` comments alongside the XML.** Pick one. XML covers the contract; inline comments are reserved for non-obvious *why*.

---

## When in doubt

Would a future reader find the contract harder to understand without it? If yes, write the doc. If no, leave the name to do the work.

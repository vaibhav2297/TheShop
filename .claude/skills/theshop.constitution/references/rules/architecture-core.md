# Architecture — Core

> Implementation guide for Rules 1, 2, 3, 6, 7, 9, 29 from `SKILL.md`. Tells you **how** to place files, structure layers, enforce the dependency rule, and write the matching tests. The rules themselves live in `SKILL.md` — this file does not restate them.

---

## The four layers — what lives where

| Layer | Responsibility | Lives in |
|---|---|---|
| Domain | Entities, value objects, enums, domain exceptions, business invariants | `src/TheShop.Domain/` |
| Application | Use cases (MediatR Commands/Queries/Handlers), interfaces for *all* external dependencies, DTOs, AutoMapper profiles, FluentValidation validators, `Result<T>`, MediatR pipeline behaviors | `src/TheShop.Application/` |
| Infrastructure | Supabase repositories, Stripe service, Resend sender, `SupabaseAuthService`, DB record types, mapping between records and entities | `src/TheShop.Infrastructure/` |
| Web | Pages, components, state stores, MudBlazor theme, resources, layouts, route definitions, UI-only services, `BusyState` / `BusyKeys` / `Routes` | `src/TheShop.Web/` |
| Composition root | DI wiring — the only place that touches every layer | `Program.cs` |

### Folder layout

```
src/
├── TheShop.Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Enums/
│   └── Exceptions/
├── TheShop.Application/
│   ├── Common/
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   └── Behaviors/
│   ├── Features/
│   │   ├── Cart/
│   │   │   ├── Commands/
│   │   │   │   └── {CommandName}/   ← Command + Handler + Validator live together per command
│   │   │   ├── Queries/
│   │   │   │   └── {QueryName}/     ← same per-query grouping
│   │   │   └── DTOs/                ← shared across the feature's commands/queries
│   │   ├── Products/      (same sub-structure)
│   │   ├── Checkout/      (same sub-structure)
│   │   ├── Orders/        (same sub-structure)
│   │   ├── Auth/          (same sub-structure)
│   │   └── Customers/     (same sub-structure)
│   ├── Mapping/
│   └── DependencyInjection.cs
├── TheShop.Infrastructure/
│   ├── Persistence/
│   │   ├── SupabaseClientFactory.cs
│   │   ├── Records/
│   │   ├── Mappers/
│   │   └── Repositories/
│   ├── Auth/
│   ├── Payments/
│   ├── Email/
│   ├── Storage/
│   └── DependencyInjection.cs
└── TheShop.Web/
    ├── Pages/
    │   ├── Index/
    │   ├── Products/
    │   ├── Cart/
    │   ├── Checkout/
    │   ├── Account/
    │   └── Admin/
    │       └── _Imports.razor    ← applies layout + [Authorize] to every admin page
    ├── Components/
    │   ├── Layout/
    │   ├── Products/
    │   └── Common/          ← generic, business-agnostic UI primitives
    ├── Auth/
    │   └── SupabaseAuthStateProvider.cs
    ├── State/
    │   ├── CartState.cs
    │   ├── AuthState.cs
    │   └── ToastState.cs
    ├── Services/               ← UI-only services (browser storage, etc.)
    ├── Theme/
    │   ├── ShopColors.cs
    │   ├── ShopIcons.cs
    │   ├── ShopTypography.cs
    │   └── ShopTheme.cs
    ├── Resources/
    │   ├── Strings.resx
    │   └── Strings.fr.resx
    ├── Common/
    │   ├── Routes.cs
    │   ├── BusyState.cs
    │   └── BusyKeys.cs
    ├── Styles/                  ← see design-styles.md
    ├── wwwroot/
    ├── Program.cs               ← composition root
    └── _Imports.razor

tests/
├── TheShop.Domain.Tests/         (pure xUnit + FluentAssertions)
├── TheShop.Application.Tests/    (xUnit + NSubstitute + FluentAssertions)
├── TheShop.Infrastructure.Tests/ (Testcontainers + real PostgreSQL)
└── TheShop.Web.Tests/            (bUnit + mocked services)
```

### Organising principle — concern, not feature (Infrastructure)

`Application` is sliced **vertically by business feature** (`Features/Cart/`, `Features/Checkout/` — Rule 9). `Infrastructure` is the mirror opposite: it is sliced **horizontally by technical concern / external system**. Do **not** create `Infrastructure/{Feature}/` folders.

- `Persistence/` owns the **database** concern and is the **only** place holding the `Records/` + `Mappers/` + `Repositories/` trio.
- `Auth/`, `Payments/`, `Email/`, `Storage/` are **flat adapter folders** — one adapter class implementing one Application interface (`SupabaseAuthService : IAuthService`, etc.). They have no `Records/`/`Mappers/` sub-folders, because they wrap an SDK, not a set of DB rows.

A single feature's Infrastructure code therefore **scatters** across these folders. Cart, for example:

```
Persistence/Records/CartRecord.cs
Persistence/Mappers/CartMapper.cs
Persistence/Repositories/SupabaseCartRepository.cs
```

Rationale: SDK-shaped concerns (Stripe, Resend, Postgrest row models) are shared across many features and don't belong to any one of them — grouping by concern keeps SDK config, DI wiring, and SDK upgrades localized, and makes Rule 3 (SDK isolation) visible at the folder level.

---

## Enforcing the dependency rule

Project references are compiler-enforced:

| Project | References |
|---|---|
| `TheShop.Domain` | nothing |
| `TheShop.Application` | `Domain` |
| `TheShop.Infrastructure` | `Application`, `Domain` |
| `TheShop.Web` | `Application`, `Domain`, `Infrastructure` (composition only) |

Practical consequences for Rule 1 / Rule 2 / Rule 3:

- A Domain entity referencing `Supabase.Client` won't compile — there is no project reference.
- Application defines `IProductRepository`; Infrastructure provides `SupabaseProductRepository : IProductRepository`. Application has zero knowledge of Supabase.
- Web injects `IMediator` and the interfaces it consumes (state stores, services). It must NOT inject `Supabase.Client` directly anywhere.
- `Program.cs` is the one place where `AddApplication()`, `AddInfrastructure(config)`, `AddPresentation()` are wired. Each layer exposes its own `DependencyInjection.cs` extension.

```csharp
// src/TheShop.Web/Program.cs
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation();

builder.Services.AddMudServices();
await builder.Build().RunAsync();
```

```csharp
// src/TheShop.Web/DependencyInjection.cs
public static IServiceCollection AddPresentation(this IServiceCollection services)
{
    services.AddLocalization(o => o.ResourcesPath = "Resources");
    services.AddSingleton<ShopTheme>();
    services.AddScoped<CartState>();
    services.AddScoped<AuthState>();
    services.AddScoped<ToastState>();
    return services;
}
```

---

## Domain layer — what NOT to put here

| Don't include | Why |
|---|---|
| `using Supabase;` / `using Stripe.*;` / `using MudBlazor;` | Rule 2 — Domain is pure |
| `[JsonProperty]`, `[Column]`, `[Required]` (data-annotation persistence attributes) | persistence concern → Infrastructure record types |
| HTTP types, `HttpClient`, REST contracts | external system → Infrastructure |
| Interfaces for external services (`IPaymentService`, `IEmailSender`) | those go in Application — Domain doesn't know they exist |

Domain entities expose **behavior**, not setters:

```csharp
// Domain enforces invariants through methods, not through anemic setters.
public class Cart
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    private readonly List<CartItem> _items = [];
    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    public void AddItem(Product product, int quantity) { /* invariants here */ }
}
```

See `examples/domain-entity.md` for the canonical pattern.

---

## Cross-cutting Web-only primitives

Three primitives are presentation concerns and **must not** leak into Application/Domain through interfaces or behaviors:

- `BusyState` (Web — keyed reference counter + `Changed` event). Drives Rule 22.
- `BusyKeys` (Web — static class with nested string constants — never magic strings).
- `Routes` (Web — static class with nested route constants; helper methods for parameterised URLs). Drives Rule 21.

A MediatR pipeline behavior using `IBusyState` would force the type into Application and leak a UI primitive across the boundary. Drive busy state explicitly from page code instead.

```csharp
// Web/Common/Routes.cs
public static class Routes
{
    public const string Home = "/";
    public static class Auth
    {
        public const string SignIn = "/sign-in";
        public static string SignInWithReturn(string returnUrl) =>
            $"{SignIn}?returnUrl={Uri.EscapeDataString(returnUrl)}";
    }
}
```

---

## Coding standards

### Naming

| Element | Convention | Example |
|---|---|---|
| Project | `ProjectName.Layer` | `TheShop.Application` |
| Namespace | matches folder structure | `TheShop.Application.Features.Cart.Commands` |
| Class | PascalCase | `AddToCartHandler` |
| Interface | `I` prefix | `IProductRepository` |
| Command | `{Verb}{Noun}Command` | `AddToCartCommand` |
| Query | `{Verb}{Noun}Query` | `GetProductByIdQuery` |
| DTO | `{Noun}Dto` | `ProductDto` |
| DB record | `{Noun}Record` | `ProductRecord` |
| Test class | `{ClassUnderTest}Tests` | `AddToCartHandlerTests` |
| Theme class | `Shop{Concept}` | `ShopColors`, `ShopIcons` |
| Resource key | `{Context}_{Purpose}` | `ProductDetail_PageTitle`, `AddToCart` |

### File organisation
- One class per file. File name = primary class name.
- File-scoped namespaces (`namespace X;`).

### Async / I-O
- All I/O methods are `async`; names end in `Async`.
- Pass `CancellationToken` on every async method that crosses a layer boundary (Rule 8). Never `.Result` or `.Wait()`.

### Nullability
- Nullable reference types enabled in every project.
- Use `?` for nullable types explicitly. Use null-forgiving `!` only when proven non-null.

### Constructor injection — primary constructors preferred (.NET 10 / C# 13)

Apply primary constructors to handlers, repositories, services, pipeline behaviors — anything whose constructor just stores dependencies. The parameter is in scope through the class body; no backing field needed.

```csharp
public sealed class RequestSignInOtpHandler(ICustomerRepository customers, IAuthService auth)
    : IRequestHandler<RequestSignInOtpCommand, Result<OtpRequestedDto>>
{
    public async Task<Result<OtpRequestedDto>> Handle(...) =>
        await customers.ExistsForEmailAsync(...);   // 'customers' in scope
}
```

**Don't** use a primary constructor when the constructor has:
- Validation logic (value objects like `Email`, `DateOfBirth`).
- Side effects such as event subscription (e.g. `SupabaseAuthService` subscribing to Gotrue state changes).
- Composition logic that builds the dependency from inputs (e.g. `SupabaseClientFactory`).
- Factory-pattern methods needing explicit constructors (e.g. `Result<T>`).

### Collection expressions

Replace `new List<T>()`, `new[] { ... }`, `Array.Empty<T>()`, and `new List<T> { a, b }` with collection expression syntax:

```csharp
List<Claim> claims = [new(ClaimTypes.NameIdentifier, userId)];
int[] sizes = [1, 2, 3];
string[] empty = [];
var merged = [..first, ..second];
```

---

## Testing (Rule 29)

Every new handler, repository, value object, or domain method gets at least one test, in `tests/TheShop.{Layer}.Tests/` matching the source folder structure.

Authoring mechanics (per-layer tooling, naming convention, manifest/trait contract) are **not** restated here — the `shop-test-writer` agent is the single authoritative source for *writing* tests, and `checklists/code-generation.md` §Tests is the authority for *verifying* them. Consult those rather than duplicating their detail in this file.

---

## Common mistakes

| Mistake | Fix |
|---|---|
| `@inject Supabase.Client Supabase` in a `.razor` page | Inject `IMediator`, dispatch a Query/Command |
| `using Supabase;` in `TheShop.Domain` or `TheShop.Application` | Move to Infrastructure; expose interface in Application |
| Service operating on entity (`orderService.ShipOrder(order, trackingNumber)`) | Behaviour belongs on the entity: `order.MarkAsShipped(trackingNumber)` |
| Returning raw entity to UI (`Task<Product> Handle(...)`) | Map to `ProductDto`; UI never sees entities |
| `throw new NotFoundException()` for a missing product | Return `Result.Fail(nameof(Strings.ProductNotFound))` |
| Anemic domain (`Cart` with public setters and no methods) | Move invariants into entity methods (`AddItem`, `RemoveItem`, `TotalPrice`) |
| Missing `CancellationToken` on an async signature crossing a layer | Add it; propagate through the call chain |
| `_isBusy` boolean in a page | Drive via `BusyState.RunAsync(BusyKeys.X, ...)` — see Rule 22 |

For patterns specific to MediatR, `Result<T>`, validators, mapping, and Infrastructure adapters, see `architecture-patterns.md`. For admin routing, RLS, and role wiring, see `architecture-admin.md`.

# Vape E-commerce — Blazor WASM Clean Architecture Instructions

> **Audience:** AI coding agent (Claude, Copilot, Cursor, etc.)
> **Project:** Premium vape e-commerce platform (Canadian market)
> **Stack:** Blazor WebAssembly + MudBlazor + Supabase + Stripe + Resend
> **Companion files:** `DESIGN.md` (visual language and theming)

This document is the canonical reference for the architecture, structure, and coding standards of this project. **Read this entire file before generating any code.** Any code that violates these rules must be flagged and corrected, not committed.

For all visual, theming, color, icon, typography, and string resource concerns, see `DESIGN.md`.

---

## Table of Contents

1. [Tech Stack](#tech-stack)
2. [Architecture Overview](#architecture-overview)
3. [Architecture Layers](#architecture-layers)
4. [The Dependency Rule](#the-dependency-rule)
5. [Folder Structure](#folder-structure)
6. [Resource Files & Static Assets](#resource-files--static-assets)
7. [Project References](#project-references)
8. [Feature Flow Example — Add to Cart](#feature-flow-example--add-to-cart)
9. [Core Principles & Rules](#core-principles--rules)
10. [Coding Standards](#coding-standards)
11. [NuGet Packages](#nuget-packages)
12. [Testing Strategy](#testing-strategy)
13. [Common Patterns & Templates](#common-patterns--templates)
14. [Anti-Patterns to Reject](#anti-patterns-to-reject)
15. [Code Generation Checklist](#code-generation-checklist)

---

## Tech Stack

| Layer | Technology | Purpose |
|---|---|---|
| Frontend | Blazor WebAssembly (.NET 8+) | UI runs entirely in the browser |
| UI Components | MudBlazor | Component library — use exclusively |
| Backend | Supabase | Auth, PostgreSQL, Storage, Edge Functions |
| Payments | Stripe (or Helcim) | Checkout, transaction processing |
| Email | Resend | Transactional emails |
| Hosting | Azure Static Web Apps | CDN-served static deployment |
| Domain | Dynadot | `.ca` and `.com` registration |

**Important:** Never invent UI components outside MudBlazor. Never call Supabase, Stripe, or Resend SDKs from anywhere except the Infrastructure layer.

---

## Architecture Overview

This project uses **Clean Architecture** with strict layer separation. The architecture has four primary layers plus a composition root:

```
                    ┌──────────────────────────┐
                    │         Domain           │   ← innermost
                    │  Entities, value objects │     depends on nothing
                    │  Pure business rules     │
                    └────────────▲─────────────┘
                                 │
                                 │ depends on
                                 │
                    ┌────────────┴─────────────┐
                    │       Application        │   depends only on Domain
                    │  Use cases, interfaces,  │
                    │  DTOs, validators        │
                    └────▲───────────────▲─────┘
                         │               │
            implements   │               │   uses interfaces /
            interfaces   │               │   sends commands & queries
                         │               │
       ┌─────────────────┴──┐         ┌──┴────────────────────┐
       │   Infrastructure   │         │      Presentation     │
       │  Supabase, Stripe, │         │   Blazor + MudBlazor, │
       │  Resend adapters   │         │   pages, state stores │
       └─────────────▲──────┘         └──▲────────────────────┘
                     │                   │
                     │  registered in DI │  registered in DI
                     │                   │
                     └─────────┬─────────┘
                               │
                    ┌──────────┴──────────────┐
                    │   Composition Root      │   ← Program.cs only
                    │   Wires everything      │     references all layers
                    │   via DI                │     to register implementations
                    └─────────────────────────┘
```

**How to read this diagram:**

- **Domain sits at the top (innermost layer).** It depends on nothing.
- **Application depends on Domain only.** It defines interfaces but never knows their implementations.
- **Infrastructure and Presentation are siblings.** Both depend inward on Application + Domain. Neither depends on the other.
- **Infrastructure** *implements* the interfaces defined in Application (e.g. `SupabaseProductRepository : IProductRepository`).
- **Presentation** *uses* the interfaces defined in Application (e.g. injects `IMediator` and sends commands/queries).
- **Composition Root (`Program.cs`)** is the only place that touches all layers — its single job is to register concrete implementations against their interfaces in the DI container at startup.

**Important:** Presentation and Infrastructure never reference each other directly. The only "link" between them is the Composition Root, which lives at the application's entry point and is allowed to know about everything.

---

## Architecture Layers

### Layer 1 — Domain (Innermost)

**Purpose:** Pure business logic and rules. Zero external dependencies.

**What lives here:**
- Entities (`Product`, `Order`, `Customer`, `Cart`)
- Value objects (`Money`, `Address`, `NicotineStrength`)
- Enums (`OrderStatus`, `ProductCategory`)
- Domain exceptions (`DomainException`, `InsufficientStockException`)
- Domain events (if using event-driven patterns)

**What does NOT live here:**
- ❌ No Supabase, Stripe, or any SDK code
- ❌ No MudBlazor or Blazor components
- ❌ No HTTP, JSON, or serialization concerns
- ❌ No `[JsonProperty]` attributes
- ❌ No database concerns
- ❌ No interfaces for external services (those go in Application)

**Why:** If you swap Supabase for Firebase tomorrow, this layer doesn't change. If you swap MudBlazor for Radzen, it doesn't change. Business rules stay stable while infrastructure churns.

**Example:**

```csharp
namespace TheShop.Domain.Entities;

public class Cart
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    private readonly List<CartItem> _items = new();
    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    public void AddItem(Product product, int quantity)
    {
        if (quantity <= 0) 
            throw new DomainException("Quantity must be positive");
        if (product.Stock < quantity) 
            throw new InsufficientStockException(product.Id);

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

---

### Layer 2 — Application (Use Cases)

**Purpose:** Orchestrates use cases. Defines what the application does in business terms.

**What lives here:**
- Use cases as MediatR Commands and Queries
- Interfaces for ALL external dependencies (`IProductRepository`, `IPaymentService`, `IEmailSender`, `ICurrentUserService`)
- DTOs (`ProductDto`, `OrderSummaryDto`)
- AutoMapper profiles
- FluentValidation validators
- `Result<T>` types for outcomes
- Pipeline behaviors (logging, validation, authorization)

**What does NOT live here:**
- ❌ No actual implementations of interfaces
- ❌ No Supabase SDK code
- ❌ No MudBlazor or Blazor code
- ❌ No `using Supabase;` statements anywhere

**Why:** This layer defines *what* your app does. Implementations come from outer layers via dependency injection. Tests can mock interfaces freely.

**Example:**

```csharp
namespace TheShop.Application.Features.Cart.Commands;

public record AddToCartCommand(Guid ProductId, int Quantity) 
    : IRequest<Result<CartDto>>;

public class AddToCartHandler : IRequestHandler<AddToCartCommand, Result<CartDto>>
{
    private readonly IProductRepository _products;
    private readonly ICartRepository _carts;
    private readonly ICurrentUserService _user;
    private readonly IMapper _mapper;

    public AddToCartHandler(
        IProductRepository products,
        ICartRepository carts,
        ICurrentUserService user,
        IMapper mapper)
    {
        _products = products;
        _carts = carts;
        _user = user;
        _mapper = mapper;
    }

    public async Task<Result<CartDto>> Handle(
        AddToCartCommand cmd, 
        CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(cmd.ProductId, ct);
        if (product is null) 
            return Result.Fail<CartDto>("ProductNotFound"); // resource KEY

        var cart = await _carts.GetForUserAsync(_user.Id, ct) 
                   ?? Cart.CreateFor(_user.Id);

        try
        {
            cart.AddItem(product, cmd.Quantity);
        }
        catch (DomainException ex)
        {
            return Result.Fail<CartDto>(ex.MessageKey);
        }

        await _carts.SaveAsync(cart, ct);
        return Result.Ok(_mapper.Map<CartDto>(cart));
    }
}
```

---

### Layer 3 — Infrastructure (Adapters)

**Purpose:** Concrete implementations that talk to external systems.

**What lives here:**
- `SupabaseProductRepository : IProductRepository`
- `SupabaseAuthService : IAuthService`
- `StripePaymentService : IPaymentService`
- `ResendEmailSender : IEmailSender`
- DTOs/Records that map to database tables (kept internal)
- Mapping logic between database records and Domain entities
- `DependencyInjection.cs` extension method to register infrastructure services

**What does NOT live here:**
- ❌ No business logic — only translation between Domain and external systems
- ❌ No domain rules — those live in Domain entities

**Why:** Provider swap = replace one class. Want to switch from Supabase to AWS Cognito + RDS? Write a new `PostgresProductRepository`, register it in DI, delete the Supabase one. Zero changes to UI or use cases.

**Example:**

```csharp
namespace TheShop.Infrastructure.Persistence.Repositories;

public class SupabaseProductRepository : IProductRepository
{
    private readonly Supabase.Client _client;

    public SupabaseProductRepository(Supabase.Client client) => _client = client;

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _client
            .From<ProductRecord>()
            .Where(x => x.Id == id)
            .Single();

        return response?.ToDomain();
    }
}
```

---

### Layer 4 — Presentation (Blazor + MudBlazor)

**Purpose:** Render UI and dispatch user actions. Nothing more.

**What lives here:**
- All `.razor` files (pages and components)
- State stores (`CartState`, `AuthState`, `ToastState`)
- MudBlazor theme configuration → see `DESIGN.md`
- Layouts and navigation
- Route definitions
- UI-only services (e.g. browser local storage wrapper)
- Resource files (`.resx`) for localized strings → see `DESIGN.md`
- Static assets (icons, fonts, images) → see `DESIGN.md`

**What does NOT live here:**
- ❌ No business logic in `@code { }` blocks
- ❌ No direct Supabase, Stripe, or Resend calls
- ❌ No SQL or HTTP calls of any kind
- ❌ No domain rule enforcement
- ❌ **No hardcoded user-facing strings** — see `DESIGN.md`
- ❌ **No hardcoded colors or icon paths** — see `DESIGN.md`

**Example:**

```razor
@page "/products/{Slug}"
@inject IMediator Mediator
@inject CartState Cart
@inject ISnackbar Snackbar
@inject IStringLocalizer<Strings> Localizer

<PageTitle>@Localizer["ProductDetail_PageTitle"]</PageTitle>

@if (_product is not null)
{
    <MudCard>
        <MudText Typo="Typo.h4">@_product.Name</MudText>
        <MudText Typo="Typo.body1">@_product.Price.Format()</MudText>
        <MudButton OnClick="@AddToCart" 
                   Color="Color.Primary" 
                   Variant="Variant.Filled"
                   StartIcon="@ShopIcons.Cart">
            @Localizer["AddToCart"]
        </MudButton>
    </MudCard>
}

@code {
    [Parameter] public string Slug { get; set; } = "";
    private ProductDto? _product;
    private int _quantity = 1;

    protected override async Task OnInitializedAsync()
    {
        var result = await Mediator.Send(new GetProductBySlugQuery(Slug));
        if (result.IsSuccess) _product = result.Value;
    }

    private async Task AddToCart()
    {
        var cmd = new AddToCartCommand(_product!.Id, _quantity);
        var result = await Mediator.Send(cmd);
        if (result.IsSuccess)
        {
            Cart.UpdateFromDto(result.Value);
            Snackbar.Add(Localizer["AddedToCart"], Severity.Success);
        }
        else
        {
            Snackbar.Add(Localizer[result.Error], Severity.Error);
        }
    }
}
```

Note the use of `IStringLocalizer<Strings> Localizer` for strings and `ShopIcons.Cart` for the icon — both required by `DESIGN.md`.

---

### Composition Root — Program.cs

**Purpose:** Wire interfaces to implementations via dependency injection.

This is the **only** place where all layers meet.

```csharp
// Program.cs
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
// Web/DependencyInjection.cs
public static IServiceCollection AddPresentation(this IServiceCollection services)
{
    services.AddLocalization(options => options.ResourcesPath = "Resources");
    services.AddSingleton<ShopTheme>();
    services.AddScoped<CartState>();
    services.AddScoped<AuthState>();
    services.AddScoped<ToastState>();
    return services;
}
```

---

## The Dependency Rule

**Source code dependencies always point inward.**

| Layer | Can depend on |
|---|---|
| Domain | Nothing |
| Application | Domain |
| Infrastructure | Application, Domain |
| Presentation | Application, Domain |
| Composition Root | All layers (only place this is allowed) |

**Critical:** Presentation must NEVER reference Infrastructure directly. Pages call use cases through MediatR — never through concrete implementations.

---

## Folder Structure

```
TheShop.sln
└── src/
    ├── TheShop.Domain/                    // Layer 1 — pure C#
    │   ├── Entities/
    │   ├── ValueObjects/
    │   ├── Enums/
    │   └── Exceptions/
    │
    ├── TheShop.Application/                // Layer 2 — use cases
    │   ├── Common/
    │   │   ├── Interfaces/
    │   │   ├── Models/
    │   │   └── Behaviors/
    │   ├── Features/                        // Vertical slices
    │   │   ├── Products/
    │   │   │   ├── Queries/
    │   │   │   ├── Commands/
    │   │   │   ├── DTOs/
    │   │   │   └── Validators/
    │   │   ├── Cart/
    │   │   ├── Checkout/
    │   │   ├── Orders/
    │   │   ├── Auth/
    │   │   └── Customers/
    │   ├── Mapping/
    │   └── DependencyInjection.cs
    │
    ├── TheShop.Infrastructure/             // Layer 3 — adapters
    │   ├── Persistence/
    │   │   ├── SupabaseClient.cs
    │   │   ├── Records/
    │   │   ├── Mappers/
    │   │   └── Repositories/
    │   ├── Auth/
    │   ├── Payments/
    │   ├── Email/
    │   ├── Storage/
    │   └── DependencyInjection.cs
    │
    └── TheShop.Web/                        // Layer 4 — Blazor WASM
        ├── Pages/
        │   ├── Index.razor
        │   ├── Products/
        │   ├── Cart/
        │   ├── Checkout/
        │   └── Account/
        ├── Components/
        │   ├── Layout/
        │   ├── Products/
        │   └── Shared/
        ├── State/                           // Client state stores
        │   ├── CartState.cs
        │   ├── AuthState.cs
        │   └── ToastState.cs
        ├── Services/                        // UI-only services
        │   └── BrowserStorageService.cs
        ├── Theme/                           // Visual language → see DESIGN.md
        │   ├── ShopColors.cs
        │   ├── ShopIcons.cs
        │   ├── ShopTypography.cs
        │   └── ShopTheme.cs
        ├── Resources/                       // Localized strings → see DESIGN.md
        │   ├── Strings.resx                 // Default (English)
        │   └── Strings.fr.resx              // French
        ├── wwwroot/
        │   ├── index.html
        │   ├── css/
        │   │   └── app.css
        │   ├── images/
        │   └── fonts/
        ├── Program.cs                       // Composition root
        ├── App.razor
        └── _Imports.razor

└── tests/
    ├── TheShop.Domain.Tests/
    ├── TheShop.Application.Tests/
    ├── TheShop.Infrastructure.Tests/
    └── TheShop.Web.Tests/                  // bUnit for components
```

---

## Resource Files & Static Assets

This project enforces strict separation between code and resources. **All visual, textual, and theming concerns are governed by `DESIGN.md`** — this section only describes WHERE these files live.

### File location summary

| Resource type | Location | Naming | Governed by |
|---|---|---|---|
| Localized strings | `Web/Resources/` | `Strings.resx`, `Strings.{lang}.resx` | DESIGN.md §Strings |
| Color tokens | `Web/Theme/ShopColors.cs` | `ShopColors` | DESIGN.md §Colors |
| Icon registry | `Web/Theme/ShopIcons.cs` | `ShopIcons` | DESIGN.md §Icons |
| Typography tokens | `Web/Theme/ShopTypography.cs` | `ShopTypography` | DESIGN.md §Typography |
| MudBlazor theme | `Web/Theme/ShopTheme.cs` | `ShopTheme` | DESIGN.md §Theme |
| Font files | `Web/wwwroot/fonts/` | `*.woff2` | DESIGN.md §Typography |
| Product images | Supabase Storage | (uploaded) | DESIGN.md §Imagery |

### The non-negotiable rules

These rules are enforced project-wide. Full details in `DESIGN.md`:

1. **No hardcoded user-facing strings.** Every string a user reads must come from `Strings.resx` via `IStringLocalizer<Strings>`. Page titles, button labels, validation messages, toast messages, ARIA labels — all from resources.

2. **All theme classes use the `Shop` prefix.** `ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`. This prevents collision with MudBlazor's built-in types and makes project-specific tokens immediately identifiable.

3. **MudBlazor components only.** If a UI requirement cannot be met by MudBlazor, ask the user before implementing any alternative.

If you encounter `<MudText>Add to Cart</MudText>` (hardcoded string) or `Color="#101010"` (hardcoded color), that is a violation. Refactor before committing.

---

## Project References

| Project | References |
|---|---|
| `TheShop.Domain` | None |
| `TheShop.Application` | `Domain` |
| `TheShop.Infrastructure` | `Application`, `Domain` |
| `TheShop.Web` | `Application`, `Domain`, `Infrastructure` (composition only) |

The .NET compiler enforces architecture at the project level.

---

## Feature Flow Example — Add to Cart

This walkthrough shows how a single user action flows through every layer. **Use this as the template for every feature you build.**

### Step 1 — UI: Customer clicks "Add to Cart"

```razor
@page "/products/{Slug}"
@inject IMediator Mediator
@inject CartState Cart
@inject ISnackbar Snackbar
@inject IStringLocalizer<Strings> Localizer

<MudButton OnClick="@AddToCart" 
           Color="Color.Primary" 
           StartIcon="@ShopIcons.Cart">
    @Localizer["AddToCart"]
</MudButton>

@code {
    private async Task AddToCart()
    {
        var cmd = new AddToCartCommand(_product.Id, _quantity);
        var result = await Mediator.Send(cmd);
        
        if (result.IsSuccess)
        {
            Cart.UpdateFromDto(result.Value);
            Snackbar.Add(Localizer["AddedToCart"], Severity.Success);
        }
        else
        {
            Snackbar.Add(Localizer[result.Error], Severity.Error);
        }
    }
}
```

### Step 2 — Application: AddToCartHandler orchestrates

```csharp
public record AddToCartCommand(Guid ProductId, int Quantity) 
    : IRequest<Result<CartDto>>;

public class AddToCartHandler : IRequestHandler<AddToCartCommand, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(AddToCartCommand cmd, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(cmd.ProductId, ct);
        if (product is null) 
            return Result.Fail<CartDto>("ProductNotFound");  // resource KEY

        var cart = await _carts.GetForUserAsync(_user.Id, ct) 
                   ?? Cart.CreateFor(_user.Id);

        try { cart.AddItem(product, cmd.Quantity); }
        catch (DomainException ex) { return Result.Fail<CartDto>(ex.MessageKey); }

        await _carts.SaveAsync(cart, ct);
        return Result.Ok(_mapper.Map<CartDto>(cart));
    }
}
```

**Note:** Application returns resource KEYS, not English text — UI looks up the localized string. This keeps Application layer language-agnostic.

### Step 3 — Infrastructure: Supabase repository executes

```csharp
public class SupabaseProductRepository : IProductRepository
{
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _client
            .From<ProductRecord>()
            .Where(x => x.Id == id)
            .Single();

        return response?.ToDomain();
    }
}
```

### Step 4 — UI updates

`CartState.UpdateFromDto` fires `StateHasChanged` → cart icon shows new count → MudSnackbar shows localized "Added to cart" toast.

---

## Core Principles & Rules

### 1. Pages are dumb
A `.razor` file's job is to render HTML and dispatch user actions. If your `@code` block exceeds 30 lines or contains conditional business logic, refactor it into the Application layer.

### 2. Domain owns the rules
Stock validation, price calculation, age checks, order state transitions — all live as methods on Domain entities.

```csharp
// ✅ Good — domain method
order.MarkAsShipped(trackingNumber);

// ❌ Bad — service operates on entity
orderService.ShipOrder(order, trackingNumber);
```

### 3. Use MediatR for use cases
Every action becomes a Command or Query handled by MediatR.

### 4. Result<T> instead of throwing for expected failures
"Product not found" or "Insufficient stock" are normal business outcomes — return `Result.Fail()`. Throw exceptions only for unexpected technical failures.

### 5. DTOs cross layer boundaries — entities don't
Application returns `ProductDto` to UI, never the raw `Product` entity.

### 6. Infrastructure isolates external SDKs
Supabase SDK, Stripe SDK, Resend SDK are imported **only** in the Infrastructure project.

### 7. Composition root is the only place that knows everything
`Program.cs` is the single file where you wire interfaces to implementations.

### 8. Vertical slices in Application
Group features by business capability, not technical concern.

### 9. CancellationToken everywhere
Every async method that crosses a layer boundary accepts a `CancellationToken`.

### 10. Immutable DTOs
Use `record` types for DTOs and Commands/Queries.

### 11. No hardcoded strings
All user-facing strings come from `Strings.resx` via `IStringLocalizer<Strings>`. See `DESIGN.md`.

### 12. No hardcoded design tokens
All colors, icons, and typography come from `Shop*` classes or MudBlazor's `Color` enum / `Typo` parameter. Never use `#101010`, raw icon paths, or inline `font-size` directly. See `DESIGN.md`.

---

## Coding Standards

### Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Projects | `ProjectName.Layer` | `TheShop.Application` |
| Namespaces | Match folder structure | `TheShop.Application.Features.Cart.Commands` |
| Classes | PascalCase | `AddToCartHandler` |
| Interfaces | `I` prefix | `IProductRepository` |
| Commands | `{Verb}{Noun}Command` | `AddToCartCommand` |
| Queries | `{Verb}{Noun}Query` | `GetProductByIdQuery` |
| DTOs | `{Noun}Dto` | `ProductDto` |
| Records (DB) | `{Noun}Record` | `ProductRecord` |
| Test classes | `{ClassUnderTest}Tests` | `AddToCartHandlerTests` |
| Theme classes | `Shop{Concept}` | `ShopColors`, `ShopIcons` |
| Resource keys | `{Context}_{Purpose}` | `ProductDetail_PageTitle`, `AddToCart` |

### File Organization
- One class per file
- File name matches the primary class name
- Use file-scoped namespaces

### Async/Await
- All I/O methods are `async`
- Method names end in `Async`
- Always pass `CancellationToken`
- Never use `.Result` or `.Wait()`

### Nullability
- Enable nullable reference types in all projects
- Use `?` for nullable types explicitly
- Use null-forgiving `!` only when proven non-null

### Constructor Injection

```csharp
// ✅ Good — constructor injection, readonly fields
public class AddToCartHandler
{
    private readonly IProductRepository _products;
    public AddToCartHandler(IProductRepository products) => _products = products;
}
```

---

## NuGet Packages

### Application Layer
```xml
<PackageReference Include="MediatR" Version="12.*" />
<PackageReference Include="FluentValidation" Version="11.*" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />
<PackageReference Include="AutoMapper" Version="13.*" />
<PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.*" />
<PackageReference Include="ErrorOr" Version="1.*" />
```

### Infrastructure Layer
```xml
<PackageReference Include="supabase-csharp" Version="1.*" />
<PackageReference Include="Stripe.net" Version="46.*" />
<PackageReference Include="Resend" Version="0.*" />
<PackageReference Include="Polly" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.*" />
```

### Web (Blazor) Layer
```xml
<PackageReference Include="MudBlazor" Version="7.*" />
<PackageReference Include="Blazored.LocalStorage" Version="4.*" />
<PackageReference Include="Microsoft.Extensions.Localization" Version="8.*" />
<PackageReference Include="Microsoft.AspNetCore.Localization" Version="8.*" />
```

### Test Projects
```xml
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
<PackageReference Include="NSubstitute" Version="5.*" />
<PackageReference Include="bunit" Version="1.*" />
<PackageReference Include="Testcontainers.PostgreSql" Version="3.*" />
```

---

## Testing Strategy

| Layer | Test type | Tools |
|---|---|---|
| Domain | Pure unit tests | xUnit, FluentAssertions |
| Application | Mocked unit tests | xUnit, NSubstitute, FluentAssertions |
| Infrastructure | Integration tests | Testcontainers + real PostgreSQL |
| Web | Component tests | bUnit + mocked services |

### Test Naming Convention
`{MethodOrFeature}_{Scenario}_{ExpectedOutcome}`

Examples:
- `AddItem_WhenQuantityIsZero_ThrowsDomainException`
- `Handle_WhenProductNotFound_ReturnsFailureResult`

---

## Common Patterns & Templates

### Result<T> Pattern

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string Error { get; }   // resource KEY, not display text

    private Result(bool isSuccess, T? value, string error) { /* ... */ }

    public static Result<T> Ok(T value) => new(true, value, "");
    public static Result<T> Fail(string errorKey) => new(false, default, errorKey);
}
```

### MediatR Pipeline Behavior — Validation

```csharp
public class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken ct)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any()) throw new ValidationException(failures);
        return await next();
    }
}
```

### State Store Pattern (Blazor)

```csharp
public class CartState
{
    private CartDto? _cart;
    public CartDto? Cart => _cart;
    public int ItemCount => _cart?.Items.Sum(i => i.Quantity) ?? 0;

    public event Action? OnChange;

    public void UpdateFromDto(CartDto cart)
    {
        _cart = cart;
        NotifyStateChanged();
    }

    public void Clear()
    {
        _cart = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
```

---

## Anti-Patterns to Reject

### ❌ Calling Supabase from a page
```razor
@inject Supabase.Client Supabase  // NEVER
```
**Fix:** Inject `IMediator`, send a query.

### ❌ Business logic in `@code` blocks
```razor
@code {
    private void AddToCart()
    {
        if (_product.Stock < _quantity) { /* ... */ }  // NEVER
    }
}
```
**Fix:** Move logic into Domain entity methods or Application handlers.

### ❌ Domain referencing Infrastructure
```csharp
using Supabase;  // NEVER in Domain
```

### ❌ Throwing exceptions for expected failures
```csharp
if (product is null) throw new NotFoundException();  // BAD for business rules
```
**Fix:** Return `Result.Fail("ProductNotFound")`.

### ❌ Anemic domain models
```csharp
public class Cart { public List<CartItem> Items { get; set; } = new(); }  // BAD
```
**Fix:** Behavior belongs on the entity.

### ❌ Passing entities to UI
```csharp
public async Task<Product> Handle(...)  // BAD
```
**Fix:** Return `ProductDto`.

### ❌ Hardcoded strings
```razor
<MudText>Add to Cart</MudText>  // NEVER
```
**Fix:** `<MudText>@Localizer["AddToCart"]</MudText>` — see `DESIGN.md`.

### ❌ Hardcoded colors or design tokens
```razor
<MudButton Style="background:#101010" />            // NEVER
```
**Fix:** Use `Color="Color.Primary"` — see `DESIGN.md`.

### ❌ Custom UI components when MudBlazor exists
Use MudBlazor consistently. If MudBlazor cannot meet the requirement, ask the user first.

---

## Code Generation Checklist

Before writing or accepting any code, verify:

### Architecture
- [ ] Is this in the right layer?
- [ ] Does it follow the dependency rule?
- [ ] Are external SDKs (Supabase/Stripe/Resend) only in Infrastructure?
- [ ] Are entities staying in Domain (not leaking to UI)?
- [ ] Is business logic in Domain entities or Application handlers?
- [ ] Are use cases dispatched through MediatR?
- [ ] Is `Result<T>` used for expected failures?
- [ ] Are DTOs (records) used to cross layer boundaries?
- [ ] Is dependency injection via constructor?
- [ ] Do async methods accept `CancellationToken`?
- [ ] Are commands/queries records (immutable)?
- [ ] Is the file in the correct folder per the structure?
- [ ] Does the class name follow naming conventions?
- [ ] Is there a corresponding test for this code?

For design-related checks, see the **Design Checklist** in `DESIGN.md`.

If any answer is "no", stop and reconsider before proceeding.

---

## Final Reminders for AI Agents

1. **When in doubt, ask.** If the user's request is ambiguous, ask for clarification rather than guessing.
2. **Refuse to violate the architecture.** If asked to "just put it in the page for now", explain why this creates technical debt.
3. **Generate tests alongside code.** Every new handler, repository, or domain method should have at least one test.
4. **Reference both files.** When making decisions, cite the specific principle from `ARCHITECTURE.md` or `DESIGN.md`.
5. **Be consistent.** The patterns shown for Cart should be replicated identically for Orders, Products, Checkout, etc.

---

**End of Architecture Instructions**

*Last updated: May 2026 · Version 1.0*
*See `DESIGN.md` for visual language, theming, and resource conventions.*

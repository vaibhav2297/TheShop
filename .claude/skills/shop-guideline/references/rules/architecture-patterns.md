# Architecture — Patterns

> Implementation guide for Rules 4, 5, 7, 8, 12 from `SKILL.md`. Covers the concrete patterns used in the Application and Infrastructure layers — MediatR, `Result<T>`, validators, AutoMapper, pipeline behaviors, repository wiring. Rule statements live in `SKILL.md`; this file does not restate them.

---

## MediatR — Commands, Queries, Handlers

Every use case is a Command (mutates state) or Query (reads state) handled by MediatR.

```csharp
namespace TheShop.Application.Features.Cart.Commands;

public record AddToCartCommand(Guid ProductId, int Quantity)
    : IRequest<Result<CartDto>>;

public sealed class AddToCartHandler(
    IProductRepository products,
    ICartRepository carts,
    ICurrentUserService user,
    IMapper mapper) : IRequestHandler<AddToCartCommand, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(AddToCartCommand cmd, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(cmd.ProductId, ct);
        if (product is null)
            return Result.Fail<CartDto>(nameof(Strings.ProductNotFound));

        var cart = await carts.GetForUserAsync(user.Id, ct) ?? Cart.CreateFor(user.Id);

        try { cart.AddItem(product, cmd.Quantity); }
        catch (DomainException ex) { return Result.Fail<CartDto>(ex.MessageKey); }

        await carts.SaveAsync(cart, ct);
        return Result.Ok(mapper.Map<CartDto>(cart));
    }
}
```

Key points:
- Command/Query is a `record` (Rule 7).
- Handler is `sealed` and uses a primary constructor (see `architecture-core.md` §Coding standards).
- `Handle(...)` accepts `CancellationToken` and propagates it to every async call (Rule 8).
- Domain invariants run on the entity (`cart.AddItem`) — never re-implemented in the handler (Rule 6).
- Errors come back as resource keys via `nameof(Strings.{Key})` (Rule 12).
- The handler returns a DTO produced by AutoMapper, not the entity (Rule 7).

See `examples/application-handler.md` for the full file pattern.

---

## `Result<T>` (Rule 5)

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string Error { get; }   // resource KEY, not display text

    public static Result<T> Ok(T value) => new(true, value, "");
    public static Result<T> Fail(string errorKey) => new(false, default, errorKey);
}
```

- `Error` carries a **resource key** (e.g. `"ProductNotFound"`), never English text. The Web layer translates it via `Localizer[result.Error]` (see Rule 11).
- Use `nameof(Strings.{Key})` so the key is compile-time-checked even on the producer side.
- Throw exceptions only for **unexpected** failures (DB connection lost, third-party 5xx, malformed serialized blob). "Product missing", "insufficient stock", "OTP expired" are *expected* — return `Result.Fail(...)`.

---

## Application interfaces (what crosses the layer boundary)

Application declares interfaces for **every** external dependency it needs. Infrastructure provides the implementations.

```csharp
namespace TheShop.Application.Common.Interfaces;

public interface ICartRepository
{
    Task<Cart?> GetForUserAsync(Guid customerId, CancellationToken ct);
    Task SaveAsync(Cart cart, CancellationToken ct);
}

public interface IPaymentService
{
    Task<Result<PaymentIntentDto>> CreateIntentAsync(decimal amount, string currency, CancellationToken ct);
}

public interface IEmailSender
{
    Task<Result> SendAsync(EmailMessage message, CancellationToken ct);
}

public interface ICurrentUserService
{
    Guid Id { get; }
    bool IsAuthenticated { get; }
}
```

Naming: `I{Domain}Repository`, `I{Provider-neutral concept}Service` / `I{Concept}Sender`. Never bake the provider into the interface name (`ISupabaseRepository` is a violation of Rule 3).

---

## FluentValidation — request validation

One validator per Command/Query. Plug them into MediatR via a pipeline behavior so validation runs before the handler.

```csharp
public sealed class AddToCartCommandValidator : AbstractValidator<AddToCartCommand>
{
    public AddToCartCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var failures = validators
            .Select(v => v.Validate(new ValidationContext<TRequest>(request)))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0) throw new ValidationException(failures);
        return await next();
    }
}
```

Register both behaviour and validators in `Application.DependencyInjection`:

```csharp
services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

---

## AutoMapper — entity ↔ DTO

DTOs cross layer boundaries; entities don't (Rule 7). Map at the edge.

```csharp
public sealed class CartProfile : Profile
{
    public CartProfile()
    {
        CreateMap<Cart, CartDto>();
        CreateMap<CartItem, CartItemDto>();
    }
}
```

Use `IMapper.Map<TDest>(source)` from inside the handler. Never expose AutoMapper to the Web layer.

---

## Infrastructure repositories

Concrete implementations sit in `TheShop.Infrastructure/Persistence/Repositories/`. Each repository takes the Supabase client via DI, queries a `*Record` row, and maps to a domain entity.

```csharp
namespace TheShop.Infrastructure.Persistence.Repositories;

public sealed class SupabaseProductRepository(Supabase.Client client) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await client
            .From<ProductRecord>()
            .Where(x => x.Id == id)
            .Single(ct);

        return response?.ToDomain();
    }
}
```

- `ProductRecord` is an Infrastructure type carrying Supabase-specific attributes (`[Table]`, `[PrimaryKey]`, `[Column]`).
- `ToDomain()` is an extension defined in `Infrastructure/Persistence/Mappers/`.
- The repository never returns `ProductRecord` — only the Domain `Product`.
- Persist methods accept `CancellationToken` and pass it to the SDK call.

See `examples/infrastructure-repository.md` for the full file pattern.

---

## Stripe / Resend adapters

Same shape as repositories: implement an Application interface, take the SDK client via DI, translate inputs/outputs into Application types.

```csharp
public sealed class StripePaymentService(StripeClient stripe) : IPaymentService
{
    public async Task<Result<PaymentIntentDto>> CreateIntentAsync(decimal amount, string currency, CancellationToken ct)
    {
        var options = new PaymentIntentCreateOptions { Amount = (long)(amount * 100), Currency = currency };
        var intent = await new PaymentIntentService(stripe).CreateAsync(options, cancellationToken: ct);
        return Result.Ok(new PaymentIntentDto(intent.Id, intent.ClientSecret));
    }
}
```

The Application layer sees only `IPaymentService` and `PaymentIntentDto`. Stripe types never escape Infrastructure.

---

## State stores (Web — Rule 4 boundary)

A state store is a Web-layer cache that the page hydrates from the Application layer's responses. It must NOT call Application directly — pages do that and feed results in.

```csharp
public sealed class CartState
{
    private CartDto? _cart;
    public CartDto? Cart => _cart;
    public int ItemCount => _cart?.Items.Sum(i => i.Quantity) ?? 0;

    public event Action? OnChange;

    public void UpdateFromDto(CartDto cart) { _cart = cart; NotifyStateChanged(); }
    public void Clear() { _cart = null; NotifyStateChanged(); }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
```

---

## NuGet packages — current targets

### Application
```xml
<PackageReference Include="MediatR" Version="12.*" />
<PackageReference Include="FluentValidation" Version="11.*" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />
<PackageReference Include="AutoMapper" Version="13.*" />
<PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.*" />
<PackageReference Include="ErrorOr" Version="1.*" />
```

### Infrastructure
```xml
<PackageReference Include="supabase-csharp" Version="1.*" />
<PackageReference Include="Stripe.net" Version="46.*" />
<PackageReference Include="Resend" Version="0.*" />
<PackageReference Include="Polly" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.*" />
```

### Web
```xml
<PackageReference Include="MudBlazor" Version="7.*" />
<PackageReference Include="Blazored.LocalStorage" Version="4.*" />
<PackageReference Include="Microsoft.Extensions.Localization" Version="8.*" />
<PackageReference Include="Microsoft.AspNetCore.Localization" Version="8.*" />
```

### Tests
```xml
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
<PackageReference Include="NSubstitute" Version="5.*" />
<PackageReference Include="bunit" Version="1.*" />
<PackageReference Include="Testcontainers.PostgreSql" Version="3.*" />
```

---

## Common mistakes

| Mistake | Fix |
|---|---|
| Handler throws `NotFoundException` for a missing entity | Return `Result.Fail(nameof(Strings.X))` |
| Handler returns the entity | Return the DTO via `IMapper.Map<TDto>` |
| `result.Fail("ProductNotFound")` magic string | `result.Fail(nameof(Strings.ProductNotFound))` |
| Page injects a repository directly | Pages dispatch via `IMediator.Send(...)`; repositories are an Application interface |
| Repository name embeds the provider in the interface (`ISupabaseRepository`) | Application interfaces are provider-neutral (`IProductRepository`); only the concrete is `Supabase*` |
| Validator wired ad-hoc inside the handler | One `AbstractValidator<TCommand>` per request, run via `ValidationBehavior<,>` pipeline |
| AutoMapper profile registered in the Web layer | Profiles live in Application; Web only resolves `IMapper` indirectly through DI |
| `Task` return type with no `CancellationToken` parameter | Add `CancellationToken ct` and propagate |

For interfaces declared in Application but consumed only inside Web (`BusyState`, `Routes`), see `architecture-core.md` §Cross-cutting Web-only primitives — those don't follow this pattern by design.

# Example — Application Command + Handler

Canonical MediatR `Command` + `Handler` pair. Shows the **dispatch → load → invoke domain → persist → map → return Result** pattern enforced by Rules 4, 5, 7, 8, 12.

```csharp
namespace TheShop.Application.Features.Cart.Commands;

public record AddToCartCommand(Guid ProductId, int Quantity)
    : IRequest<Result<CartDto>>;

public sealed class AddToCartCommandValidator : AbstractValidator<AddToCartCommand>
{
    public AddToCartCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

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

Highlights:
- Command is a `record` (Rule 9).
- Validator is a sibling type. Validation runs via the project's `ValidationBehavior<,>` pipeline — not inline inside the handler.
- Handler is `sealed`, primary constructor, no backing fields.
- `Handle(...)` accepts `CancellationToken`; it propagates to every async call (Rule 8).
- Domain invariants run on `cart.AddItem(...)` — handler does not re-implement them (Rule 6).
- Expected failures → `Result.Fail<T>(nameof(Strings.X))` (Rule 5, Rule 12). Exceptions thrown by Domain are caught and converted to `Result.Fail`.
- Entity never leaves the handler; `IMapper` produces `CartDto` (Rule 7).
- No `using Supabase;` — the handler only knows the interfaces (`IProductRepository`, `ICartRepository`).

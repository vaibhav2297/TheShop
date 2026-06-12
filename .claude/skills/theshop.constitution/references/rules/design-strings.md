# Design — Strings & Localization

> Implementation guide for Rules 11 and 12 from `SKILL.md`. Covers `Strings.resx` setup, the three access patterns, resource key naming, and the Application-layer producer pattern. The rules themselves live in `SKILL.md`; this file does not restate them.

---

## File layout

```
src/TheShop.Web/Resources/
├── Strings.resx              ← default (English)
└── Strings.fr.resx           ← French translations
```

One `Strings.resx` holds every user-facing string for the application. Scope keys via the naming convention (below) to avoid collisions.

### The typed accessor is auto-generated — never write it

The strongly-typed C# accessor class that Rule 11 depends on (`Strings.Designer.cs` — a static class with one property per resource key) is **auto-generated on build**. The generator wiring (`PublicResXFileCodeGenerator`) already lives in `TheShop.Web.csproj`; it requires no setup or maintenance.

When adding strings, edit **only** `Strings.resx` / `Strings.fr.resx` and build. **Never write or edit `Strings.Designer.cs` by hand** — it regenerates from the `.resx` automatically.

```csharp
// Auto-generated — DO NOT EDIT
namespace TheShop.Web.Resources;

public class Strings
{
    public static string AddToCart =>
        ResourceManager.GetString("AddToCart", Culture);

    public static string ProductDetail_PageTitle =>
        ResourceManager.GetString("ProductDetail_PageTitle", Culture);
    // ... one property per resource key
}
```

Each property returns the localized value for the current culture automatically.

### Why French resources from day one

This is a Canadian e-commerce business. Quebec's Bill 96 strengthens French-language requirements for businesses operating in Quebec. Even if French translations are added later, **scaffold the file structure now** — adding `Strings.fr.resx` later becomes a content task, not an architectural one.

---

## Resource key naming

Use `{Context}_{Purpose}`. Keys must be valid C# identifiers (no hyphens, no spaces, no leading digits) since they become property names on the generated `Strings` class.

| Kind | Pattern | Example |
|---|---|---|
| Page title | `{Page}_PageTitle` | `ProductDetail_PageTitle`, `Cart_PageTitle` |
| Button label | `{Action}` or `{Page}_{Action}` | `AddToCart`, `Save`, `Checkout_PlaceOrder` |
| Field label | `{Field}_Label` | `Email_Label`, `Password_Label` |
| Placeholder | `{Field}_Placeholder` | `Email_Placeholder` |
| Helper text | `{Field}_Hint` | `Password_Hint` |
| Error / validation | `{Field}_{Rule}` | `Email_Required`, `Password_TooShort` |
| Empty state | `Empty_{Context}` | `Empty_Cart`, `Empty_NoResults` |
| Toast — success | `{Action}_Success` or `Added{Noun}` | `AddedToCart`, `OrderPlaced` |
| Toast — error | `{Action}_Failed` | `AddToCart_Failed` |
| Confirmation | `Confirm_{Action}` | `Confirm_Delete`, `Confirm_Logout` |
| Shared / single-word | one word | `Cancel`, `Save`, `Continue` |

---

## The three access patterns

There are exactly three allowed ways to access localized strings. Magic-string indexer access (`Localizer["AddToCart"]`) is never allowed.

### Pattern 1 — `Strings.{Key}` (default — compile-time-known keys)

Direct property access on the auto-generated class. Fully type-safe, no `IStringLocalizer` injection, easiest to read.

```razor
@using TheShop.Web.Resources

<PageTitle>@Strings.ProductDetail_PageTitle</PageTitle>

<MudButton OnClick="@AddToCart">@Strings.AddToCart</MudButton>

<MudTextField Label="@Strings.Email_Label"
              Placeholder="@Strings.Email_Placeholder"
              HelperText="@Strings.Email_Hint" />
```

In code-behind:

```csharp
Snackbar.Add(Strings.AddedToCart, Severity.Success);
```

For format strings with placeholders, `string.Format` works the same way:

```razor
@* Strings.resx: Product_StockWarning = "Only {0} left in stock" *@
<MudAlert>@string.Format(Strings.Product_StockWarning, _product.Stock)</MudAlert>
```

### Pattern 2 — `Localizer[runtimeKey]` (only when the key is determined at runtime)

When the key isn't known until runtime — for example when the Application layer returns a key as a string in `Result.Fail()` — inject `IStringLocalizer<Strings>` and use the indexer with the runtime value. This is the only legitimate use of the indexer.

```razor
@inject IStringLocalizer<Strings> Localizer
```

```csharp
private async Task AddToCart()
{
    var result = await Mediator.Send(cmd);
    if (!result.IsSuccess)
    {
        // result.Error is a string like "ProductNotFound" returned from Application
        Snackbar.Add(Localizer[result.Error], Severity.Error);
    }
}
```

```razor
@if (!result.IsSuccess)
{
    <MudAlert Severity="Severity.Error">@Localizer[result.Error]</MudAlert>
}
```

### Pattern 3 — `Localizer[nameof(Strings.Key)]` (rare — testing or scoped culture switching)

For tests that inject a mocked `IStringLocalizer<Strings>`, or for components that need explicit `IStringLocalizer` for scoped culture switching, use `nameof()` so the key stays compile-safe while still going through the localizer interface.

```razor
<MudButton>@Localizer[nameof(Strings.AddToCart)]</MudButton>
```

Use this only when there's a concrete reason to involve `IStringLocalizer`. For 95% of UI code, Pattern 1 is the right choice. Don't reach for `IStringLocalizer` "just in case" — the typed accessor in Pattern 1 already handles culture switching automatically.

---

## Decision summary

| You have… | Use |
|---|---|
| A static, compile-time-known key | `@Strings.AddToCart` |
| A runtime key (`result.Error`, dynamic input) | `@Localizer[runtimeKey]` |
| A mocked localizer in a test | `@Localizer[nameof(Strings.AddToCart)]` |
| A string literal as the key | **Forbidden.** Use Pattern 1. |

---

## Application-layer producer pattern (Rule 12)

Application returns resource **keys**, not English text:

```csharp
// Even better — compile-time-checked even on the producer side
return Result.Fail<CartDto>(nameof(Strings.ProductNotFound));
```

```csharp
public sealed class CartCapacityExceededException : DomainException
{
    public CartCapacityExceededException()
        : base(nameof(Strings.CartCapacityExceeded)) { }
}
```

Web then translates the runtime key via Pattern 2:

```razor
@if (!result.IsSuccess)
{
    <MudAlert Severity="Severity.Error">@Localizer[result.Error]</MudAlert>
}
```

Application stays language-agnostic AND compile-safe.

> **Wait — Application is in `TheShop.Application/`, but `Strings` lives in `TheShop.Web/Resources/`. Doesn't Application import Web?**
> No. The Application project references the generated `Strings.Designer.cs` class through a shared resource project pattern, or via a string-key constants class in `TheShop.Application/Common/`. Whichever pattern is in use, the **rule** holds: never pass a magic string into `Result.Fail`. The compiler check is what matters; the wiring is mechanical.

---

## Coverage — what counts as "user-facing"

Rule 11 covers every string a user reads. That includes:

- Page titles, headings, body text
- Button labels and link text
- Form field labels, placeholders, helper text
- Validation messages
- Toast / snackbar messages
- Modal / dialog titles and content
- Empty state messages
- Loading text
- Error messages
- ARIA labels and accessibility text
- Tooltip content
- Email subject lines and body templates
- Meta tags and SEO descriptions

**Exempt:** Constants (`const string ApiVersion = "v1"`), log messages, internally-thrown exception messages (caught and converted to resource keys), test assertions.

---

## Common mistakes

| Mistake | Fix |
|---|---|
| `<MudText>Add to Cart</MudText>` | `<MudText>@Strings.AddToCart</MudText>` |
| `<MudButton>@Localizer["AddToCart"]</MudButton>` | `<MudButton>@Strings.AddToCart</MudButton>` |
| Typo: `Localizer["AddtoCart"]` — silent runtime failure showing the literal key | Pattern 1 — typo fails at compile time |
| Application: `Result.Fail<T>("ProductNotFound")` | `Result.Fail<T>(nameof(Strings.ProductNotFound))` |
| Missing French translation: French page shows English | At minimum, add `[TODO]`-prefixed translation in `Strings.fr.resx` so the gap is visible |
| Resource key with hyphen: `Add-to-cart` | Use snake-case / PascalCase only — keys become C# identifiers |
| `Localizer[nameof(Strings.AddToCart)]` everywhere | Pattern 3 is for tests / scoped culture switching; default to Pattern 1 |

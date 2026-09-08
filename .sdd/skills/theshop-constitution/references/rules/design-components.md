# Design — Components

> Implementation guide for Rules 14, 17, 20, 22, 23, 24, 25 from `SKILL.md`. Covers when to extract a reusable component, the `MudComponentBase` + `Class`/`Style` forwarding pattern, per-MudBlazor-component rules, the busy-state surface, and code-behind split. The rules themselves live in `SKILL.md`; this file does not restate them.

---

## Deciding whether to extract a reusable component (Rule 25)

Before writing a new component, decide whether a component is the right unit. The forwarding rules (`MudComponentBase`, `Class`/`Style`, builders) only matter once you've decided extraction is justified. Default to **inline markup** until an *extract* trigger fires — and stop if any *avoid* signal is also true.

### Extract when

- The same UI **and** behaviour repeats in two or more places today, non-trivially.
- Design consistency across pages depends on it (product card, OTP input, page header).
- Logic and markup belong together — extracting just one would split a tight coupling.
- The component has a **single, clearly named** responsibility (`ShopProductCard`, `ShopOtpInput`).
- The pattern is stable and unlikely to need restructuring in the next handful of changes.
- Reuse demonstrably improves maintenance or readability at the call sites.
- You can name it specifically — never `Generic*`, `Common*`, `Shared*`, or numeric suffixes.

### Avoid when

- Used **only once** today and you cannot point at a concrete second use.
- The only motivation is to reduce lines in the parent page.
- You struggle to name it or explain its responsibility in one sentence.
- It would need many parameters / boolean flags to satisfy its callers (sign of two components hiding in one).
- It mixes multiple unrelated responsibilities (presentation + business logic + side effects).
- Only the **logic** repeats — not the markup. Extract a service, helper, or `*State` store instead.
- The markup is tiny (one or two MudBlazor components in a `MudStack`) and the abstraction adds no semantics.
- You're future-proofing for a hypothetical caller. Wait for the second real call site.

**Rule of thumb:** when in doubt, inline first; extract on the second real call site. Per `CLAUDE.md`: *three similar lines is better than a premature abstraction.* This applies double for Razor — the cost of an awkward component (parameter explosion, slot juggling, render-tree churn) is high.

---

## Reusable component skeleton (Rules 23, 24)

Every reusable component:
1. Inherits from `MudComponentBase` (directly or transitively).
2. Forwards `Class` and `Style` to its root element.

### Direct inheritance examples

```csharp
// ✅ Direct child of MudComponentBase
public partial class ShopProductCard : MudComponentBase { }

// ✅ Transitive — chain anywhere is fine
public partial class ShopOrderCard : ShopProductCard { }       // ShopProductCard : MudComponentBase

// ✅ Extending a Mud component (Mud components inherit from MudComponentBase)
public partial class ShopBrandedButton : MudButton { }

// ❌ Missing MudComponentBase entirely
public partial class ShopProductCard : ComponentBase { }       // no Class/Style/UserAttributes plumbing
```

A consumer must always be able to write `<ShopProductCard Class="my-spacing" Style="..." />` and have it work without the component re-declaring pass-through attributes.

### Pattern A — root has no internal classes/styles

Forward `Class` and `Style` directly:

```razor
@* ShopSection.razor — pass-through *@
<MudGrid Class="@Class" Style="@Style">
    <MudPaper>
        @ChildContent
    </MudPaper>
</MudGrid>
```

### Pattern B — root has internal classes/styles

Compose with `CssBuilder` / `StyleBuilder` and **end the chain with `.AddClass(Class)` / `.AddStyle(Style)`** so the consumer's values land last and win:

```razor
@* ShopAlert.razor *@
<MudGrid Class="@Classname" Style="@Stylename">
    <MudPaper>
        @ChildContent
    </MudPaper>
</MudGrid>
```

```csharp
// ShopAlert.razor.cs
protected string Classname => new CssBuilder("mud-alert")
    .AddClass("mud-dense", Dense)
    .AddClass("mud-square", Square)
    .AddClass(Class)              // consumer's Class last
    .Build();

private string Stylename => new StyleBuilder()
    .AddStyle("margin-top", "4px")
    .AddStyle(Style)              // consumer's Style last
    .Build();
```

### Anti-pattern — silently dropping consumer overrides

```razor
@* ❌ consumer's Class/Style is silently dropped *@
<MudGrid Class="mud-alert">
    <MudPaper>@ChildContent</MudPaper>
</MudGrid>
```

This is a violation of Rule 24 even though the markup "works" — every call-site override goes nowhere.

---

## General component rules

### States — every interactive component must visually handle

- Default
- Hover
- Active / pressed
- Focus (keyboard accessibility)
- Disabled
- Loading (where applicable)

### Naming convention

`Component / Type / Variant / State`. Examples:
- `Button / Primary / Large / Default`
- `Input / Text / Default / Focused`
- `Card / Product / Default`

Avoid `Button1`, `NewButton`, `FinalCard`, `Generic*`, `Common*`.

### Variants

Components support the MudBlazor standard set where applicable:
- Buttons: `Variant.Filled`, `Variant.Outlined`, `Variant.Text`
- Sizes: `Size.Small`, `Size.Medium`, `Size.Large`
- Colors: `Color.Primary`, `Color.Secondary`, `Color.Tertiary`

### Base components — only wrap MudBlazor when customisation is genuinely needed

If standard MudBlazor components meet the need, use them directly. Don't create wrapper components for the sake of "branding" alone — the theme covers that.

---

## Per-component rules

### `MudTextField` (Rule 17)

**Always `Placeholder`, never `Label`.** The project's input style relies on placeholder-only fields — `Label` produces an outline/floating-label layout we explicitly don't want.

```razor
@* ✅ *@
<MudTextField @bind-Value="_email"
              Placeholder="@Strings.Email_Placeholder"
              HelperText="@Strings.Email_Hint" />

@* ❌ uses Label *@
<MudTextField @bind-Value="_email"
              Label="@Strings.Email_Label" />
```

If a design genuinely needs a label-above-input pattern, render the label as a separate `<MudText>` above the field — don't fall back to `MudTextField.Label`:

```razor
@* ✅ visible field name above the input *@
<MudText Typo="Typo.caption">@Strings.Email_Label</MudText>
<MudTextField @bind-Value="_email"
              Placeholder="@Strings.Email_Placeholder" />
```

---

## Busy state — `BusyState`, `BusyKeys`, `BusyFor`, `ShopLoadingOverlay` (Rule 22)

The project owns spinner placement and styling centrally. Pages must not hand-roll loaders.

### Inline button busy state

Wrap the control in `<BusyFor Key="@BusyKeys.X" Context="busy">` and bind `MudButton.Disabled="@busy"` plus an inline `MudProgressCircular` when `busy`. `BusyFor` subscribes to `BusyState.Changed` and re-renders only its child fragment on per-key transitions.

```razor
<BusyFor Key="@BusyKeys.Auth.SignIn" Context="busy">
    <MudButton Variant="Variant.Filled"
               Color="Color.Primary"
               Disabled="@(!_isFormValid || busy)"
               OnClick="OnSubmitAsync">
        @if (busy)
        {
            <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
        }
        @Strings.Auth_SendCode
    </MudButton>
</BusyFor>
```

The page drives the busy state explicitly:

```csharp
private async Task OnSubmitAsync()
{
    await BusyState.RunAsync(BusyKeys.Auth.SignIn, async () =>
    {
        var result = await Mediator.Send(new RequestSignInOtpCommand(_email));
        // ...
    });
}
```

### App-blocking busy state

`<ShopLoadingOverlay />` is mounted once in `MainLayout.razor` and observes `BusyKeys.Global`. Trigger for app-blocking work (session restore, sign-out, etc.):

```csharp
await BusyState.RunAsync(BusyKeys.Global, () => RestoreSessionAsync());
```

### Rules summary (full statement in `SKILL.md` Rule 22)

- Never hand-roll `MudProgressCircular` outside the `BusyFor` `ChildContent` fragment — placement and styling live in that component.
- No `_isBusy` boolean fields in pages. `BusyState` is the only source of truth.
- `BusyKeys` constants only — no magic strings at call sites.

---

## Code-behind separation (Rule 20)

Every `.razor` file with logic has a sibling `.razor.cs` partial class. Markup-only files (pure display components) may stay single-file.

### What goes where

**`.razor` file:**
- Markup (component tree, render fragments)
- Directives: `@inherits`, `@implements`, `@typeparam`, `@attribute`
- Local `@using` directives for namespaces the markup references
- **No** `@page` directive — route declarations live in code-behind

**`.razor.cs` file:**
- `public partial class X : ComponentBase` (or `LayoutComponentBase`)
- `[Route(Routes.X)]` attribute for pages
- All `[Inject]`, `[Parameter]`, fields, methods, lifecycle hooks, `IDisposable`

### Example

```razor
@* Pages/Auth/SignIn.razor *@
@attribute [AllowAnonymous]
@using TheShop.Web.Common
@using TheShop.Web.Components.Common

<PageTitle>@Strings.SignIn_PageTitle</PageTitle>
<MudContainer>...</MudContainer>
```

```csharp
// Pages/Auth/SignIn.razor.cs
using Microsoft.AspNetCore.Components;
using TheShop.Web.Common;

namespace TheShop.Web.Pages.Auth;

[Route(Routes.Auth.SignIn)]
public partial class SignIn : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    // ...
}
```

### Rules summary

- No inline `@code` blocks larger than ~5 lines.
- Page namespaces follow folder structure (`Pages/Auth/SignIn.razor.cs` → `TheShop.Web.Pages.Auth`).
- Add `@using` directives in the `.razor` file that needs them — don't pollute `_Imports.razor` with feature-specific namespaces.

For canonical full-file patterns, see `examples/web-page.md` and `examples/web-component.md`.

---

## Common mistakes

| Mistake | Fix |
|---|---|
| Reusable component extends `ComponentBase`, not `MudComponentBase` | Inherit from `MudComponentBase` (direct or transitive) — Rule 23 |
| Hardcoded `Class="mud-alert"` on the root, ignoring `@Class` parameter | Use Pattern A or Pattern B; consumer's `Class` is the last `.AddClass(...)` in the chain |
| Single-use wrapper that only re-orders default `MudButton` parameters | Inline at the call site; revisit on a second real caller — Rule 25 |
| Component with five boolean flags (`IsLarge`, `IsPrimary`, `IsDisabledOnSubmit`, `IsCompact`, `HasIcon`) | This is two or three components hiding in one — split, or stop and inline |
| `<MudTextField Label="...">` | Use `Placeholder`; if a visible label is needed, render a sibling `<MudText Typo="Typo.caption">` |
| Hand-rolled `MudProgressCircular` next to a button | Wrap with `<BusyFor>`, drive with `BusyState.RunAsync(BusyKeys.X, ...)` |
| `private bool _isBusy;` field in a page | Banned. Use `BusyState` keyed by `BusyKeys.X` |
| `BusyState.RunAsync("sign-in", ...)` magic string | Use `BusyKeys.Auth.SignIn` constant |
| `@page "/products/{Slug}"` in markup with a `partial class` | Move to `[Route(Routes.Products.Detail)]` on the code-behind |
| 30-line `@code { }` block in `.razor` | Move to `.razor.cs` partial |

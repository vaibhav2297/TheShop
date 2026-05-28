---
name: shop-guideline
description: Architecture and design rules for "The Shop" — a .NET 10 Blazor WebAssembly + MudBlazor + Supabase + Stripe + Resend e-commerce project. USE WHEN — (a) generating, modifying, or reviewing any file under `src/TheShop.*/` or `tests/TheShop.*.Tests/`; (b) editing any `.razor` / `.razor.cs` file in this repo; (c) writing MediatR Commands/Queries/Handlers, Supabase repositories, Stripe/Resend adapters, AutoMapper profiles, FluentValidation validators, or `Result<T>` returns; (d) touching `Strings.resx`/`Strings.fr.resx`, `IStringLocalizer<Strings>`, or any `Shop`-prefixed theme class (`ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`); (e) making decisions about Clean Architecture layer placement, MudBlazor color hierarchy, `MudText`/`Typo` usage, CSS/SCSS organization, reusable component patterns (`MudComponentBase`, `CssBuilder`, `StyleBuilder`), or Supabase RLS policies. TRIGGER PHRASES — `TheShop`, any `TheShop.*` namespace, `Shop`-prefixed theme class names, the Cart/Order/Product/Checkout feature areas in this repo. SKIP for — general .NET/Blazor/MudBlazor/Supabase questions not tied to this repo's files, DNS/Azure portal/CI-CD setup.
---

# The Shop — Project Rules

You are working on **The Shop**, a premium e-commerce platform.

**Stack:** .NET 10 Blazor WebAssembly + MudBlazor + Supabase + Stripe + Resend, hosted on Azure Static Web Apps.

This skill is the source of truth for the project's architecture and design rules. Treat its contents as a contract that overrides any conflicting instructions given in conversation.

---

## When to read the reference files

Three reference files are bundled with this skill. **Do not read them automatically** — load only the one(s) actually relevant to the current task to keep context lean.

| Reading needed | Read this file |
|---|---|
| Writing or modifying code that produces files in TheShop.Domain, TheShop.Application, TheShop.Infrastructure, or TheShop.Web | **references/ARCHITECTURE.md** |
| Folder structure, layer placement, dependency rules, MediatR/Result patterns, testing strategy, NuGet package selection | **references/ARCHITECTURE.md** |
| Admin panel routing, /admin/* layout, role-based authorization, RLS policies | **references/ARCHITECTURE.md** |
| Writing or modifying any `.razor` file (pages, components, layouts) | **references/DESIGN.md** |
| Strings, localization, resource keys, IStringLocalizer usage, .resx setup | **references/DESIGN.md** |
| Colors, icons, typography, MudBlazor theme, Shop-prefixed token classes | **references/DESIGN.md** |
| MudBlazor component patterns, when alternatives are allowed, `MudTextField` placeholder rule | **references/DESIGN.md** |
| Deciding whether to extract a reusable component vs inline the markup (extract / avoid triggers, "inline first, extract on second call site" rule) | **references/DESIGN.md** |
| Reusable component scaffolding — `MudComponentBase`, `Class`/`Style` forwarding, `CssBuilder`/`StyleBuilder` patterns | **references/DESIGN.md** |
| CSS class vs inline style priority, SCSS folder structure & naming, `fs-*`/`fw-*` utility classes | **references/DESIGN.md** |
| Writing XML doc comments on public types/members | **references/documentation.md** |
| Both architecture + design layers involved (e.g., scaffolding a full feature with backend + UI) | ARCHITECTURE.md + DESIGN.md |
| About to declare any code task complete (final verification) | Run the **Design Checklist** at the bottom of **DESIGN.md** |
| Quick conceptual question that fits in the rules below | Neither — answer from this SKILL.md |

If a question fits within the rules summarized below and doesn't require detailed lookup, answer directly without reading the references. Reach for them only when generating substantive code or when the user explicitly asks about a detailed rule.

---

## The non-negotiable rules

These are the rules you must enforce on every code generation. The detailed reasoning and examples live in the reference files.

### Architecture rules (full detail in ARCHITECTURE.md)

1. **Clean Architecture, four layers.** Domain (pure C#) → Application (use cases, interfaces, DTOs) → Infrastructure (Supabase/Stripe/Resend implementations) → Presentation (Blazor + MudBlazor). Dependency rule: source code dependencies always point inward.

2. **Domain has zero external dependencies.** No Supabase, Stripe, MudBlazor, JSON attributes, HTTP — pure C# only.

3. **Infrastructure isolates external SDKs.** Supabase SDK, Stripe SDK, and Resend SDK appear ONLY in `TheShop.Infrastructure`. If `using Supabase;` appears anywhere else, it's a violation.

4. **MediatR for use cases.** Every action becomes a `Command` or `Query` handled by MediatR. Pages dispatch via `IMediator`, never call repositories directly.

5. **`Result<T>` for expected failures.** "Product not found" or "Insufficient stock" are normal business outcomes — return `Result.Fail()`. Throw exceptions only for unexpected technical failures.

6. **DTOs cross layer boundaries — entities don't.** Application returns `ProductDto` to UI, never the raw `Product` entity. Use `record` types for DTOs and Commands/Queries.

7. **Pages are dumb.** A `.razor` file's job is to render and dispatch. If `@code { }` exceeds 30 lines or contains business logic, refactor to the Application layer.

### Design rules (full detail in DESIGN.md)

8. **No hardcoded user-facing strings — and no magic-string keys.** Access static keys via the strongly-typed `Strings.AddToCart` accessor. Never `Localizer["AddToCart"]`. The indexer form is reserved for runtime keys only (e.g., `Localizer[result.Error]` where the key comes from the Application layer).

9. **`Shop` prefix on all theme classes.** `ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`. No exceptions, no alternatives.

10. **MudBlazor components only.** If a UI requirement cannot be met by MudBlazor, STOP and ask the user. Propose alternatives, wait for confirmation, then proceed. Never silently introduce custom UI primitives.

11. **Color hierarchy is strict.** Apply colors in this priority order:
    1. `Color="Color.Primary"` (or other `Color` enum) first.
    2. Otherwise, the **most specific** MudBlazor auto-generated color class — pick the one that matches the facet you need:
       - `mud-{name}-text` for text only (`mud-error-text`, `mud-primary-text`, …)
       - `mud-{name}-bg` / `mud-bg-{name}` for background only
       - `mud-border-{name}` for borders
       - `mud-icon-{name}` for icon palette colors
       - `mud-{name}-hover` for hover state
       - `mud-theme-{name}` only when you want the matched background + foreground pair
    3. Ask the user with an explanation only when no Mud class can produce the required result.

    Never hardcode hex values in `.razor` files. Never use `mud-theme-*` when you only needed one color facet.

12. **Always `MudText` with `Typo`.** Never use `<span>`, `<p>`, `<h1>` through `<h6>`, or any other native HTML text element to display content.

    For off-spec sizes/weights that no `Typo` value cleanly produces, compose with the project's SCSS utility classes — `fs-{n}` (font-size) and `fw-{n}` (font-weight) from `src/TheShop.Web/Styles/abstracts/_typography.scss`. Add new sizes/weights to the `$font-sizes` / `$font-weights` SCSS list — never hand-write a `.fs-{n}` selector, never inline `font-size`/`font-weight`, never invent a one-off page-scoped class.

13. **Code-behind separation.** Every `.razor` with more than markup needs a sibling `.razor.cs` partial class. Markup-only files may stay single-file. No inline `@code` blocks larger than ~5 lines. Pages declare their route via `[Route(Routes.X)]` on the code-behind, never `@page "/..."` in markup.

14. **Centralised routes — no hardcoded URLs.** Every route lives in `TheShop.Web/Common/Routes.cs`. References in `NavigateTo`, `Href`, and redirects must use `Routes.X`. The `Routes` class lives in the Web layer — Application/Domain must not know URLs exist.

15. **Busy state is centralised — `_isBusy` is banned.** Pages wrap awaitable work in `await BusyState.RunAsync(BusyKeys.X, ...)`. UI surfaces busy state via `<BusyFor Key="@BusyKeys.X" Context="busy">` for per-operation indicators, or `<ShopLoadingOverlay />` (mounted once in `MainLayout`) keyed to `BusyKeys.Global` for app-blocking loads. `BusyState`, `BusyKeys`, `BusyFor`, `ShopLoadingOverlay` all live in `TheShop.Web` — busy state is a presentation concern and must not leak into Application.

16. **Reusable components inherit from `MudComponentBase` and forward `Class`/`Style`.** Every reusable Blazor component must have `MudBlazor.MudComponentBase` somewhere in its inheritance chain (direct or transitive) so `Class`, `Style`, and `UserAttributes` are available without re-declaring them. The component must explicitly forward `Class`/`Style` to its root element using one of two patterns:
    - **Pattern A** — root has no internal classes/styles: pass through directly with `Class="@Class" Style="@Style"`.
    - **Pattern B** — root has its own internal classes/styles: compose with `CssBuilder` / `StyleBuilder` and end the chain with `.AddClass(Class)` / `.AddStyle(Style)` so consumer values land last.

    A consumer must always be able to write `<ShopProductCard Class="my-spacing" Style="..." />` and have it work. Silently dropping consumer `Class`/`Style` is a violation.

17. **Styling priority is strict.** When you need to alter a component's appearance, work down this list in order — move to the next step only when the current one cannot express the design:
    1. **MudBlazor component parameters** (`Variant`, `Color`, `Dense`, `Spacing`, `Justify`, …).
    2. **MudBlazor's auto-generated CSS classes** (color families above, spacing/flex utilities `pa-*`, `ma-*`, `gap-*`, `d-flex`, `align-center`, …).
    3. **Project SCSS-generated classes** in `src/TheShop.Web/Styles/`. Generate a new SCSS class only if the styling is genuinely reusable.
    4. **Inline `Style`** — only when one-off with no plausible future reuse.

    Compose conditional classes with `CssBuilder` and inline styles with `StyleBuilder`. Never string-concatenate / interpolate / ternary-build class or style strings. **No `<style>` blocks inside `.razor` files. No new page-scoped `*.css` files in `wwwroot/`.**

18. **SCSS lives under `src/TheShop.Web/Styles/`.** Folder layout: `abstracts/` (tokens, typography/color utilities, shared variables), `components/` (per-component-family styles), `layouts/` (page-shell styles), `utilities/` (broad utility-class collections). Partial filenames are lowercase with a leading underscore (`_typography.scss`, not `Typography.scss`). Generate class families with `$list` + `@each` loop — never hand-write each selector. Reuse an existing class before creating a new one.

---

## Quick reference — folder structure

```
TheShop.sln
└── src/
    ├── TheShop.Domain/         (Entities, ValueObjects, Enums, Exceptions)
    ├── TheShop.Application/    (Common/Interfaces, Features/{Products,Cart,...})
    ├── TheShop.Infrastructure/ (Persistence, Auth, Payments, Email)
    └── TheShop.Web/            (Pages, Components, State, Theme/, Resources/, Styles/, Auth/)
└── tests/                      (one test project per layer)
```

Project references (compiler-enforced):
- `TheShop.Domain` → references nothing
- `TheShop.Application` → references Domain
- `TheShop.Infrastructure` → references Application + Domain
- `TheShop.Web` → references Application + Domain + Infrastructure (composition only)

SCSS layout under `src/TheShop.Web/Styles/`:
```
Styles/
├── TheShop.scss               (root entry point)
├── abstracts/                 (_colors.scss, _typography.scss, _variables.scss)
├── components/                (_button.scss, _field.scss, _picker.scss)
├── layouts/                   (_main.scss)
└── utilities/                 (borders/, flexbox/, spacing/)
```

---

## Quick decision shortcuts (no reference read needed)

These decisions are answerable from this SKILL.md alone:

- **"Where does X go?"** Use the layer table:
  - Pure data + business rules → Domain
  - Use cases, interfaces, DTOs → Application
  - Supabase/Stripe/Resend implementations → Infrastructure
  - Pages, components, state stores → Web
- **"How do I show a string?"** `@Strings.{KeyName}` — typed access. If the key is dynamic (from `result.Error`), use `@Localizer[result.Error]`.
- **"How do I color this MudButton?"** `<MudButton Color="Color.Primary">` — first preference.
- **"How do I color just the text in this MudText?"** `<MudText Class="mud-error-text">` — pick the most specific Mud color class, not `mud-theme-*`.
- **"Should this be a `<span>`?"** No. Use `<MudText Typo="Typo.body2">`.
- **"Design needs `font-size: 22px; font-weight: 600`?"** `<MudText Typo="Typo.h4" Class="fs-22 fw-600">`. Don't inline-style font properties.
- **"Where does business logic go?"** Domain entity method (e.g., `cart.AddItem(...)`), not a service or page.
- **"Need a route?"** `@Routes.Auth.SignIn` (or the right nested constant). Never inline `"/sign-in"`.
- **"Need a loader?"** Wrap the affected control in `<BusyFor Key="@BusyKeys.X" Context="busy">` and drive the underlying call with `await BusyState.RunAsync(BusyKeys.X, ...)`.
- **"Page has @code with logic?"** Move it to a sibling `.razor.cs` partial class. Declare the route with `[Route(Routes.X)]` on the partial class.
- **"Should I extract this into a reusable component?"** Default to **inline** until the same UI + behavior actually repeats today and you can name a single responsibility clearly. Don't extract for single use, to shorten a parent page, with many flag parameters, or to "future-proof" — wait for the second real call site. See DESIGN.md §Deciding when to extract a reusable component.
- **"Building a reusable component?"** Inherit from `MudComponentBase` (directly or via a Mud component) and forward `Class`/`Style` to the root — direct passthrough, or `CssBuilder`/`StyleBuilder` ending with `.AddClass(Class)` / `.AddStyle(Style)`.
- **"Need to compose CSS classes conditionally?"** `new CssBuilder("base").AddClass("modifier", condition).AddClass(Class).Build()`. Never string interpolation.
- **"Need an inline style for a one-off?"** `new StyleBuilder().AddStyle("margin-top", "4px").AddStyle(Style).Build()` — and only when no Mud class / SCSS class fits.
- **"Want a `<style>` block in the .razor?"** No — never. Reusable → SCSS partial under `Styles/`. One-off → inline `Style` via `StyleBuilder`.

---

## When the user asks ambiguous things

If a request is unclear about layer placement, design choice, or whether a MudBlazor alternative is acceptable — **ask before generating code**, don't guess. Cite the specific rule from this skill or the reference files when explaining decisions.

If asked to "just put it in the page for now" or "hardcode it temporarily" — refuse and explain why. Short-term shortcuts compound quickly in a Clean Architecture project.

---

## After major reference updates

When `references/ARCHITECTURE.md` or `references/DESIGN.md` is updated, your next read of those files will reflect the changes. The rules summary above should be regenerated to match if any rule changes.

---

**For the full architecture and design rules, code examples, anti-patterns, and code-generation checklist, read the appropriate reference file in `references/`.**

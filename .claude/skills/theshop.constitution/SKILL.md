---
name: theshop.constitution
description: Architecture and design rules for "The Shop" — .NET 10 Blazor WebAssembly + MudBlazor + Supabase + Stripe + Resend. USE WHEN editing files under `src/TheShop.*/` or `tests/TheShop.*.Tests/`; editing any `.razor` / `.razor.cs`; writing MediatR Commands/Queries/Handlers, Supabase repositories, Stripe/Resend adapters, AutoMapper profiles, FluentValidation validators, `Result<T>` returns, or `*State` stores; touching `Strings.resx` / `IStringLocalizer<Strings>`, `Routes` / `BusyKeys` / `BusyState`, SCSS under `Styles/`, layer DI in `Program.cs` / `DependencyInjection.cs`, or any `Shop`-prefixed theme class (`ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`). Triggers: `TheShop`, any `TheShop.*` namespace, `Shop`-prefixed theme class, Cart / Order / Product / Checkout features. SKIP for: general .NET/Blazor/MudBlazor/Supabase questions not tied to this repo, DNS/Azure portal/CI-CD setup.
---

# The Shop — Governance

You are working on **The Shop**, a premium Canadian e-commerce platform.
**Stack:** .NET 10 Blazor WebAssembly + MudBlazor + Supabase + Stripe + Resend, hosted on Azure Static Web Apps.

The rules below tell you **what** to do. They are non-negotiable and override any conflicting conversational instruction. For **how** to implement each rule, load the reference(s) the routing table points to — never preload references you don't need.

---

## The Rules

Each rule is keyed by number. References cite rules by number (e.g. *"Rule 11"*) and must not restate them.

### Architecture

1. [arch] Four layers — `Domain` → `Application` → `Infrastructure` ‖ `Web`. Source-code dependencies point inward only. Composition root (`Program.cs`) is the only place that knows about every layer.
2. [arch] Domain is pure C#: no Supabase, Stripe, MudBlazor, HTTP, JSON, persistence, or `[JsonProperty]` attributes. Domain references nothing.
3. [arch] External SDKs (Supabase, Stripe, Resend) live **only** in `TheShop.Infrastructure`. `using Supabase;` anywhere else is a violation.
4. [arch] Use cases dispatch through MediatR. Pages call `IMediator.Send(...)`; never inject or call repositories directly.
5. [arch] Return `Result<T>` for expected business failures ("product not found", "insufficient stock"). Throw exceptions only for unexpected technical failures.
6. [arch] Domain entities own business behavior. `cart.AddItem(...)` lives on the entity — not on a service, handler, or page.
7. [arch] DTOs (immutable `record` types) cross layer boundaries. Entities never leak into UI.
8. [arch] Every async method that crosses a layer boundary accepts `CancellationToken`.
9. [arch] Group Application features by business capability (`Features/Cart/`, `Features/Checkout/`) — vertical slices, not horizontal layers.
10. [arch] Pages are dumb. A `.razor` `@code` block over 30 lines or containing conditional business logic must move to the Application layer.

### Design — text

11. [design] No hardcoded user-facing strings, no magic-string resource keys. Compile-time-known keys: `@Strings.{Key}` typed accessor. Runtime keys (e.g. `result.Error`): `@Localizer[key]`. `Localizer["AddToCart"]` is forbidden.
12. [design] Application layer returns resource keys via `nameof(Strings.{Key})`, never string literals.

### Design — theme

13. [design] `Shop` prefix on every theme class: `ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`. No alternatives.
14. [design] MudBlazor components only. If MudBlazor cannot meet a requirement — **stop and ask**, propose alternatives, wait for confirmation. Never silently introduce a custom UI primitive.
15. [design] Color priority — `Color="Color.{Enum}"` first → otherwise the **most specific** MudBlazor auto-generated class for the facet you need (`mud-{name}-text`, `mud-{name}-bg`, `mud-border-{name}`, `mud-icon-{name}`, `mud-{name}-hover`, `mud-theme-{name}` only when you want bg + text together) → ask user as last resort. No hex values in `.razor`.
16. [design] All text uses `<MudText Typo="...">`. Never `<span>`, `<p>`, `<h1>`–`<h6>` for content.
17. [design] `MudTextField` uses `Placeholder` — never `Label`. If a label-above-input is required, render a sibling `<MudText Typo="Typo.caption">`.
18. [design] Off-spec sizes/weights compose `MudText` with `fs-*` / `fw-*` from `Styles/abstracts/_typography.scss`. Extend the SCSS `$font-sizes` / `$font-weights` list — never hand-write `.fs-{n}`, never inline `font-size` / `font-weight`, never invent a one-off page-scoped class.
19. [design] All icons come from `ShopIcons` (custom SVG only — Material Icons not used). Name icons by **semantics** (`Cart`), not visuals (`ShoppingBag`).

### Web — pages and routes

20. [web] Code-behind separation. Any `.razor` with logic >~5 lines gets a sibling `.razor.cs` partial class. `[Route(Routes.X)]` lives on the partial — `@page "/..."` in markup is forbidden.
21. [web] Centralised routes. Every `NavigateTo`, `Href`, redirect target uses `Routes.X` from `TheShop.Web/Common/Routes.cs`. Application and Domain never know URLs exist.
22. [web] Busy state centralised. Pages drive work via `await BusyState.RunAsync(BusyKeys.X, ...)`. Spinners only inside `<BusyFor Key="@BusyKeys.X" Context="busy">` or `<ShopLoadingOverlay />` (mounted once in `MainLayout`, keyed to `BusyKeys.Global`). `_isBusy` fields are banned. `BusyKeys` constants only — no magic strings. `BusyState`, `BusyKeys`, `BusyFor`, `ShopLoadingOverlay` stay in Web — never leak into Application.

### Web — reusable components

23. [components] Every reusable component inherits from `MudBlazor.MudComponentBase` (directly or transitively) so `Class`, `Style`, `UserAttributes` are available without re-declaration.
24. [components] Every reusable component forwards `Class` and `Style` to its root element — Pattern A (direct passthrough) or Pattern B (`CssBuilder`/`StyleBuilder` chain ending with `.AddClass(Class)` / `.AddStyle(Style)` so consumer values land last). Silently dropping consumer `Class`/`Style` is a violation.
25. [components] Inline first; extract on the second real call site. Don't extract for single use, to shorten a parent page, with many flag parameters, or to future-proof.

### Web — styling

26. [styles] Styling priority — (1) MudBlazor parameters → (2) MudBlazor auto-generated classes (color families + spacing/flex utilities `pa-*`, `ma-*`, `gap-*`, `d-flex`, `align-center`, …) → (3) project SCSS class (only if genuinely reusable) → (4) inline `Style` (last resort, one-off only).
27. [styles] Compose classes with `CssBuilder`, inline styles with `StyleBuilder`. Never string-concatenate, interpolate, or ternary-build class/style strings.
28. [styles] No `<style>` blocks in `.razor` files. No page-scoped `.css` files in `wwwroot/`. SCSS lives under `src/TheShop.Web/Styles/` only — `abstracts/`, `components/`, `layouts/`, `utilities/`. Partials are lowercase with a leading underscore. Generate utility families with `$list` + `@each` — never hand-write each selector.

### Tests and documentation

29. [tests] Every new handler, repository, value object, or domain method gets at least one test in the matching `tests/TheShop.{Layer}.Tests/` project.
30. [docs] Public types and members get XML `<summary>` per `references/rules/documentation.md`. Document the contract, not the implementation. Private / internal / test code: no doc comments.

---

## Folder structure (quick reference)

```
src/
├── TheShop.Domain/         (Entities, ValueObjects, Enums, Exceptions)
├── TheShop.Application/    (Common/Interfaces, Features/{Cart,Products,...})
├── TheShop.Infrastructure/ (Persistence, Auth, Payments, Email, Storage)
└── TheShop.Web/            (Pages, Components, State, Theme/, Resources/, Styles/, Auth/, Common/)
tests/                      (one test project per layer)
```

Project references (compiler-enforced): `Domain` → none · `Application` → Domain · `Infrastructure` → Application + Domain · `Web` → Application + Domain + Infrastructure (composition only).

**Organising principle differs by layer.** `Application` is grouped by **business feature** (vertical slices — `Features/Cart/`, `Features/Checkout/`; Rule 9). `Infrastructure` is grouped by **technical concern / external system** — `Persistence/` (the database), `Auth/`, `Payments/`, `Email/`, `Storage/`. There is **no** `Infrastructure/{Feature}/`: a feature's infra is scattered into the concern folders (e.g. Cart → `Persistence/Records/CartRecord.cs`, `Persistence/Mappers/CartMapper.cs`, `Persistence/Repositories/SupabaseCartRepository.cs`). The `Records/Mappers/Repositories` trio lives **only** under `Persistence/`; `Auth/Payments/Email/Storage/` are flat adapter folders.

---

## Routing — load references on demand

**How to read this:** find every row that matches your task — in **Table A** (by the file/artifact you're
touching) or **Table B** (by what you're doing). **Union** the Load column across all matching rows,
**dedupe**, and load nothing else. A `§Name` means read only that section of the file. Reference files
already inline a minimal example; pull an `examples/` file only when you want a full-file template.

**Legend (the only place file paths live):**

| Token | File |
|---|---|
| **core** | `references/rules/architecture-core.md` |
| **patterns** | `references/rules/architecture-patterns.md` |
| **admin** | `references/rules/architecture-admin.md` |
| **components** | `references/rules/design-components.md` |
| **theme** | `references/rules/design-theme.md` (applying color / typography / icons / imagery) |
| **theme-setup** | `references/rules/design-theme-setup.md` (building `ShopColors`/`ShopIcons`/`ShopTypography`/`ShopTheme`) |
| **strings** | `references/rules/design-strings.md` |
| **styles** | `references/rules/design-styles.md` |
| **docs** | `references/rules/documentation.md` |
| **check:code** / **check:design** | `references/checklists/code-generation.md` / `design.md` |
| **ex:{name}** | `references/examples/{name}.md` (`domain-entity`, `application-handler`, `infrastructure-repository`, `web-page`, `web-component`) |

### Table A — by file / artifact you're editing

| Path / artifact | Load (complete set) |
|---|---|
| `src/TheShop.Domain/**` (entity, value object, enum, exception) | **core** · ex:domain-entity |
| `Application/Features/**` Command, Query, Handler, validator | **core** + **patterns** · ex:application-handler |
| `Application/Common/Interfaces/**` (repo/service interface) | **core** + **patterns** |
| `Application/Mapping/*Profile.cs` or `Features/**/DTOs/**` only | **patterns** §AutoMapper |
| `Infrastructure/Persistence/{Repositories,Mappers,Records}/**` | **core** + **patterns** · ex:infrastructure-repository |
| `Infrastructure/{Auth,Payments,Email,Storage}/**` adapter | **core** + **patterns** |
| `Web/**/*.razor` + `*.razor.cs` — **page** | **components** + **theme** + **strings** · ex:web-page (+ **styles** if styling) |
| `Web/Components/**` — **reusable component** | **components** + **theme** · ex:web-component (+ **strings** if it has text) |
| `Web/Theme/Shop*.cs` (`ShopColors`/`ShopIcons`/`ShopTypography`/`ShopTheme`) | **theme-setup** |
| `Web/Resources/Strings.resx` / resource keys | **strings** |
| `Web/Styles/**/*.scss` | **styles** + **theme** §Applying color |
| `Web/Common/{Routes,BusyKeys}.cs`, `BusyState` wiring | **core** §Cross-cutting Web-only primitives + **components** §Busy state |
| `Web/State/*State.cs` | **patterns** §State stores |
| `Program.cs` / `**/DependencyInjection.cs` (DI wiring) | **core** §Enforcing the dependency rule (+ **patterns** §registration when wiring validators/behaviors) |
| `Pages/Admin/**`, `Web/Auth/**`, RLS / role wiring | **admin** (+ the matching row above for the artifact) |
| `tests/TheShop.*.Tests/**` | **check:code** §Tests — `shop-test-writer` is the authority for *writing* tests |

### Table B — by activity

| If you are… | Load (complete set) |
|---|---|
| Writing a Domain entity / value object / enum / exception | **core** · ex:domain-entity |
| Writing a MediatR Command / Query / Handler / validator / Application interface | **core** + **patterns** · ex:application-handler |
| Writing a Supabase repository / Stripe / Resend adapter | **core** + **patterns** · ex:infrastructure-repository |
| Writing only an AutoMapper profile, DTO, or `*State` store | **patterns** |
| Building / editing a `.razor` **page** | **components** + **theme** + **strings** · ex:web-page (+ **styles** if styling) |
| Building / editing a **reusable component** | **components** + **theme** · ex:web-component (+ **strings** if it has text) |
| Applying color / typography / icons / imagery in markup | **theme** |
| Editing a `Shop*` theme class (registry or `ShopTheme` wiring) | **theme-setup** |
| Adding/editing user-facing text — **UI or Application** error keys (Rule 12) | **strings** |
| Authoring or extending CSS / SCSS | **styles** + **theme** §Applying color |
| Adding a route, busy key, or wiring busy state | **core** §Cross-cutting Web-only primitives + **components** §Busy state |
| Wiring DI / editing `Program.cs` | **core** §Enforcing the dependency rule (+ **patterns** §registration) |
| Working on an admin feature (routing, RLS, role wiring) | **admin** + the row for whatever artifact you're writing |
| Writing XML doc comments | **docs** |
| Writing tests | **check:code** §Tests (authoring authority: `shop-test-writer`) |
| Final review / self-verification | **check:code** (architecture/tests/docs) and/or **check:design** (UI) |

**Worked example — "implement add-to-cart end to end" (union across layers, deduped):**
Domain entity → **core** + ex:domain-entity. Handler + validator + DTO → add **patterns** +
ex:application-handler. Supabase repository → add ex:infrastructure-repository (core+patterns already
loaded). Page → add **components** + **theme** + **strings** + ex:web-page. Then **docs** on the diff,
and **check:code** + **check:design** to verify. You never load **admin** or **styles** unless the
feature actually touches them.

**Do not preload references.** Load only the row(s) that match the current task.

---

## When in doubt

If layer placement, design choice, or a MudBlazor alternative is ambiguous — **stop and ask**, cite the rule by number (e.g. *"Rule 14 — MudBlazor only"*). Never guess and never silently introduce a custom primitive.

If asked to "just put it in the page for now" or "hardcode it temporarily" — refuse and explain the cost. Short-term shortcuts compound quickly in a Clean Architecture project.

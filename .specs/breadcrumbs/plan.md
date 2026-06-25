# Implementation Plan — Breadcrumbs

> Companion to `.specs/breadcrumbs/spec.md`. This plan is technical (HOW); the spec is non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

Build a reusable, layout-mounted breadcrumb system that renders a "you are here" trail on every page below a root, in the chrome between the app bar and the page content. The trail is **structural** — derived from the site/admin hierarchy via `Routes` and `Strings`, with dynamic item names (category, product, order) supplied by the page that owns the data — not from navigation history. Per user direction the trail is rendered with MudBlazor's `MudBreadcrumbs`, and the wiring is built to be **maintainable and extendible**: a single scoped state store, one reusable component, and one fluent trail builder, so adding a new page's trail is a one-line call. This is a **Web-layer-only** feature: no Domain, Application, Infrastructure, database, or RLS changes.

**Sequencing note.** Today the only storefront page that exists is `Home` (a root — no trail), plus the auth pages (excluded, and on `AuthLayout`). No `Products` listing, product-detail, category, or admin pages exist yet — their routes are constants the app bar links to, but the pages aren't built. So this plan delivers the breadcrumb **system** (built and tested now) and the **extension pattern**; each browsing/admin page gets its trail as it ships, exactly as the spec's admin-sequencing note frames it (spec §1).

## 2. Tech Stack

Scoped to what this feature actually uses.

- **Domain / Application / Infrastructure:** no changes. No entities, MediatR handlers, repositories, DTOs, migrations, or RLS.
- **Web:**
  - **MudBlazor** — `MudBreadcrumbs` + `BreadcrumbItem` (per user direction "use mudblazor breadcrumb"), `MudLink`, `MudIcon`, `MudText`.
  - **`MudBreakpointProvider`** (MudBlazor) — exposes the current breakpoint via `OnBreakpointChanged` to drive responsive collapse, with no manual viewport-service subscription.
  - **SCSS** (`src/TheShop.Web/Styles/`) — one new component partial for label truncation.
  - **Resources** — `Strings.resx` / `Strings.fr.resx` for the nav landmark label, the admin root label, and the collapse-expander label.
  - **bUnit** — component + state-store tests in `tests/TheShop.Web.Tests`.

## 3. High-level Architecture

A page declares its place in the hierarchy by pushing a trail into a scoped `BreadcrumbState`; the layout renders that trail once, consistently, for every page. Pages never render the breadcrumb component themselves.

```
Page (e.g. a future ProductDetail) loads its data, then:
   BreadcrumbState.Set(
       BreadcrumbTrail.Storefront()                 // seeds Home root from Routes + Strings
           .Add(category.Name, Routes.Category(category.Slug))
           .Current(product.Name))                  // last item, non-link
   ↓
BreadcrumbState (scoped, Web/State) raises OnChange
   ↓
MainLayout (subscribed) re-renders the breadcrumb slot:
   <ShopBreadcrumbs Items="@Breadcrumbs.Trail" />   // only when HasTrail
   ↓
ShopBreadcrumbs wraps MudBreadcrumbs:
   • chevron SeparatorTemplate (ShopIcons.Outlined.Chevron_Right)
   • ItemTemplate: parents → MudLink(href); current/removed → non-link text + aria-current="page"
   • responsive MaxItems (collapse middle on Xs/Sm) + ShopIcons ExpanderIcon
   • truncation via .shop-breadcrumb-item + title (full label)
   ↓
On NavigationManager.LocationChanged → BreadcrumbState.Clear() (next page re-Sets, or root pages leave it empty → no trail)
```

## 4. Data Model

No persistence, no DTOs, no domain types. The only "model" is MudBlazor's existing `BreadcrumbItem` value (`Text`, `Href`, `Disabled`, `Icon`) — reused directly, since the entire feature lives in the Web layer and crosses no layer boundary that would require a project DTO.

### Web types (new)

- **`BreadcrumbState`** (`Web/State/BreadcrumbState.cs`) — scoped store mirroring `AnnouncementState`. Holds `IReadOnlyList<BreadcrumbItem>? Trail`; exposes `bool HasTrail`, `event Action? OnChange`, `void Set(IReadOnlyList<BreadcrumbItem>)`, `void Clear()`.
- **`BreadcrumbTrail`** (`Web/Common/BreadcrumbTrail.cs`) — a small fluent builder. Static entry points seed the correct root:
  - `BreadcrumbTrail.Storefront()` → seeds `Home` (`Strings.Nav_Home`, `Routes.Home`).
  - `BreadcrumbTrail.Admin()` → seeds `Dashboard` (`Strings.Nav_Dashboard`, admin dashboard route once it exists).
  - Instance methods: `.Add(string text, string? href)` (intermediate level; `href == null` ⇒ rendered as plain, non-link text — covers the "removed parent" case), `.Current(string text)` (final, always non-link), `.Build()` → `IReadOnlyList<BreadcrumbItem>`. The terminal `.Current(...)` returns the list directly so call sites read as one expression.

## 5. Core Design Decisions

1. **Decision:** Render breadcrumbs from a **layout slot** driven by a scoped `BreadcrumbState`, not a `<ShopBreadcrumbs>` dropped into each page.
   - **Why:** The Figma frame places the trail as fixed chrome (a 42px bar between app bar `y=96` and content `y=138`) — consistent placement matters on a premium store. A single slot guarantees it. This mirrors the established `AnnouncementState` → `MainLayout` idiom (scoped store + `OnChange`/`StateHasChanged`/`Dispose`), so it's the lowest-risk, most maintainable shape — directly serving the user's "maintainable and extendible" direction.
   - **Rejected:** Per-page `<ShopBreadcrumbs Items=...>` — scatters markup, invites inconsistent placement, and duplicates the "no trail on roots" guard on every page.
   - **Rejected:** A pure route-to-trail resolver (URI → trail) — cannot supply dynamic item names (FR-6: category/product/order names live only on the page that loaded them).

2. **Decision:** Use MudBlazor's `MudBreadcrumbs` + `BreadcrumbItem` directly (per user direction); wrap only thin enough to apply theme + behavior in one place (`ShopBreadcrumbs`).
   - **Why:** Rule 14 (MudBlazor-only); the component meets every requirement. A wrapper is justified because the separator, item template, responsive collapse, and a11y attributes are non-trivial and must be identical everywhere (Rule 25 — real second-and-beyond call sites as pages ship).
   - **Rejected:** A custom breadcrumb primitive (Rule 14 violation) or a parallel breadcrumb DTO (no layer boundary is crossed — needless mapping).

3. **Decision:** Centralize trail construction in a fluent `BreadcrumbTrail` builder.
   - **Why:** "Maintainable and extendible" (user direction) — one source of truth for roots, fixed labels, and href wiring; a new page's trail is one declarative chain. Keeps `Routes`/`Strings` usage out of the component and pages' `@code` lean (Rule 10).
   - **Rejected:** Each page hand-assembling a `List<BreadcrumbItem>` — duplicates the root + label lookups and drifts over time.

4. **Decision:** Trails are structural (FR-5). The builder derives the skeleton from the fixed hierarchy; the page supplies item names. A storefront product follows its category path — `Home → Category → [Sub-category] → Product` — and falls back to `Home → Products → {Product}` when the product has no category (spec §4 constraint).
   - **Why:** Guarantees AC-5 (identical trail whether navigated or deep-linked) and the uncategorized-product edge case.

5. **Decision:** The current page and any removed/unavailable parent both render as **disabled** `BreadcrumbItem`s (non-link). The builder marks a parent disabled by passing `href: null`.
   - **Why:** Satisfies FR-4 (current = non-link) and the removed-parent edge case with one rendering path; MudBlazor renders `Disabled` items as plain text.

6. **Decision:** Responsive collapse via `MaxItems` driven by `MudBreakpointProvider`. `ShopBreadcrumbs` wraps its `MudBreadcrumbs` in a `MudBreakpointProvider` and recomputes `MaxItems` from the provider's `OnBreakpointChanged` callback — `MaxItems = 2` at `Sm` and below (middle levels collapse behind the `…` expander; first level + current stay visible), unset at `Md` and up (full trail). This keeps the responsive logic self-contained in the component with no `IBrowserViewportService` injection and no manual subscribe/dispose. The expander uses the existing `ShopIcons.Outlined.More_Horizontal` glyph (horizontal ellipsis).
   - **Why:** Spec edge case — middle levels collapse into an expandable "…" on small screens, full trail on desktop. MudBlazor's built-in collapse + expander is the native, accessible way to do this, and the provider component is the lightest-touch way to feed it the breakpoint.
   - **Rejected:** A fixed `MaxItems` (would collapse on desktop too); collapsing at `Xs` only (narrow `Sm` widths would overflow); CSS-only hiding of middle items (loses the accessible, clickable expander); injecting `IBrowserViewportService` / implementing `IBrowserViewportObserver` (more lifecycle boilerplate than the provider component for the same result).

7. **Decision:** Long-label truncation via a reusable SCSS class `.shop-breadcrumb-item` (ellipsis) plus the native `title` attribute carrying the full label.
   - **Why:** Applied to every item across every trail ⇒ genuinely reusable ⇒ SCSS, not inline `Style` (Rule 26/28). `title` gives the full name on desktop hover; on touch the truncation alone keeps the layout intact.

8. **Decision:** Accessibility — localized `aria-label` on the `<nav>` landmark (via `UserAttributes` on `MudBreadcrumbs`), `aria-current="page"` set explicitly by the `ItemTemplate` on the current item (not relying on MudBlazor's internal markup), and keyboard-focusable parent links (`MudLink`).
   - **Why:** AC-10. Item links are real `MudLink`s (keyboard-reachable, visible focus from the theme).

## 6. Core Functional Flow

### Flow 1: Browse to a product deep in a category (spec Behavior 1) — *applies as the product page ships*
1. The product page loads its product (with category path) — data it already owns.
2. In `OnParametersSetAsync`, it calls `BreadcrumbState.Set(BreadcrumbTrail.Storefront().Add(category.Name, Routes.Category(...)).Current(product.Name))`.
3. `BreadcrumbState` raises `OnChange`; `MainLayout` re-renders the slot with `<ShopBreadcrumbs Items="@Breadcrumbs.Trail" />`.
4. `ShopBreadcrumbs` renders `Home › Category › Wool Parka` — parents as `MudLink`, the product as non-link text with `aria-current="page"`.

### Flow 2: Jump back to a parent level (spec Behavior 2)
1. User clicks a parent `MudLink` (e.g. the category).
2. `MudLink` navigates to `item.Href` (a `Routes.*` value); `LocationChanged` fires; layout clears the trail; the destination page re-`Set`s its own (shorter) trail ending at that level.

### Flow 3: Admin opens a record to manage (spec Behavior 3) — *applies as admin pages ship*
1. A future admin record page calls `BreadcrumbState.Set(BreadcrumbTrail.Admin().Add(Strings.AdminProducts..., adminProductsRoute).Current(product.Name))`.
2. Slot renders `Dashboard › Products › Wool Parka`, Dashboard/Products clickable, the record non-link.

### Flow 4: View a breadcrumbed page in French (spec Behavior 4)
1. Site culture is `fr`. Fixed labels resolve through `Strings.*` (e.g. `Nav_Home` → "Accueil", `Nav_Dashboard` → "Tableau de bord"); the nav `aria-label` resolves to its French value. Item names are data and appear as stored.

### Flow 5: Deep page on a small screen (spec edge case)
1. `ShopBreadcrumbs` observes `Xs`/`Sm` via `IBrowserViewportService` and sets `MaxItems` so middle levels collapse behind the `…` expander; first level + current stay visible; the user expands to reveal the rest.

### Flow 6: Root page / no trail (spec §4, AC-7)
1. `Home` (and the auth pages, on `AuthLayout`) never call `Set`. `MainLayout` renders the slot only while `Breadcrumbs.HasTrail`, so no trail appears.

## 7. Development Plan

### Phase 1 — Resource strings
- Add to `Strings.resx`: `Breadcrumb_AriaLabel` ("Breadcrumb"), `Nav_Dashboard` ("Dashboard"), `Breadcrumb_ShowMore` ("Show more" — collapse-expander aria-label).
- Mirror in `Strings.fr.resx`: `Breadcrumb_AriaLabel` ("Fil d'Ariane"), `Nav_Dashboard` ("Tableau de bord"), `Breadcrumb_ShowMore` ("Afficher plus"). (Reuses existing `Nav_Home`, `Nav_Products`, `Nav_Categories`, `Nav_Brands`, `Nav_Deals`.)

### Phase 2 — Styles
- New partial `Styles/components/_breadcrumbs.scss` with `.shop-breadcrumb-item` (`display:inline-block; max-width; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; vertical-align:bottom`). Import it from `Styles/TheShop.scss`. If a max-width token is reused elsewhere, source it from `abstracts/_variables.scss`.

### Phase 3 — State store
- `Web/State/BreadcrumbState.cs` (sealed, mirrors `AnnouncementState`: `Trail`, `HasTrail`, `OnChange`, `Set`, `Clear`).
- Register `services.AddScoped<BreadcrumbState>();` in `Web/DependencyInjection.cs` (beside the other `*State` registrations).

### Phase 4 — Trail builder
- `Web/Common/BreadcrumbTrail.cs` — `Storefront()` / `Admin()` entry points + `.Add(text, href)` / `.Current(text)` / `.Build()`. Uses `Routes.*` and `Strings.*` only. Add a `Routes.Category(slug)` helper (and any other parent-route helpers) **only when** the corresponding page ships — see Section 11.

### Phase 5 — Web (the `ShopBreadcrumbs` component)

**Figma references** *(read by `shop-ui-implementer` at impl time — re-fetched then)*

- **File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop
- **Nodes:**
  - `2263:5417` — **Breadcrumb** component instance (the bar to translate): `Home › Products › Puffco Peak Pro`, full-width 42px, left-padded, chevron separators, current label in heavier weight than the clickable parents.
  - `2263:5413` — **Product Detail** page frame (context only): shows the breadcrumb bar sitting directly between the Appbar (`2263:5416`, y=96) and the page content (`2263:5418`, y=138) — confirms the layout-slot placement, not in-page markup.
- **Visual intent notes:** `2263:5417` is the breadcrumb chrome itself — match separator (chevron, not `/`), the parent-vs-current weight contrast, and the left inset. `2263:5413` is only to confirm where the bar lives in the page stack.

**Tasks**
- `Components/Common/ShopBreadcrumbs.razor` + `.razor.cs` — `: MudComponentBase`; `[Parameter] IReadOnlyList<BreadcrumbItem>? Items`.
  - Pattern B forwarding: root `MudBreadcrumbs` gets `Class="@Classname"` ending `.AddClass(Class)` and `Style="@Style"` (Rule 24/27).
  - `SeparatorTemplate` → `<MudIcon Icon="@ShopIcons.Outlined.Chevron_Right" Size="Size.Small" />` (Rule 19).
  - `ItemTemplate` → parents render `<MudLink Href="@context.Href" Class="shop-breadcrumb-item" title="@context.Text">`; the current/disabled item renders non-link themed text (`MudText`/`MudLink Disabled`) carrying `aria-current="page"` + `.shop-breadcrumb-item` + `title`.
  - Wrap the `MudBreadcrumbs` in a `MudBreakpointProvider`; bind its `OnBreakpointChanged` to a handler that sets `MaxItems = 2` at `Sm` and below and `null` at `Md`+ (default `null` until the first breakpoint resolves ⇒ full trail). Set `ExpanderIcon="@ShopIcons.Outlined.More_Horizontal"` (Rule 19) and pass the expander an `aria-label` of `Strings.Breadcrumb_ShowMore`.
  - Localized landmark: `aria-label="@Strings.Breadcrumb_AriaLabel"` on `MudBreadcrumbs` (lands on the `<nav>` via `UserAttributes`).

### Phase 6 — Layout integration
- `MainLayout.razor` — between `<ShopAppBar />` and `<MudMainContent>`, add `@if (Breadcrumbs.HasTrail) { <ShopBreadcrumbs Items="@Breadcrumbs.Trail" /> }`.
- `MainLayout.razor.cs` — inject `BreadcrumbState`; subscribe `Breadcrumbs.OnChange += StateHasChanged`; subscribe `NavigationManager.LocationChanged` → `Breadcrumbs.Clear()`; unsubscribe both in `Dispose`.

### Phase 7 — Tests
- `tests/TheShop.Web.Tests` (bUnit, feature-trait `breadcrumbs`):
  - `BreadcrumbStateTests` — `Set`/`Clear`/`HasTrail`/`OnChange`.
  - `BreadcrumbTrailTests` — Storefront/Admin roots; `.Add`/`.Current` ordering; `href:null` ⇒ disabled; uncategorized fallback; structural identity (same input ⇒ same trail).
  - `ShopBreadcrumbsTests` — representative storefront + admin trails: parents are links with correct `Href`; final item is non-link; chevron separator present; `aria-current="page"` on current; `nav` `aria-label` localized; truncation class + `title` present; collapse path sets `MaxItems`.
  - `MainLayout` slot — no `<nav>` when `HasTrail` is false (AC-7).

### Phase 8 — Consumer wiring (forward-looking)
- No consumer page exists today that should show a trail (`Home`/auth are excluded). Wire each page's `BreadcrumbState.Set(...)` **as it ships** (Products listing, product detail, categories, then admin pages), extending `BreadcrumbTrail` and adding any missing parent-route helpers at that time. This is the feature's standing definition-of-done, per the spec's sequencing note.

### Phase 9 — End-to-end & polish
- `/theshop.test breadcrumbs` → `/theshop.verify breadcrumbs` (smoke the system via a temporary harness page or the first real consumer page; check desktop full trail vs mobile collapse, keyboard focus, screen-reader landmark) → `/theshop.review breadcrumbs` (incl. French gate) → `/theshop.document`.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: Storefront trail begins at Home, ends at current page | Phase 4 (`Storefront()` root), Phase 5 (component), Phase 7 (`ShopBreadcrumbsTests` storefront trail); live on each storefront page via Phase 8 |
| AC-2: Admin trail begins at Dashboard, ends at current page | Phase 4 (`Admin()` root), Phase 5, Phase 7 (admin-trail test); live as admin pages ship (Phase 8) |
| AC-3: Clicking a non-final level navigates to that page | Phase 4 (hrefs from `Routes.*`), Phase 5 (`ItemTemplate` `MudLink`), Phase 7 (asserts `Href`) |
| AC-4: Final item is the current page and is not a link | Phase 4 (`.Current` ⇒ disabled), Phase 5 (non-link render), Phase 7 |
| AC-5: Trail identical whether navigated or deep-linked | Decision 4 + Phase 4 (structural builder), Phase 7 (identity test) |
| AC-6: A specific-item level shows the item's actual name | Phase 4 (`.Add`/`.Current` take supplied names), Phase 7 |
| AC-7: No trail on Home, admin Dashboard landing, or auth screens | Phase 6 (slot only when `HasTrail`; roots never `Set`; auth on `AuthLayout`), Phase 7 (no-`nav` test) |
| AC-8: Every label in both English and French, per active culture | Phase 1 (`Strings` + `fr.resx`), Phase 5 (typed `Strings.*`), Phase 9 French gate |
| AC-9: Long label shortened without breaking layout (desktop + small) | Phase 2 (`.shop-breadcrumb-item`), Phase 5 (`title` + responsive collapse), Phase 7 |
| AC-10: Keyboard-navigable, announced as breadcrumb nav, current = current location | Phase 5 (`MudLink` focus + nav `aria-label` + `aria-current="page"`), Phase 7 (a11y attribute asserts) |

## 9. Validation & Edge-case Handling Strategy

This feature has **no Application-layer validators, domain exceptions, or `Result.Fail` keys** — it owns no use case and performs no input validation. The equivalent rigor is the UI resilience strategy: every spec edge case maps to a concrete component/builder behavior.

| Spec edge case | Handling |
|---|---|
| Very long category/product/order name | `.shop-breadcrumb-item` truncates with ellipsis; `title="@context.Text"` exposes the full label (desktop hover). Layout never breaks. |
| Page directly below root (e.g. Products) | Builder emits two items (`Home`, current) — no intermediate levels. |
| On Home / admin Dashboard landing / auth | No `Set` call ⇒ `HasTrail == false` ⇒ slot renders nothing (AC-7). |
| Product belongs to no category | `BreadcrumbTrail.Storefront().Add(Nav_Products, Routes.Products).Current(product.Name)` fallback. |
| Deep-link / bookmark arrival (no parent nav) | Structural builder is independent of navigation history — full trail still produced (AC-5). |
| Parent no longer exists (removed category) | Builder passes `href: null` ⇒ disabled `BreadcrumbItem` ⇒ plain text; the rest of the trail still links. |
| Small / mobile screen, deep trail | `IBrowserViewportService` drives `MaxItems`; middle levels collapse behind the `…` expander; first + current stay visible. |

## 10. Database Schema & RLS Policies

**None.** Breadcrumbs derive entirely from the in-memory route hierarchy (`Routes`), localized labels (`Strings`), and data already loaded by each page. This feature adds no tables, columns, indexes, migrations, or RLS policies, and therefore introduces no new security boundary. (Admin breadcrumbs, when wired, render only inside already-RLS-gated admin pages — the breadcrumb itself exposes nothing that page doesn't.)

## 11. Open Questions, Risks & Assumptions

None — all questions resolved. The collapse expander uses `ShopIcons.Outlined.More_Horizontal` (§5 Decision 6); responsive collapse is `MaxItems = 2` at `Sm` and below via `MudBreakpointProvider` (§5 Decision 6, §7 Phase 5); the stale-trail risk is mitigated by clearing on `LocationChanged` (§7 Phase 6); `aria-current="page"` is set explicitly by the `ItemTemplate` (§5 Decision 8); parent routes/builder are extended as each page ships (§1, §7 Phase 8); and the full-label tooltip uses the native `title` attribute, guaranteed truncation with a best-effort touch reveal (§5 Decision 7, §9).

---
**Status:** Resolved · **Spec:** `.specs/breadcrumbs/spec.md` · **Created:** 2026-06-23 · **Resolved:** 2026-06-23

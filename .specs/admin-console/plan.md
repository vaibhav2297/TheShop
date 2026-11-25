# Implementation Plan — Admin Console

> Companion to `.specs/admin-console/spec.md`. This plan is technical (HOW); the spec is
> non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

Add a read-only admin dashboard at `/admin` that renders one overview card per governed module
(Products, Categories, Brands, Users, Roles) the signed-in staff member is permitted to
view, each showing a live record count and a link to that module's management page. Counts are
computed server-side by a single `SECURITY DEFINER` Postgres function that re-checks each module's
`authorize()` gate and counts **all** rows regardless of RLS-visibility/status (the storefront RLS on
`products`/`brands` hides rows, and `auth.users` is not client-selectable). No new domain behavior,
no writes — this is a query-only, permission-aware presentation slice over existing data.

## 2. Tech Stack

- **Domain:** none — no new entities/value objects (counts are over existing tables).
- **Application:** MediatR (`IRequest`/handler), `Result<T>`, `ICurrentUserService` for per-module
  permission gating. No FluentValidation (the query has no input fields). No AutoMapper (DTOs are
  built directly).
- **Infrastructure:** `supabase-csharp` (`client.Rpc(...)`) calling one Postgres RPC.
- **Web:** MudBlazor (`MudCard`/`MudPaper`/`MudGrid`/`MudText`), `BusyState`, bUnit for tests.
- **Persistence:** Supabase (PostgreSQL + RLS); one new `SECURITY DEFINER` function, **no new tables**.

## 3. High-level Architecture

```
AdminConsole.razor loads (/admin, gated by PolicyNames.AdminArea via Pages/Admin/_Imports.razor)
   ↓  BusyState.RunAsync(BusyKeys.AdminDashboard, …)
IMediator.Send(new GetAdminDashboardQuery())
   ↓
GetAdminDashboardHandler (Application)
   ├── for each module in AdminDashboardCatalogue.Modules
   │      where ICurrentUserService.HasPermission(module.ViewPermissionCode):   // FR-5 (UX gate)
   │        try  → IAdminDashboardRepository.CountModuleAsync(module) → int
   │        catch → null count (placeholder)                                     // FR-7 / AC-5
   └── Result.Ok(new AdminDashboardDto([AdminModuleCardDto(module, count?), …]))
   ↓
SupabaseAdminDashboardRepository (Infrastructure)
   → client.Rpc("admin_module_count", { p_module })   // SECURITY DEFINER, re-checks authorize()  ← real boundary
   ↓
Result<AdminDashboardDto> → AdminConsole.razor
   → foreach card: <AdminModuleCard Label Count Href/>  (count == null → "—")
```

## 4. Data Model

### Domain entities & value objects
None. No new or modified Domain types. The module set is a presentation grouping, not a domain
invariant, so it lives in Application (Decision 4).

### Application contracts (Application → Web)
- **`AdminModule`** (enum, `Features/Admin/`) — `Products, Categories, Brands, Users, Roles`.
  Stable identifier the Web maps to route + label + icon; Infra maps to the RPC's `p_module` string.
- **`AdminDashboardCatalogue`** (static, `Features/Admin/`) — the ordered list of
  `(AdminModule Module, string ViewPermissionCode)`, citing `PermissionCatalogue` codes:
  Products→`products.view`, Categories→`categories.view`, Brands→`brands.view`,
  Users→`admin_users.view`, Roles→`roles.view`.
- **`AdminModuleCardDto`** (`record`) — `AdminModule Module`, `int? Count` (`null` ⇒ placeholder).
- **`AdminDashboardDto`** (`record`) — `IReadOnlyList<AdminModuleCardDto> Modules` (only permitted
  modules present ⇒ empty list ⇒ empty-state).
- **`IAdminDashboardRepository`** (`Common/Interfaces/`) — `Task<int> CountModuleAsync(AdminModule
  module, CancellationToken ct)`.

### Database tables (new or modified)
| Table | Purpose | Key columns |
|---|---|---|
| _none_ | Counts read existing `products`, `categories`, `brands`, `roles`, and `auth.users`. | — |

### Indexes
None. `count(*)` over each table is a full aggregate; no new index earns its keep for an
admin-only, on-load-only read.

## 5. Core Design Decisions

1. **Decision:** Counts come from a single parameterized `SECURITY DEFINER` RPC
   `admin_module_count(p_module TEXT)`, called once per permitted module.
   - **Why:** The client cannot count "all records regardless of status" (spec FR-3/RULE-3): the
     `products` SELECT policy exposes only `is_published = true` (migration 0002, no admin-view
     policy), `brands` hides inactive rows from non-`brands.view` callers, and `auth.users` has no
     client SELECT at all. A `SECURITY DEFINER` function runs as owner, counts every row, and
     re-checks `public.authorize('<gate>')` itself — so hiding a card in the UI is never the only
     protection (constitution admin rule; RLS/`authorize` is the boundary).
   - **Rejected:** Client-side `client.From<TRecord>().Count()` per table — under-counts unpublished
     products/inactive brands and cannot touch `auth.users`. Rejected: one aggregate RPC returning
     all five counts in one row — loses AC-5 per-module failure isolation and returns counts for
     modules the handler would otherwise never request.

2. **Decision:** The handler gates each module by `ICurrentUserService.HasPermission` and wraps each
   `CountModuleAsync` call in its own `try/catch`, mapping failure to `Count = null`.
   - **Why:** Delivers FR-5 (only permitted cards, by omission) and FR-7/AC-5 (one module's count
     failure shows a placeholder on that card only while the others render normally) in one place.
   - **Rejected:** A single try/catch around all counts — a failure would blank every card,
     violating AC-5 ("the other cards display normally").

3. **Decision:** No `[RequiresPermission]` on `GetAdminDashboardQuery`; the page is gated by
   `PolicyNames.AdminArea` (via `Pages/Admin/_Imports.razor`) and each count by the RPC's
   `authorize()`.
   - **Why:** There is no single permission that means "the dashboard"; access is "≥ 1 admin
     permission" (AdminArea) plus per-module gating. A customer/guest reaching the query gets an
     empty module list (all `HasPermission` false) and never a count.
   - **Rejected:** Attaching one module's permission to the query — would wrongly deny admins who
     hold a different module's permission.

4. **Decision:** `AdminModule` enum + `AdminDashboardCatalogue` live in Application, not Domain.
   - **Why:** The five-module grouping is a UI/presentation concern (which cards to show), not a
     business invariant; Domain stays pure (Rule 2). The gate codes still reference the Domain
     `PermissionCatalogue` (single source of truth).
   - **Rejected:** A Domain enum — over-loads Domain with a dashboard-shaped concept.

5. **Decision:** The dashboard renders inside the existing shared chrome (`MainLayout` →
   `ShopAppBar`/`ShopFooter`), matching the Figma frame; no dedicated admin layout this feature.
   - **Why:** Ratified in `/theshop.clarify` (spec Constraints); Figma node `2496:6781` shows the
     announcement bar, app bar, and footer around the content.
   - **Rejected:** A separate admin shell — out of scope; would duplicate chrome for no spec benefit.

6. **Decision:** Reuse the existing admin routing/authorization scaffolding for the denied
   experience (AC-4) rather than adding page-level checks.
   - **Why:** `Pages/Admin/_Imports.razor` already stamps `[Authorize(Policy = AdminArea)]`; the
     app's `AuthorizeRouteView` redirects guests to sign-in and shows `AccessDeniedView` for
     authenticated-but-unauthorized users. Consistent with `ManageProducts`/`ManageBrands`.
   - **Rejected:** Re-implementing an `<AuthorizeView>` guard on the page — redundant with the
     folder-level policy.

7. **Decision:** The Categories/Users/Roles cards link to their `Routes.Admin.*`
   constants, which are **not** backed by a page this feature — navigating resolves to the app's
   NotFound page until each module ships. No placeholder shell pages are added (OQ-3, ratified).
   - **Why:** Keeps this feature strictly to the dashboard (spec out-of-scope: "this dashboard only
     counts and links to them"); the cards still count and link from day one.
   - **Rejected:** Adding gated placeholder shells now — scope creep beyond "dashboard only links,"
     and throwaway once each module's real page arrives.

## 6. Core Functional Flow

### Flow 1: Open the admin console (spec Behavior 1 & 3)
1. `AdminConsole.razor` `OnInitializedAsync` runs `await BusyState.RunAsync(BusyKeys.AdminDashboard,
   …)` and calls `Mediator.Send(new GetAdminDashboardQuery())`.
2. `GetAdminDashboardHandler` iterates `AdminDashboardCatalogue.Modules`; for each module where
   `currentUser.HasPermission(module.ViewPermissionCode)` it calls
   `repo.CountModuleAsync(module.Module, ct)` inside `try/catch`.
3. Success → `AdminModuleCardDto(module, count)`; exception → `AdminModuleCardDto(module, null)`.
   Modules the user can't view are skipped entirely.
4. Handler returns `Result.Ok(new AdminDashboardDto(cards))`.
5. Page renders one `<AdminModuleCard>` per DTO entry (count `null` → `Strings.AdminConsole_CountUnavailable`
   "—"). Empty `Modules` → `Strings.AdminConsole_NoModules` empty-state (spec Edge case 1).

### Flow 2: Jump to a module's page (spec Behavior 2)
1. Each `<AdminModuleCard>` root is a MudBlazor link (`Href` = the module's `Routes.Admin.*`
   constant, resolved from the Web presentation map) — keyboard-focusable with a visible focus ring.
2. Activating it navigates to that module's management page (`ManageProducts`/`ManageBrands` exist
   today; the Categories/Users/Roles routes are not registered by this feature, so they
   resolve to the app's NotFound page until each module ships — the card still counts and links,
   Decision 7).

### Flow 3: Unauthorized visitor (spec Behavior 4 / AC-4)
1. Guest → `AuthorizeRouteView` redirects to `Routes.Auth.SignIn` (existing).
2. Authenticated customer (no admin permission) → AdminArea policy fails → `AccessDeniedView`.
   The query is never reached; no counts leak.

## 7. Development Plan

### Step 1 — Domain (`shop-domain-implementer`)

**Skipped — no Domain impact.** No new entities, value objects, enums, or exceptions; the module
grouping is an Application concern (Decision 4) and counts read existing tables.

### Step 2 — Application (`shop-application-implementer`)

**Depends on:** resolved plan.

- [ ] **TASK-001** — `AdminModule` enum + `AdminDashboardCatalogue` (ordered `(AdminModule,
  ViewPermissionCode)` list referencing `PermissionCatalogue`) in `Features/Admin/`.
- [ ] **TASK-002** — DTOs `AdminModuleCardDto(AdminModule, int? Count)` and
  `AdminDashboardDto(IReadOnlyList<AdminModuleCardDto>)` in `Features/Admin/DTOs/`.
- [ ] **TASK-003** — `IAdminDashboardRepository.CountModuleAsync(AdminModule, CancellationToken)` in
  `Common/Interfaces/`.
- [ ] **TASK-004** — `GetAdminDashboardQuery` + `GetAdminDashboardHandler` in
  `Features/Admin/Queries/GetAdminDashboard/`: per-module `HasPermission` gate, per-module
  `try/catch` → `null` count, returns `Result<AdminDashboardDto>` (Section 9).
- [ ] **TASK-005** — Application unit tests (`GetAdminDashboardHandlerTests`): permitted-subset only,
  count exception → `null` on that card while others succeed, no permissions → empty `Modules`.

**Completion gate:** Application builds · handler covers Section 9 outcomes · `IAdminDashboardRepository`,
query, DTOs, and enum are written and compiling for the freeze.

### Step 3 — Contract freeze

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `AdminModule` enum + `AdminDashboardCatalogue` | Application | Infrastructure, Web | Stable |
| `GetAdminDashboardQuery` / `Result<AdminDashboardDto>` | Application | Web | Stable |
| `AdminModuleCardDto` / `AdminDashboardDto` shape | Application | Web | Stable |
| `IAdminDashboardRepository.CountModuleAsync` | Application | Infrastructure | Stable |

### Step 4 — Infrastructure (`shop-infra-implementer`) — runs in parallel with Step 5

**Depends on:** contract freeze (`IAdminDashboardRepository`, `AdminModule` stable).

- [ ] **TASK-006** — Migration `0013_admin_dashboard_counts.sql`: `public.admin_module_count(p_module
  TEXT) RETURNS BIGINT`, `SECURITY DEFINER STABLE`, `SET search_path = public`, re-checking
  `public.authorize('<gate>')` per module and counting the corresponding source (Section 10);
  `GRANT EXECUTE … TO authenticated`. Applied via Supabase MCP.
- [ ] **TASK-007** — `SupabaseAdminDashboardRepository : IAdminDashboardRepository` in
  `Persistence/Repositories/` — maps `AdminModule` → `p_module` string, calls
  `client.Rpc("admin_module_count", …)`, parses the `bigint` to `int`; register in
  `Infrastructure/DependencyInjection.cs`.
- [ ] **TASK-008** — Infrastructure tests: `admin_module_count` denies without the gate
  (`insufficient_privilege`), counts **all** rows including unpublished products / inactive brands,
  and returns the `auth.users` total for `users`.

**Completion gate:** migration applies cleanly · function re-checks `authorize()` and counts all
statuses · repository satisfies the frozen interface · no Infrastructure type leaks inward.

### Step 5 — Web (`shop-ui-implementer`) — runs in parallel with Step 4

**Depends on:** contract freeze (query/DTO/enum shapes stable).

**Figma references** *(re-fetched by `shop-ui-implementer` at implementation time)*

- **File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2496-6781
- **Nodes:**
  - `2496:6781` — "Admin Console" page frame; the whole `/admin` screen inside announcement/appbar/footer chrome.
  - `2496:6787` — Title block: heading "ADMIN CONSOLE" (`2496:6788`) + subtitle (`2496:6789`).
  - `2501:2127` — the module-card grid container (the design draws 6 card slots; this feature
    renders **five** — Products, Categories, Brands, Users, Roles — and omits the Permissions tile).
  - `2501:2128`, `2505:1184`, `2505:1194`, `2535:5181`, `2535:5207` — module card frames
    (icon region `Frame 18` + label/count region `Frame 19`); one card = one `AdminModuleCard`. The
    sixth design card (`2535:5220`) is unused.

- [ ] **TASK-009** — `Routes.Admin` constants `ManageCategories = "/admin/categories"`,
  `ManageUsers = "/admin/users"`, `ManageRoles = "/admin/roles"` (link targets only — no page is
  registered at these yet, so they resolve to NotFound until each module ships, Decision 7);
  `BusyKeys.AdminDashboard`; `AdminConsole.razor` + `.razor.cs` at
  `[Route(Routes.Admin.Console)]` under `Pages/Admin/`.
- [ ] **TASK-010** — `Components/Admin/AdminModuleCard.razor` — reusable (rendered 5×): inherits
  `MudComponentBase`, forwards `Class`/`Style` to the root (Rule 23/24), renders label + count (or
  `Strings.AdminConsole_CountUnavailable` when null) + icon, whole card links via `Href`,
  keyboard-focusable with an aria-label pairing module name + count (spec a11y constraint).
- [ ] **TASK-011** — Web presentation map `AdminModule → (Routes.Admin.*, Strings label, ShopIcons
  icon)`; wire `AdminConsole.razor.cs` to `Mediator.Send(new GetAdminDashboardQuery())` inside
  `BusyState.RunAsync(BusyKeys.AdminDashboard, …)`; render cards, `BusyFor` spinner, and the
  empty-state (`Strings.AdminConsole_NoModules`).
- [ ] **TASK-012** — Resource strings in `Strings.resx` (+ `Strings.fr.resx`): page title, heading,
  subtitle, five module labels, count aria template, empty-state, count-unavailable placeholder
  (Section 9).
- [ ] **TASK-013** — Update `ShopAppBar.razor` admin menu item `Href` from
  `Routes.Admin.ManageProducts` → `Routes.Admin.Console` (FR-6).
- [ ] **TASK-014** — bUnit tests: permitted cards render with counts, unpermitted cards absent,
  `null` count → placeholder while siblings show numbers, empty-state when no modules, card `Href`
  targets, and the app-bar menu link points at `/admin`.

**Completion gate:** matches Figma nodes · MudBlazor-only, no hardcoded strings/design tokens
(rules 2–5, 11, 13–16) · reusable card forwards `Class`/`Style` · consumes only frozen contracts ·
guests/customers see the sign-in/`AccessDeniedView` experience.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5 complete.

- [ ] **TASK-015** — Cross-layer verification: solution builds, DI resolves
  `IAdminDashboardRepository`, migration applied, permitted/absent/placeholder/empty flows work
  end-to-end against a seeded admin.
- [ ] **TASK-016** — Run `/theshop.test admin-console` → `/theshop.verify admin-console` →
  `/theshop.review admin-console` → `/theshop.document`.

### Deviation procedure

- **Accept** a deviation only if it preserves approved behavior and layer boundaries, aligns better
  with existing conventions, and doesn't weaken authorization/integrity or expand scope.
- **Reject** it if it changes a requirement, adds business behavior, violates dependency direction,
  silently alters a frozen contract, or smuggles in unrelated refactoring.
- **Contract change:** stop dependent work → record it here (update the freeze table + affected TASK
  ids) → resume only after re-freeze.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: all five permitted cards render with name + current count | TASK-004, TASK-006, TASK-007, TASK-010, TASK-011 |
| AC-2: selecting a card navigates to its management page | TASK-009 (routes), TASK-010 (card link), TASK-011 (map) |
| AC-3: a module the admin can't view is absent (not disabled) | TASK-004 (per-module gate), TASK-006 (`authorize` in RPC), TASK-014 |
| AC-4: customer denied / guest redirected to sign-in | TASK-009 (page under `Pages/Admin` AdminArea gate), Decision 6, TASK-014 |
| AC-5: count failure → placeholder on that card, others normal | TASK-004 (per-module `try/catch`), TASK-010/011 (placeholder), TASK-014 |
| AC-6: account-menu entry lands on `/admin` | TASK-013 |
| AC-7: all dashboard text available in French | TASK-012 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)
None — `GetAdminDashboardQuery` carries no input fields, so there is nothing to validate
(no `FluentValidation` validator is added).

### Domain exceptions
None — no domain behavior is invoked.

### Handling map (spec edge cases & RULEs)
| Spec item | Strategy |
|---|---|
| RULE-1 / AC-4 (access) | `Pages/Admin/_Imports.razor` `[Authorize(AdminArea)]` + `AuthorizeRouteView` (guest→sign-in, customer→`AccessDeniedView`). No count query runs. |
| RULE-2 / FR-5 / AC-3 (card only if permitted) | Handler skips modules failing `HasPermission`; RPC re-checks `authorize()` (defense-in-depth). |
| RULE-3 / FR-3 (count all statuses) | `admin_module_count` counts as owner, bypassing storefront RLS; reads `auth.users` for Users. |
| FR-7 / AC-5 (count fails → placeholder) | Handler `try/catch` per module → `Count = null`; page renders `Strings.AdminConsole_CountUnavailable`. |
| Edge: zero records | RPC returns `0`; card shows `0` (not skipped). |
| Edge: no permitted modules | Handler returns empty `Modules`; page shows `Strings.AdminConsole_NoModules`. |
| Edge: revoked permission next load | Module drops out of the handler's permitted set; card gone. |

### Result.Fail error keys (new entries in `Strings.resx`)
None. The query returns `Result.Ok` even when some counts are `null` (placeholder is a success
state, not a failure). New **UI** resource keys (not error keys):

| Key | English text |
|---|---|
| `AdminConsole_PageTitle` | "Admin Console" |
| `AdminConsole_Heading` | "Admin Console" |
| `AdminConsole_Subtitle` | "Manage your store from one place." |
| `AdminConsole_Module_Products` | "Products" |
| `AdminConsole_Module_Categories` | "Categories" |
| `AdminConsole_Module_Brands` | "Brands" |
| `AdminConsole_Module_Users` | "Users" |
| `AdminConsole_Module_Roles` | "Roles" |
| `AdminConsole_CountUnavailable` | "—" |
| `AdminConsole_CountAria` | "{0}: {1} records" |
| `AdminConsole_NoModules` | "There's nothing for you to manage here yet." |

All keys mirrored in `Strings.fr.resx` (the review step's French-completeness gate catches stragglers).

## 10. Database Schema & RLS Policies

### Schema
No new tables or indexes. One new function.

```sql
-- 0013_admin_dashboard_counts.sql
-- Per-module record count for the admin console. SECURITY DEFINER so it counts EVERY row
-- regardless of storefront RLS/status (spec FR-3/RULE-3) and can read auth.users; it re-checks
-- the module's authorize() gate itself so hiding a card in the UI is never the only protection.
CREATE OR REPLACE FUNCTION public.admin_module_count(p_module TEXT)
RETURNS BIGINT
LANGUAGE plpgsql SECURITY DEFINER STABLE
SET search_path = public AS $$
DECLARE
    v_count BIGINT;
BEGIN
    CASE p_module
        WHEN 'products' THEN
            IF NOT public.authorize('products.view')   THEN RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege'; END IF;
            SELECT count(*) INTO v_count FROM public.products;
        WHEN 'categories' THEN
            IF NOT public.authorize('categories.view') THEN RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege'; END IF;
            SELECT count(*) INTO v_count FROM public.categories;
        WHEN 'brands' THEN
            IF NOT public.authorize('brands.view')     THEN RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege'; END IF;
            SELECT count(*) INTO v_count FROM public.brands;
        WHEN 'users' THEN                                            -- all accounts (customers + staff)
            IF NOT public.authorize('admin_users.view') THEN RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege'; END IF;
            SELECT count(*) INTO v_count FROM auth.users;
        WHEN 'roles' THEN
            IF NOT public.authorize('roles.view')      THEN RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege'; END IF;
            SELECT count(*) INTO v_count FROM public.roles;
        ELSE
            RAISE EXCEPTION 'unknown module %', p_module USING ERRCODE = 'invalid_parameter_value';
    END CASE;

    RETURN v_count;
END $$;

GRANT EXECUTE ON FUNCTION public.admin_module_count(TEXT) TO authenticated;
```

### RLS policies
No new table policies — the function is the boundary (it calls `public.authorize(...)`, the same
predicate used by every other admin policy). `authenticated` is the only grantee (not `anon`);
`authorize()` already returns `false` for an expired/absent admin session (migration 0007).

## 11. Open Questions, Risks & Assumptions

None — all questions resolved. Ratified decisions: the console shows **five** modules — the
Permissions tile was dropped as a dead-end (permissions are a fixed code-defined catalogue with no
manage action; Section 1 Out-of-scope); the Users card is gated by `admin_users.view` (Sections 4 &
10); the not-yet-built module routes resolve to NotFound rather than getting placeholder shells
(Decision 7); and no validator / no error key is confirmed (Section 9). Two risks are knowingly
accepted:

- **⚠️ Risk — ✅ Accepted:** Up to five `admin_module_count` RPC calls per load (one per permitted
  module). Acceptable for an admin-only, on-load screen; collapse to one aggregate RPC later if it
  ever bites (not done now, to preserve AC-5 per-module failure isolation). Owned by TASK-007.
- **⚠️ Risk — ✅ Accepted:** `HasPermission` reads token claims (bounded by token TTL) while the RPC
  reads `authorize()` live, so a just-revoked module can briefly render a card whose RPC then denies
  → the card shows the placeholder rather than vanishing until the next load. Self-correcting; matches
  the RBAC feature's documented staleness contract. Owned by TASK-004/006.

---
**Status:** Resolved · **Spec:** `.specs/admin-console/spec.md` · **Created:** 2026-07-23 · **Resolved:** 2026-07-24

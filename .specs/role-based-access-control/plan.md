# Implementation Plan — Role-Based Access Control

> Companion to `.specs/role-based-access-control/spec.md`. This plan is technical (HOW); the spec is non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

Build the complete RBAC domain model (roles, permissions, user role assignments) and the authorization infrastructure that consumes it: a MediatR authorization pipeline behavior in Application, database-backed permission checks in Supabase RLS, and Blazor policy-based gating in Web. RBAC configuration is read-only this release — provisioned by migration seed data — and every newly registered user is auto-assigned the Customer role by the database at account creation. The architecture must support future runtime RBAC administration without domain-model or authorization-mechanism changes.

**Critical correction to existing code:** migrations `0001` and `0004` currently authorize admin writes via `auth.jwt() ->> 'role' = 'admin'` — a role-name check frozen into the JWT at login. That violates FR-5 (permission-based, not role-based) and FR-11 (revocation effective on next action; JWT claims stay stale until re-login). This plan replaces those checks with a database-lookup `authorize()` function.

## 2. Tech Stack

- **Domain:** C# 13 / .NET 10, no external deps.
- **Application:** MediatR 12 (new `AuthorizationBehavior<,>`), `Result<T>` (project-internal). No FluentValidation needed — the one new query has no parameters.
- **Infrastructure:** `supabase-csharp` 1.x (RPC call for permission fetch); SQL migration under `supabase/migrations/`.
- **Web:** Blazor `AuthorizationCore` (policy-based authorization, custom `AuthorizationHandler`), MudBlazor, bUnit.
- **Persistence:** Supabase PostgreSQL — 4 new tables, 1 `SECURITY DEFINER` authorization function, 1 RPC, triggers, RLS.

## 3. High-level Architecture

A permission is checked at three independent layers (per `architecture-admin.md` §Security — layers 1–2 are UX, layer 3 is the security boundary). All three read the **same database tables**, so a configuration change is live on the very next request (FR-11) — no JWT claim carries role or permission data.

```
User triggers an action (page, menu, direct link, or raw API call)
   ↓
[Web]   Blazor policy "perm:{code}" → PermissionAuthorizationHandler → PermissionState (hydrated set)
        └── governs what renders: nav items, buttons, route access (FR-6, AC-4)
   ↓
[App]   IMediator.Send(command with [RequiresPermission("orders.refund")])
        └── AuthorizationBehavior → IPermissionService.GetCurrentUserPermissionsAsync()
            missing → Result.Fail(RbacErrorKeys.AccessDenied)  (FR-7, FR-12)
   ↓
[DB]    Supabase RLS policy: USING (public.authorize('orders.refund'))
        └── authorize() joins user_roles ⋈ role_permissions ⋈ permissions for auth.uid()
            — the only real security boundary; stops requests that bypass the UI entirely
```

`PermissionState` (Web) is hydrated by dispatching `GetMyPermissionsQuery` whenever auth state changes, and cleared on sign-out. A server-side denial (revocation mid-session) re-hydrates it, so revoked modules disappear from navigation (Behavior 4, edge case 1).

## 4. Data Model

### Domain entities & value objects

- **`Permission`** (value object, `ValueObjects/`) — wraps a permission code in `module.action` form (e.g. `products.view`, `orders.refund`). Validates format on creation; equality by code.
- **`PermissionCatalogue`** (static class, `ValueObjects/` or `Enums/`-adjacent) — the single source of truth for every permission code, organized as nested static classes per module: `Products`, `Categories`, `Orders`, `Customers`, `Coupons`, `Promotions`, `Reports`, `Settings`, `AdminUsers`, `Roles`. Each module exposes `View`/`Create`/`Edit`/`Delete` plus module-specific sensitive actions (`Orders.Refund`, `Customers.Export`). Exposes `All` for seed generation and an `IsAdminArea(code)` helper (every module here is admin-area). The DB seed is generated from this class — code and database can never drift silently (FR-3).
- **`Role`** (entity, `Entities/`) — `Id`, `NameKey` (a resource key, e.g. `Role_Support` — localization lives in Web, FR-2/AC-11), `IsSystem`, `IReadOnlyCollection<Permission> Permissions`. Behavior: `Grant(Permission)` / `Revoke(Permission)` / `Rename(string)` throwing `SystemRoleImmutableException` when `IsSystem` — the future-administration surface exists now so no domain change is needed later, even though nothing calls the mutators this release. `Rehydrate(...)` for repository mappers (same pattern as `Customer`).
- **`UserAccess`** (entity/aggregate, `Entities/`) — a user's role assignments: `UserId`, `IReadOnlyCollection<Role> Roles`. Behavior: `EffectivePermissions()` (union across roles, FR-4), `HasPermission(Permission)`, `HoldsAdminAreaPermission()`.
- **Exceptions** (`Exceptions/`) — `SystemRoleImmutableException`, `InvalidPermissionException` (bad code format); both carry `MessageKey` per the existing `DomainException` pattern.

### DTOs (Application → Web)

- **`UserPermissionsDto`** — `IReadOnlySet<string> Permissions`. The only DTO the UI needs; role objects never cross to Web this release.

### Database tables (new or modified)

| Table | Purpose | Key columns |
|---|---|---|
| `roles` (new) | Role definitions | `id uuid PK`, `name_key text UNIQUE`, `is_system boolean` |
| `permissions` (new) | Permission catalogue | `id uuid PK`, `code text UNIQUE`, `module text` |
| `role_permissions` (new) | Role → permission grants | `role_id FK`, `permission_id FK`, composite PK |
| `user_roles` (new) | User → role assignments | `user_id FK auth.users`, `role_id FK`, `assigned_at`, composite PK |
| `customers` (modified) | Replace JWT-role admin clause in RLS | policy change only |
| `storage.objects` product-images policies (modified) | Replace JWT-role admin clauses | policy change only |

### Indexes

- `user_roles (user_id)` — the hot path of every `authorize()` call.
- `role_permissions (role_id)` — second leg of the join.
- `permissions (code)` — unique index doubles as lookup.

## 5. Core Design Decisions

1. **Decision:** Permissions are resolved by database lookup on every check — never embedded in the JWT.
   - **Why:** FR-11 requires a revocation to block the *very next* attempt without sign-out; JWT claims are minted at login and stale until refresh (`architecture-admin.md` itself warns "the user must log out and back in"). FR-5 requires permission-based decisions, and the permission set is too large for a token anyway.
   - **Rejected:** Supabase custom-access-token hook stuffing permissions into JWT claims — revocation would lag one token lifetime, violating FR-11 and AC-7.

2. **Decision:** One `public.authorize(requested_permission text)` `SECURITY DEFINER STABLE` SQL function is the single RLS predicate; all admin-gated policies call it.
   - **Why:** Rule set in `architecture-admin.md` — RLS is the only real security boundary (FR-7, AC-5). Centralizing the join in one function keeps every future policy a one-liner and makes the permission mechanism upgradeable (custom roles, scoping) without touching policies — the spec's "no redesign" constraint.
   - **Rejected:** Inlining the three-table join into each policy — duplication, and future changes would touch every table's policy.

3. **Decision:** Auto-Customer assignment is a Postgres trigger on `auth.users` `AFTER INSERT` — not code in `VerifySignUpOtpHandler`.
   - **Why:** Spec: assignment is "performed by the backend and not configurable through the application", and must hold for *every* account-creation route. A trigger fires atomically with user creation regardless of which client or flow created the user; the handler path could be bypassed and isn't atomic. `VerifySignUpOtpHandler` needs **no change**.
   - **Rejected:** Application-layer assignment in the sign-up handler — non-atomic, route-specific, and it would grant the Web anon key insert rights on `user_roles`, undermining the read-only posture.

4. **Decision:** Super-Admin floor (FR-10/AC-9), self-role-change refusal (FR-10/AC-8), and system-role immutability (FR-2) are enforced by database triggers on `roles` / `user_roles` / `role_permissions`.
   - **Why:** The spec says no route — *including a configuration change* — may violate these. Seeds and future admin APIs all hit the database; triggers are the one choke point that covers them all. Domain entity invariants mirror the same rules for in-process fidelity, but the trigger is the enforcement.
   - **Rejected:** Application-only enforcement — seed SQL and direct API calls would bypass it.

5. **Decision:** Application-layer enforcement is a MediatR `AuthorizationBehavior<,>` reading a `[RequiresPermission("code")]` attribute on commands/queries.
   - **Why:** Rule 4 — every use case flows through MediatR, so a pipeline behavior guarantees "every action is checked" (FR-7) with zero per-handler code; denial returns `Result.Fail(RbacErrorKeys.AccessDenied)` per Rule 5/12. This is also exactly how the constitution's admin reference says to route admin permission checks ("through MediatR pipeline behaviors").
   - **Rejected:** Per-handler `if (!HasPermission)` checks — forgettable, violating the spec's "no feature may ship ungated" constraint.

6. **Decision:** The 8-hour admin session limit is enforced inside `authorize()`: for admin-area permissions it also checks the caller's `auth.sessions.created_at` (via the JWT `session_id` claim) is within 8 hours.
   - **Why:** Server-side enforcement of the session constraint with no client trust; the Web layer just reacts to the resulting denial by routing to sign-in with the existing "please sign in again" experience. Customer sessions are untouched. Because `auth.sessions` is a Supabase internal (not a documented contract), Phase 3 starts with a probe that verifies the dependency before building on it; the designed fallback is a `public.session_starts` table written at sign-in — a one-function swap, and a failure would fail *closed* (admins locked out), never open.
   - **Rejected:** Client-side timer only — trivially bypassed, and the spec's constraint is a security rule, not UX. Building `session_starts` preemptively — adds a table + write path for a problem not yet observed.

7. **Decision:** Web gating uses Blazor policy-based authorization — a `PermissionRequirement` + `PermissionAuthorizationHandler` reading a `PermissionState` store hydrated via `GetMyPermissionsQuery` (single `get_my_permissions()` RPC).
   - **Why:** Lets pages and nav use standard `[Authorize(Policy = ...)]` / `<AuthorizeView Policy=...>` (constitution admin patterns), while the permission data still comes from the database, not the JWT. One RPC per auth-state change, not per check.
   - **Rejected:** Augmenting `ClaimsPrincipal` with permission claims inside `SupabaseAuthStateProvider` — the provider would need async mediator calls during state resolution, tangling auth plumbing with Application dispatch.

8. **Decision:** Ship a minimal `/admin/manage-products` shell as the verification harness (per user direction), and render the access-denied experience **in place** — `AuthorizeRouteView`'s `NotAuthorized` content shows a shared `AccessDeniedView` component inside the normal page chrome at the attempted URL, exactly as the Figma design (node `2470:2200`) shows it. No dedicated `/access-denied` route.
   - **Why:** FR-6/AC-3/AC-4/AC-6 need an admin surface to gate, and none exists yet. `Pages/Admin/_Imports.razor` applies `[Authorize(Policy = PolicyNames.AdminArea)]` to everything under `/admin` forever; the shell page additionally requires `products.view`. In-place rendering preserves the attempted URL (matches the stale-screen/direct-link edge cases) and reuses the store chrome per the design.
   - **Rejected:** A standalone access-denied page + redirect — loses the attempted URL and contradicts the pinned design; deferring all admin UI — leaves four ACs unverifiable end-to-end.

## 6. Core Functional Flow

### Flow 1: Sign up → Customer role (Behavior 1, FR-1)

1. Existing sign-up flow runs unchanged; `IAuthService.VerifyOtpAsync` creates the `auth.users` row.
2. `on_auth_user_created` trigger inserts `(new.id, customer_role_id)` into `user_roles` atomically.
3. `get_my_permissions()` for that user returns no admin-area codes → `PermissionState` empty of admin permissions → no admin nav, `/admin` routes denied, RLS refuses admin-gated statements.

### Flow 2: Admin works inside a permission boundary (Behavior 2)

1. On sign-in / rehydration, `MainLayout` reacts to `AuthState.OnChange` and dispatches `GetMyPermissionsQuery`; handler calls `IPermissionService.GetCurrentUserPermissionsAsync()` → `get_my_permissions()` RPC.
2. `PermissionState.UpdateFrom(dto)` fires; the admin link renders under `<AuthorizeView Policy="@PolicyNames.AdminArea">`; admin navigation entries (this release: Manage Products) render only for modules where the user holds the `view` permission.
3. Absent permissions ⇒ absent UI — not disabled (AC-4).

### Flow 3: Unauthorized attempt (Behavior 3, FR-7/FR-12)

1. **Route level:** navigating to a policy-gated page fails the `PermissionAuthorizationHandler`; `App.razor`'s `AuthorizeRouteView` `NotAuthorized` content redirects unauthenticated users to `Routes.Auth.SignInWithReturn(...)` and renders `AccessDeniedView` in place (URL preserved, normal chrome) for authenticated ones — per Figma node `2470:2200`.
2. **Action level:** a stale screen dispatches a command; `AuthorizationBehavior` re-reads permissions → `Result.Fail(RbacErrorKeys.AccessDenied)`; page shows `Localizer[result.Error]` and re-hydrates `PermissionState` (revoked module vanishes from nav — edge case 1).
3. **API level:** a raw Supabase call bypassing the UI hits RLS; `authorize()` returns false; statement affects zero rows. Nothing is changed at any level (AC-5, AC-10).

### Flow 4: Roles changed in configuration (Behavior 4, FR-8/FR-11)

1. A migration/seed updates `user_roles` or `role_permissions`.
2. No JWT invalidation is needed: the very next RLS-gated statement and the next `AuthorizationBehavior` check read the new rows (AC-7).
3. The UI catches up on the next `PermissionState` hydration (navigation, auth change, or an access-denied response).

## 7. Development Plan

### Phase 1 — Domain foundations

- `ValueObjects/Permission.cs`, `PermissionCatalogue.cs` (all 10 modules × actions + sensitive actions).
- `Entities/Role.cs`, `Entities/UserAccess.cs`.
- `Exceptions/SystemRoleImmutableException.cs`, `Exceptions/InvalidPermissionException.cs`.
- Tests: `RoleTests` (system-role immutability, grant/revoke), `UserAccessTests` (union, admin-area detection), `PermissionTests` (format validation), `PermissionCatalogueTests` (all spec modules present, codes unique) in `tests/TheShop.Domain.Tests/`.

### Phase 2 — Application use cases

- `Common/Interfaces/IPermissionService.cs` — `Task<IReadOnlySet<string>> GetCurrentUserPermissionsAsync(CancellationToken ct)`.
- `Common/Behaviors/AuthorizationBehavior.cs` + `Common/Behaviors/RequiresPermissionAttribute.cs`; register the behavior in `Application/DependencyInjection.cs` **after** `ValidationBehavior`.
- `Features/Roles/Queries/GetMyPermissions/GetMyPermissionsQuery.cs` + handler; `Features/Roles/DTOs/UserPermissionsDto.cs`.
- `Features/Roles/RbacErrorKeys.cs` (pattern: existing `AuthErrorKeys`) — `AccessDenied = "Rbac_AccessDenied"`.
- Tests: `AuthorizationBehaviorTests` (permitted passes through, missing permission fails with key, no-attribute requests skip the check, unauthenticated fails), `GetMyPermissionsHandlerTests` in `tests/TheShop.Application.Tests/`.

### Phase 3 — Infrastructure

- **Session-dependency probe (first task):** before building `authorize()`, verify that `auth.sessions.created_at` is queryable from a `SECURITY DEFINER` function and that the JWT carries `session_id` — via a Testcontainers assertion and a check against the live project (Supabase MCP). If either fails, switch to the designed fallback: a `public.session_starts (user_id, session_id, started_at)` table populated at sign-in, read by `authorize()` instead — a one-function swap.
- Migration `0007_create_rbac.sql`:
  - Tables `roles`, `permissions`, `role_permissions`, `user_roles` + indexes.
  - Seed: 4 system roles, full permission catalogue (mirroring `PermissionCatalogue`), least-privilege grants per spec Constraints — Support: exactly `orders.view`, `orders.edit`, `customers.view` (no delete, no refund — refunds are sensitive and stay Admin+; ratified by user); Admin: all modules except Settings/Admin Users/Roles; Super Admin: everything. First Super Admin assignment (store-setup step documented in the migration header); backfill: every existing `auth.users` row gets Customer.
  - `public.authorize(text)` SECURITY DEFINER STABLE (permission join + 8-hour `auth.sessions.created_at` check for admin-area codes); `public.get_my_permissions()` RPC.
  - Triggers: `on_auth_user_created` (auto-Customer), super-admin-floor guard on `user_roles`/`role_permissions`, system-role protection on `roles`, self-change guard (`auth.uid() = user_id` writes refused).
  - RLS: enable on all four tables; `roles`/`permissions`/`role_permissions` SELECT for authenticated; `user_roles` SELECT own rows; **no client write policies anywhere** (read-only config, FR-8).
  - Replace `auth.jwt() ->> 'role' = 'admin'` clauses in `customers` and product-images policies with `authorize('customers.view')` / `authorize('products.edit')` equivalents.
- `Persistence/Records/RoleRecord.cs`, `PermissionRecord.cs`, `UserRoleRecord.cs`; `Persistence/Repositories/SupabasePermissionService.cs : IPermissionService` (calls the RPC); register in `Infrastructure/DependencyInjection.cs`.
- Tests: Testcontainers suite in `tests/TheShop.Infrastructure.Tests/` — trigger behaviors (auto-Customer, super-admin floor, self-change refusal, system-role protection), `authorize()` truth table, seed completeness vs. `PermissionCatalogue`, **and a rewritten-policy truth table** for `customers` and product-images (customer reads own row, admin reads all via permission, non-admin write refused, image read/write per `products.*`) — the regression guard for migrating those policies off JWT role checks.

### Phase 4 — Web

**Figma references** *(read by `shop-ui-implementer` at impl time)*

- **File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop
- **Nodes:**
  - `2470:2200` — "Add Product" admin page shown in its **access-denied state**: full store chrome retained (announcement bar `2470:2201`, app bar `2470:2203`, breadcrumb `2470:2462`, footer `2470:2270`, copyright `2470:2271`) with the content region replaced by the denied message.
  - `2470:2220` — the access-denied content block itself: "ACCESS DENIED!" title (`2470:2221`) + "Your account doesn't have permission to access this page." body (`2470:2222`). This is the template for the shared `AccessDeniedView` component.
- **Visual intent notes:** access denial is an in-place content state at the attempted URL, not a separate error page — the surrounding navigation stays usable. The breadcrumb still shows the attempted location (Products trail).

**Tasks**

- `State/PermissionState.cs` (hydrate/clear/`OnChange`, `HasPermission`, `HasAnyAdminPermission`); hydration wiring on auth-state change; register in `AddPresentation()`.
- `Auth/PermissionRequirement.cs` + `Auth/PermissionAuthorizationHandler.cs`; `Common/PolicyNames.cs` (`AdminArea` + `Permission(code)` helper); policy registration in `Program.cs` `AddAuthorizationCore` generated from `PermissionCatalogue.All`.
- `Components/Common/AccessDeniedView.razor(.cs)` — shared component matching node `2470:2220`; MudBlazor only, localized title/message + a back link (store home; admin home when the user still holds admin access — spec Behavior 3).
- `App.razor`: `AuthorizeRouteView` `NotAuthorized` → sign-in redirect (unauthenticated) or render `AccessDeniedView` in place (authenticated).
- `Pages/Admin/_Imports.razor` (`[Authorize(Policy = PolicyNames.AdminArea)]`), `Pages/Admin/ManageProducts.razor(.cs)` at new `Routes.Admin.ManageProducts` — the harness shell, additionally gated on `products.view` (matches node `2470:2200`; content shell only, full product management ships with the admin-catalogue feature).
- `MainLayout`: admin-panel link inside `<AuthorizeView Policy="@PolicyNames.AdminArea">`; Manage Products entry gated on `products.view`.
- `Strings.resx` + `Strings.fr.resx`: `Rbac_AccessDenied`, `AccessDenied_*`, `Nav_AdminPanel`, `ManageProducts_*`, `Role_{Name}` ×4, `Permission_{Module}_{Action}` display names (full FR mirror — AC-11; French is gate-checked at review).
- Tests (bUnit): `AccessDeniedViewTests`, `ManageProductsTests` (gated reachability), `PermissionAuthorizationHandlerTests`, `PermissionStateTests` in `tests/TheShop.Web.Tests/`.

### Phase 5 — End-to-end & polish

- `/theshop.test role-based-access-control` → full suite via manifest.
- `/theshop.verify role-based-access-control` → smoke ACs against the running app (seeded Support / Admin / Super Admin accounts).
- `/theshop.review role-based-access-control` (includes French-localization gate) → `/theshop.document`.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: new user holds exactly Customer; no admin reach | Phase 3 (auto-Customer trigger, backfill), Phase 4 (`_Imports` policy gate, route redirect), Phase 3 tests |
| AC-2: four built-in roles, least-privilege, immutable | Phase 3 (seed + system-role trigger), Phase 1 (`Role.IsSystem` invariants + tests) |
| AC-3: all 10 modules gated by own permissions; actions separately controllable | Phase 1 (`PermissionCatalogue` + tests), Phase 3 (seed completeness test, `authorize()`), Phase 2 (`[RequiresPermission]`) |
| AC-4: UI shows only permitted modules/actions (absent, not disabled) | Phase 4 (`AuthorizeView` nav gating, `ManageProducts` shell reachability, bUnit tests) |
| AC-5: platform refuses out-of-screen requests | Phase 2 (`AuthorizationBehavior` + tests), Phase 3 (RLS via `authorize()` + truth-table tests) |
| AC-6: seeded staff account has exactly its role's access | Phase 3 (seed grants per Constraints), Phase 5 (verify with seeded accounts) |
| AC-7: config change effective next action; mid-session revocation blocks next attempt | Design decision 1 (DB lookup, no JWT), Phase 3 (`authorize()`), Phase 2 (behavior re-reads per request), Phase 5 verify |
| AC-8: self-role-change refused | Phase 3 (self-change trigger + test) |
| AC-9: zero-Super-Admin change rejected | Phase 3 (super-admin-floor trigger + test) |
| AC-10: direct link / stale screen → access-denied, nothing changes | Phase 4 (`AccessDeniedView` in-place rendering), Phase 2 (behavior denial), Phase 3 (RLS) |
| AC-11: EN + FR for role names, permission names, access-denied messages | Phase 4 (resx pairs; names stored as keys per `Role.NameKey` design), Phase 5 review gate |

## 9. Validation & Error Handling Strategy

### Authorization pipeline (Application layer)
- `AuthorizationBehavior` — request carries `[RequiresPermission]` and the user lacks it (or is unauthenticated) → `Result.Fail(RbacErrorKeys.AccessDenied)`. No exception; expected business failure per Rule 5.

### Domain exceptions
- `SystemRoleImmutableException` — `Role.Grant/Revoke/Rename` on `IsSystem` role. `MessageKey = "Rbac_SystemRoleImmutable"`.
- `InvalidPermissionException` — malformed permission code. `MessageKey = "Rbac_InvalidPermission"`.

### Database-level refusals (config/seed routes — no runtime writers this release)
- Super-admin floor, self-change, system-role triggers raise SQL exceptions → a bad seed/config migration **fails to apply**, leaving existing access untouched (spec edge case 6).

### Error keys (new `Strings.resx` entries; mirrored in `Strings.fr.resx`)
| Key | English text |
|---|---|
| `Rbac_AccessDenied` | "You don't have permission to do that." |
| `AccessDenied_Title` | "Access denied" |
| `AccessDenied_Message` | "You don't have permission to view this area." |
| `AccessDenied_BackToStore` | "Back to the store" |
| `AccessDenied_BackToAdmin` | "Back to admin home" |

### Edge-case coverage (spec §5)
| Edge case | Handled by |
|---|---|
| Mid-session revocation | DB lookup per check (`authorize()`, behavior) + `PermissionState` re-hydration on denial |
| Roles changed while signed out | Nothing cached client-side; next sign-in hydrates fresh |
| Customer/visitor direct-links to admin | Route policy → sign-in redirect (visitor) or in-place `AccessDeniedView` (customer) |
| View-only user tries to modify | `[RequiresPermission(edit)]` on the mutating command + RLS write policy |
| Self-role-change request | DB trigger refusal (no runtime route exists anyway) |
| Zero-Super-Admin change | DB trigger rejects the statement |
| Deactivated staff account | Deactivation = Supabase user ban (`banned_until` via dashboard/SQL — settled decision): blocks token refresh and authentication, so every subsequent action is refused; `user_roles` rows retained for the future admin UI |

## 10. Database Schema & RLS Policies

### Schema (migration `0007_create_rbac.sql`)
```sql
CREATE TABLE roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name_key TEXT NOT NULL UNIQUE,          -- resource key; localized in Web
    is_system BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code TEXT NOT NULL UNIQUE,              -- 'products.view', 'orders.refund', ...
    module TEXT NOT NULL                    -- 'products', 'orders', ...
);

CREATE TABLE role_permissions (
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE user_roles (
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, role_id)
);

CREATE INDEX idx_user_roles_user_id ON user_roles(user_id);
CREATE INDEX idx_role_permissions_role_id ON role_permissions(role_id);
```

### Authorization function — the single RLS predicate
```sql
CREATE OR REPLACE FUNCTION public.authorize(requested_permission TEXT)
RETURNS BOOLEAN LANGUAGE plpgsql SECURITY DEFINER STABLE
SET search_path = public AS $$
BEGIN
    IF auth.uid() IS NULL THEN RETURN false; END IF;

    -- Admin-area permissions: enforce the 8-hour admin session constraint
    -- (all catalogue permissions are admin-area this release).
    IF NOT EXISTS (
        SELECT 1 FROM auth.sessions s
        WHERE s.id = (auth.jwt() ->> 'session_id')::uuid
          AND s.created_at > now() - interval '8 hours'
    ) THEN RETURN false; END IF;

    RETURN EXISTS (
        SELECT 1
        FROM user_roles ur
        JOIN role_permissions rp ON rp.role_id = ur.role_id
        JOIN permissions p ON p.id = rp.permission_id
        WHERE ur.user_id = auth.uid() AND p.code = requested_permission
    );
END $$;

CREATE OR REPLACE FUNCTION public.get_my_permissions()
RETURNS SETOF TEXT LANGUAGE sql SECURITY DEFINER STABLE
SET search_path = public AS $$
    SELECT p.code FROM user_roles ur
    JOIN role_permissions rp ON rp.role_id = ur.role_id
    JOIN permissions p ON p.id = rp.permission_id
    WHERE ur.user_id = auth.uid();
$$;
```

### RLS policies
```sql
ALTER TABLE roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE permissions ENABLE ROW LEVEL SECURITY;
ALTER TABLE role_permissions ENABLE ROW LEVEL SECURITY;
ALTER TABLE user_roles ENABLE ROW LEVEL SECURITY;

-- Read-only config: SELECT only; NO insert/update/delete policies on any RBAC table.
CREATE POLICY "roles_select_authenticated" ON roles
    FOR SELECT USING (auth.uid() IS NOT NULL);
CREATE POLICY "permissions_select_authenticated" ON permissions
    FOR SELECT USING (auth.uid() IS NOT NULL);
CREATE POLICY "role_permissions_select_authenticated" ON role_permissions
    FOR SELECT USING (auth.uid() IS NOT NULL);
CREATE POLICY "user_roles_select_own" ON user_roles
    FOR SELECT USING (user_id = auth.uid() OR public.authorize('admin_users.view'));

-- Migrate existing policies off JWT role-name checks (FR-5 / FR-11):
--   customers (0001):  ... OR (SELECT auth.jwt() ->> 'role') = 'admin'
--                      → ... OR public.authorize('customers.view')
--   product-images (0004): role = 'admin' clauses
--                      → public.authorize('products.view' / 'products.create' / 'products.edit' / 'products.delete')
```

### Triggers
```sql
-- 1. Auto-Customer at account creation (FR-1) — fires for every registration route.
CREATE TRIGGER on_auth_user_created AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.assign_customer_role();

-- 2. Super-admin floor (FR-10): BEFORE DELETE/UPDATE on user_roles and
--    role_permissions — raise if the change would leave zero Super Admin holders.
-- 3. System-role protection (FR-2): BEFORE UPDATE/DELETE on roles where is_system.
-- 4. Self-change guard (FR-10): BEFORE INSERT/UPDATE/DELETE on user_roles
--    where auth.uid() = user_id (NULL auth.uid() — migrations/seeds — exempt,
--    but still subject to the super-admin floor).
```

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

- **⚠️ Risk — ✅ Accepted:** `authorize()` adds a three-table join to every RLS-gated admin statement (vs. the old free JWT string comparison). Accepted as the fundamental price of FR-11 instant revocation; shipped mitigations: `STABLE` per-statement caching plus covering indexes on `user_roles(user_id)` and `role_permissions(role_id)`. Revisit only on a measured performance problem.

---
**Status:** Resolved · **Spec:** `.specs/role-based-access-control/spec.md` · **Created:** 2026-07-14 · **Resolved:** 2026-07-14

<!-- Status lifecycle: "Draft" → "Resolved" once /theshop.resolve settles every ❓ open question and ratifies every 📌 assumption in Section 11 (accepted ⚠️ risks may remain, labeled). /theshop.implement warns while the plan is still Draft. -->

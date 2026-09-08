# Architecture — Admin

> Implementation guide for the admin panel: routing, authorization, RBAC, and RLS. The admin sits inside the same Blazor app under the `/admin/*` route prefix — one codebase, one deployment, shared business logic with the customer surface. Access is governed by the permission-based RBAC system (`.specs/role-based-access-control/`). Load this file only when building or modifying admin features.

---

## Why the admin lives inside the same app

- One codebase, one deployment, no duplication.
- Shared MudBlazor components, shared DTOs, shared Application/Domain logic.
- Same Supabase auth flow, with permission-based access control.
- Faster MVP delivery; admin can be built in parallel with customer features.
- Migration path preserved — Clean Architecture means admin can be extracted to a separate Blazor project later without rewriting business logic. Don't optimize for that now.

---

## The RBAC model

**Access is decided by permissions, never by role names.** A permission is a `module.action` code (e.g. `products.view`, `orders.refund`). A role is a named collection of permissions; a user's effective access is the union of all their roles' permissions. Gate every check on the specific permission a capability needs — two users with different roles but the same permission must see the same capability.

- **Permission catalogue** — `Domain/ValueObjects/PermissionCatalogue.cs` is the single source of truth: ten modules (Products, Categories, Orders, Customers, Coupons, Promotions, Reports, Settings, AdminUsers, Roles) × view/create/edit/delete, plus sensitive actions (`orders.refund`, `customers.export`). The database `permissions` table seed is generated from `PermissionCatalogue.All` so code and DB cannot drift. **Always reference codes through the catalogue** (`PermissionCatalogue.Products.View.Code`) — never hardcode the string.
- **Four system roles**, seeded by migration and immutable at runtime (`Role.IsSystem` + DB trigger both enforce this): `Customer` (no admin permissions), `Support` (orders view/manage, customers view), `Admin` (all merchandising/operations modules), `SuperAdmin` (everything, including Settings, AdminUsers, Roles).
- **No runtime RBAC administration this release.** Roles, permission sets, and staff assignments are provisioned via migrations/store setup. Every new signup automatically receives the Customer role via the `on_auth_user_created` DB trigger.
- **New capability ⇒ new permission from day one.** No feature may ship gated by an "is an Admin" role check or with no gate at all (spec constraint).

### How permissions reach the client

The database's **custom access token hook** (`supabase/migrations/0010_custom_access_token_hook.sql`) stamps three claims into every access token Supabase mints — at sign-in, OTP verification, and every silent refresh:

| JWT claim | Content | Mapped to (by `SupabaseAuthStateProvider`) |
|---|---|---|
| `perms` | effective permission codes across all non-expired role assignments | one `ShopClaimTypes.Permission` (`perm`) claim per code |
| `app_roles` | role name keys, e.g. `["Customer","Admin"]` | one `ClaimTypes.Role` claim per role |
| `perm_v` | the user's `perm_version` counter at mint time | read by `authorize_fresh()` in RLS, not mapped |

Every client-side authorization decision (policies, `AuthorizeView`, `ICurrentUserService.HasPermission`) reads these claims — no per-request database round-trip. Consequences:

- **Staleness is bounded by the access-token TTL.** A role change takes effect at the next token mint (silent refresh) — no sign-out/sign-in required. Emergency revocation = revoke the user's sessions.
- **Client claims are a UX mirror, not security.** RLS re-checks the live `user_roles` tables on every query; a stale or forged claim set cannot grant data access.
- **Fail closed.** A malformed token yields an authenticated principal with zero permission claims; the hook itself returns empty `perms` on any error rather than blocking token issuance.

---

## Routing layout

There is **no separate admin layout** — admin pages use `MainLayout` like the rest of the app. What distinguishes `/admin/*` is authorization, not chrome.

| URL | Route constant | Authorization |
|---|---|---|
| `/` , `/products`, … | `Routes.*` | public |
| `/cart`, `/account` | `Routes.*` | authenticated customer |
| `/admin/products` | `Routes.Admin.ManageProducts` | `PolicyNames.AdminArea` + `products.view` on-page |

Every admin route gets a constant under `Routes.Admin` (`Web/Common/Routes.cs`); every policy name comes from `PolicyNames` (`Web/Common/PolicyNames.cs`) — no magic strings in either case.

---

## Enforcement — four layers, all required

URL prefixes are NOT security. Anyone can type `/admin/products` into a browser or call Supabase APIs directly. All four layers below must be in place for every admin capability.

| Layer | What it gates | Lives in |
|---|---|---|
| 1. Route policy | Reaching any `/admin/*` page at all | `Pages/Admin/_Imports.razor` |
| 2. Per-permission UI | Which modules/actions render for this user | `AuthorizeView Policy="perm:{code}"` in pages |
| 3. Application pipeline | Every command/query, however triggered | `[RequiresPermission]` + `AuthorizationBehavior` |
| 4. **Supabase RLS** | Direct API calls bypassing the UI entirely | `authorize()` / `authorize_fresh()` policies |

**Layer 4 is the only real security boundary.** Layers 1–3 are UX and defense-in-depth — they keep honest users on the right side of the door and produce clean access-denied experiences. Only RLS, enforced at the database, stops a determined caller.

### Layer 1 — route gate (`_Imports.razor`)

Blazor applies directives in `_Imports.razor` to every `.razor` file in the same folder. `Pages/Admin/_Imports.razor` applies the coarse admin-area gate to every admin page — no per-page attribute needed:

```razor
@* Web/Pages/Admin/_Imports.razor *@
@using Microsoft.AspNetCore.Authorization
@using TheShop.Web.Common

@attribute [Authorize(Policy = PolicyNames.AdminArea)]
```

`PolicyNames.AdminArea` (registered statically in `Web/DependencyInjection.cs`) matches any authenticated user holding **at least one** permission claim — every catalogue permission is admin-area this release, so this doubles as "can reach the admin surface". `App.razor`'s `AuthorizeRouteView` handles the failure paths: authenticated-but-denied users see `AccessDeniedView` (message + way back home); anonymous users are redirected to sign-in.

### Layer 2 — fine-grained UI policies (`perm:{code}`)

`ShopAuthorizationPolicyProvider` (`Web/Auth/`) synthesizes a `perm:{code}` policy **on demand** for every catalogue permission — nothing is pre-registered per entry. A policy is satisfied when the principal carries the matching `perm` claim. A `perm:` policy naming a code outside the catalogue resolves to no policy at all, so typos fail fast at authorization time.

Build policy names with the helper, gate the capability, and **hide — never merely disable** — what the user lacks (AC-4):

```razor
@* Web/Pages/Admin/ManageProducts.razor *@
<AuthorizeView Policy="@PolicyNames.Permission(PermissionCatalogue.Products.View.Code)">
    <Authorized>
        @* page content *@
    </Authorized>
    <NotAuthorized>
        <AccessDeniedView />
    </NotAuthorized>
</AuthorizeView>
```

Same pattern for navigation — e.g. the admin link in `ShopAppBar` renders inside `<AuthorizeView Policy="@PolicyNames.AdminArea">`.

### Layer 3 — Application pipeline (`[RequiresPermission]`)

Every admin-capable MediatR command/query declares its permission; `AuthorizationBehavior` checks it via `ICurrentUserService.HasPermission` before the handler runs, and short-circuits with `Result.Fail(RbacErrorKeys.AccessDenied)` — an expected business outcome, not an exception. This catches every request path, including ones no button triggered (FR-7).

```csharp
[RequiresPermission("products.delete")]
public sealed record DeleteProductCommand(Guid Id) : IRequest<Result>;
```

Undecorated requests pass through unchecked — so customer-surface requests are unaffected, and forgetting the attribute on an admin command silently skips this layer. Check for it in review.

### Layer 4 — Supabase RLS (`authorize()`)

RLS policies call `public.authorize('module.action')`, which checks the caller's **live** role assignments (honoring `user_roles.expires_at`) — never `auth.jwt()` role claims. Wrap the call in a scalar subquery so Postgres evaluates it once per statement, not per row:

```sql
-- products: anyone can read, only permission holders can write
CREATE POLICY "products_admin_insert" ON products
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create')));

-- customers: self-read OR customers.view permission
CREATE POLICY "customers_select" ON customers
    FOR SELECT USING (
        (SELECT auth.uid()) = id
        OR (SELECT public.authorize('customers.view'))
    );
```

For **high-risk operations** (role management, refunds, customer export), use `authorize_fresh('...')` instead — it additionally rejects tokens minted before the user's last permission change (`perm_v` claim vs `user_access_meta.perm_version`), giving zero-staleness revocation. Ordinary policies stay on `authorize()` and accept TTL-bounded staleness.

**Every Supabase table containing admin-only or user-scoped data must have RLS policies. No exceptions.**

---

## Recipe — adding a new admin capability

1. **Permission first.** If the capability isn't covered by an existing catalogue entry, add it to `PermissionCatalogue` (and the `permissions` table seed migration) before anything else.
2. **Route constant** in `Routes.Admin`; page under `Pages/Admin/` (Layer 1 is inherited automatically).
3. **Gate the page/action** with `AuthorizeView Policy="@PolicyNames.Permission(...)"`, with `AccessDeniedView` in `NotAuthorized`.
4. **Decorate the command/query** with `[RequiresPermission("module.action")]`.
5. **RLS policy** on every table the capability touches, calling `(SELECT public.authorize('module.action'))` — `authorize_fresh` if the operation is sensitive.
6. **Localize** — role names, permission names, and access-denied messages ship in English and French (`Strings.resx` / `Strings.fr.resx`).

---

## Role assignment & provisioning

- **Customers:** automatic — the `on_auth_user_created` trigger assigns the Customer role at signup. Never assign it manually.
- **Staff:** development-time seeding/migrations only. There are no runtime screens or routes for RBAC administration this release.
- **DB triggers guard the invariants** regardless of route: system roles cannot be renamed/deleted or have permissions edited; no user can change their own role assignments; no change may leave the platform with zero Super Admins.
- **Propagation:** a role change is reflected in the user's next minted token — within one access-token TTL, no re-login needed. RLS reflects it immediately on the next query regardless of the token.

---

## Future migration path

If the business outgrows the single-app pattern (5+ admin staff with independent deploy cadence, IP-allowlist requirements, separate admin team), the admin can be promoted to a separate subdomain (`admin.yourshop.ca`, DNS-only) or a separate Blazor project (UI extraction only — `Application`, `Domain`, and `Infrastructure` are already shared). The permission model also anticipates custom roles, runtime role administration, and store/region scoping without redesign. Don't optimize for any of this prematurely.

---

## Common mistakes

| Mistake | Fix |
|---|---|
| Checking role names (`Roles = "admin"`, `IsInRole`, `app_roles`) to gate a capability | Gate on the specific permission: `PolicyNames.Permission(...)` in UI, `[RequiresPermission]` in Application, `authorize('module.action')` in RLS |
| Hardcoding a permission string in `.razor`/C# | Reference it via `PermissionCatalogue.{Module}.{Action}.Code` |
| `@layout AdminLayout` or building admin-specific chrome | No `AdminLayout` exists — admin pages use `MainLayout`; `/admin/*` differs by authorization only |
| Reading the role from Supabase `user_metadata` or `auth.jwt() ->> 'role'` | Obsolete pre-RBAC pattern. Client claims come from the access token hook (`perms`/`app_roles`); RLS uses `authorize()` |
| Pre-registering a policy per permission in `AddAuthorizationCore` | Don't — `ShopAuthorizationPolicyProvider` synthesizes `perm:{code}` policies on demand |
| Bare `public.authorize(...)` in an RLS predicate | Wrap it: `(SELECT public.authorize(...))` — once per statement, not per row |
| Admin command/query without `[RequiresPermission]` | Undecorated requests skip Layer 3 entirely — every admin capability must declare its permission |
| Disabling (instead of hiding) controls the user lacks permission for | AC-4: absent from the UI, not merely disabled — use `AuthorizeView`, not `Disabled="..."` |
| Telling a user to log out/in after a role change | Not required — the next token refresh picks up the change; RLS reflects it immediately |
| New feature gated by "is an Admin" or nothing | Every new capability gets its own catalogue permission from day one |
| Putting admin-specific Application handlers under `Features/Admin/*` | Admin uses the *same* handlers as customer-facing code when the operation is identical. Fork only for elevated capabilities (bulk update, soft-delete, …) — gated by `[RequiresPermission]`, not by role checks |

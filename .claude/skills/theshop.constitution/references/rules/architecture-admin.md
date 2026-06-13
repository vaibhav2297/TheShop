# Architecture — Admin

> Implementation guide for the admin panel: routing, layout, authorization, RLS, and role wiring. The admin sits inside the same Blazor app under the `/admin/*` route prefix — one codebase, one deployment, shared business logic with the customer surface. Load this file only when building or modifying admin features.

---

## Why the admin lives inside the same app

- One codebase, one deployment, no duplication.
- Shared MudBlazor components, shared DTOs, shared Application/Domain logic.
- Same Supabase auth flow, with role-based access control.
- Faster MVP delivery; admin can be built in parallel with customer features.
- Migration path preserved — Clean Architecture means admin can be extracted to a separate Blazor project later without rewriting business logic. Don't optimize for that now.

---

## Routing layout

| URL | Layout | Authorization |
|---|---|---|
| `yourshop.ca/` | `MainLayout` | public |
| `yourshop.ca/products` | `MainLayout` | public |
| `yourshop.ca/cart` | `MainLayout` | authenticated customer |
| `yourshop.ca/account` | `MainLayout` | authenticated customer |
| `yourshop.ca/admin` | `AdminLayout` | role `admin` |
| `yourshop.ca/admin/products` | `AdminLayout` | role `admin` |
| `yourshop.ca/admin/orders` | `AdminLayout` | role `admin` |
| `yourshop.ca/admin/customers` | `AdminLayout` | role `admin` |

---

## `_Imports.razor` — apply layout & authorization to every admin page

Blazor automatically applies directives in `_Imports.razor` to every `.razor` file in the same folder. Drop one file into `Pages/Admin/` and you don't have to repeat `@layout` and `[Authorize]` on each page.

```razor
@* Web/Pages/Admin/_Imports.razor *@
@using Microsoft.AspNetCore.Authorization
@using TheShop.Web.Components.Layout

@layout AdminLayout
@attribute [Authorize(Roles = "admin")]
```

Every page in `Pages/Admin/` automatically gets `AdminLayout` and the admin role requirement. Pages stay clean — no per-page layout or authorize attribute:

```razor
@* Web/Pages/Admin/AdminProducts.razor *@
@page "/admin/products"
@using TheShop.Web.Resources
@inject IMediator Mediator

<PageTitle>@Strings.AdminProducts_PageTitle</PageTitle>
<MudText Typo="Typo.h4">@Strings.AdminProducts_Heading</MudText>

<MudDataGrid Items="@_products" Loading="@_loading">
    @* columns *@
</MudDataGrid>
```

(Note: Rule 20 still applies — move the `@code` block to `AdminProducts.razor.cs`. The example above is condensed.)

---

## `AdminLayout` component

Sidebar navigation + admin-specific styling. Customer pages continue to use `MainLayout`.

```razor
@* Web/Components/Layout/AdminLayout.razor *@
@inherits LayoutComponentBase
@using TheShop.Web.Resources

<MudLayout>
    <MudAppBar Color="Color.Primary">
        <MudText Typo="Typo.h6">@Strings.Admin_AppName</MudText>
    </MudAppBar>

    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.Large" Class="py-6">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>
```

---

## Authorization wiring

Configure Blazor's authorization in `Program.cs`:

```csharp
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

builder.Services.AddScoped<AuthenticationStateProvider, SupabaseAuthStateProvider>();
```

Custom `AuthenticationStateProvider` reads the role claim from the Supabase JWT:

```csharp
// Web/Auth/SupabaseAuthStateProvider.cs
public sealed class SupabaseAuthStateProvider(Supabase.Client supabase) : AuthenticationStateProvider
{
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var session = supabase.Auth.CurrentSession;
        if (session is null) return new AuthenticationState(new ClaimsPrincipal());

        var role = session.User.UserMetadata?["role"]?.ToString() ?? "customer";
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, session.User.Id),
            new(ClaimTypes.Email, session.User.Email),
            new(ClaimTypes.Role, role),
        ];
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "supabase")));
    }
}
```

---

## Hiding admin links from customers

Use `AuthorizeView` to conditionally render admin navigation in `MainLayout`:

```razor
<AuthorizeView Roles="admin">
    <Authorized>
        <MudButton Href="@Routes.Admin.Dashboard" Color="Color.Primary">
            @Strings.Nav_AdminPanel
        </MudButton>
    </Authorized>
</AuthorizeView>
```

---

## Security — three independent layers, all required

URL prefixes are NOT security. Anyone can type `/admin/products` into a browser. Real protection comes from three layers — all must be in place.

| Layer | Protects against | Lives in |
|---|---|---|
| 1. `[Authorize]` attribute | Logged-in customers navigating to admin pages | `Pages/Admin/_Imports.razor` |
| 2. Authorization policies | Role mismatch, expired session | `Program.cs` policy config |
| 3. **Supabase RLS** | Direct API calls bypassing the UI entirely | database policies |

**Layer 3 is the only real security boundary.** Layers 1–2 are UX checks — they keep honest users on the right side of the door. A determined caller can bypass them by hitting Supabase APIs directly. Only Row-Level Security (RLS) policies enforced at the database stop that.

### RLS examples

```sql
-- products: anyone can read, only admin can write
CREATE POLICY "products_select_all" ON products
    FOR SELECT USING (true);

CREATE POLICY "products_admin_write" ON products
    FOR ALL USING (auth.jwt() ->> 'role' = 'admin');

-- orders: customers see only their own, admin sees all
CREATE POLICY "orders_customer_select" ON orders
    FOR SELECT USING (customer_id = auth.uid());

CREATE POLICY "orders_admin_all" ON orders
    FOR ALL USING (auth.jwt() ->> 'role' = 'admin');
```

**Every Supabase table containing admin-only or user-scoped data must have RLS policies. No exceptions.**

---

## Promoting a user to admin

Default role on signup is `customer`. To promote, update the JWT metadata:

```sql
UPDATE auth.users
SET raw_user_meta_data = raw_user_meta_data || '{"role": "admin"}'::jsonb
WHERE email = 'you@yourshop.ca';
```

**Important — the user must log out and back in.** JWTs are issued at login and contain the role at that moment. Updating the database does not change existing tokens. A fresh login mints a new JWT carrying the admin role.

---

## Future migration path

If the business outgrows the single-app pattern (5+ admin staff with independent deploy cadence, IP-allowlist requirements, separate admin team), the admin can be promoted to:

- **A separate subdomain** (`admin.yourshop.ca`) — DNS-only, no code changes.
- **A separate Blazor project** — UI extraction only; `Application`, `Domain`, and `Infrastructure` are already shared.

Clean Architecture preserves these migration options. Don't optimize for them prematurely.

---

## Common mistakes

| Mistake | Fix |
|---|---|
| Per-admin-page `@layout AdminLayout` + `[Authorize(Roles = "admin")]` | Drop the `_Imports.razor` in `Pages/Admin/` and let it inherit |
| Admin table without an RLS policy | Add one — admin URL guards do not stop direct API calls |
| Customer table without an RLS policy scoping to `auth.uid()` | Add `customer_id = auth.uid()` policy — without it, any authenticated user can read any other user's data |
| Admin promotion done by raw SQL while the user stays logged in | After the SQL update, log the user out and back in to re-mint the JWT |
| Putting admin-specific Application handlers under `Features/Admin/*` | Admin uses the *same* Application handlers as customer-facing code when the operation is identical. Only fork the handler if the admin requires elevated capabilities (bulk update, soft-delete, etc.) — and route admin permission checks through MediatR pipeline behaviors driven off the JWT role claim |

# Role-Based Access Control

## 1. Problem Statement

The Shop is run by more than one kind of person: customers shop, while staff manage products, orders, customers, promotions, and store settings from the admin panel. Today there is no way to say *who* is allowed to do *what* — anyone with admin access could change prices, delete products, or read customer data. As the team grows (support staff, catalogue managers, future regional admins), giving everyone full access is a security and compliance risk, and giving nobody access blocks the business from operating.

**Solution (one line):** Every user holds one or more roles made up of fine-grained, module-level permissions that control exactly which store areas and actions they can use — enforced by the platform itself on every action.

**In scope:**
- A complete role-and-permission model: four built-in roles (Customer, Support, Admin, Super Admin) and the fine-grained permission catalogue, organized by store module, that governs the entire admin panel.
- Enforcement of those permissions on every action, by the platform itself — hiding a control is never the only protection; a request made by any route is checked the same way.
- Initial roles, their permission sets, and staff role assignments established through development-time seeding/configuration and store setup; every newly registered user automatically receives the Customer role at account creation.

**Out of scope:**
- RBAC administration of any kind at runtime — no screens **or any other runtime route** to create, edit, delete, or assign roles, permissions, or user role assignments, and no runtime RBAC administration by business users. All role and permission changes happen through development-time configuration this iteration.
- Audit logging of privileged actions — deliberately removed from this iteration; it will arrive as its own future feature.
- Creating custom roles or editing the permission set of built-in roles (the model must accommodate this later, but no role-editing capability ships now).
- Multi-store, warehouse, or regional scoping of roles and permissions (a future evolution the model must not preclude).
- Any change to how users sign in — sign-up, one-time codes, and sessions are covered by the existing Authentication feature.

## 2. Functional Requirements

1. **FR-1:** Every user account holds one or more roles. A user who signs up through the store automatically receives the Customer role — and nothing more — at the moment their account is created.
2. **FR-2:** The platform ships with four built-in system roles — Customer, Support, Admin, and Super Admin — which cannot be renamed or deleted.
3. **FR-3:** Every capability in the admin panel is governed by a specific fine-grained permission, organized by module — Products, Categories, Orders, Customers, Coupons, Promotions, Reports, Settings, Admin Users, and Roles — and by action within the module (at minimum: view, create, edit, delete, plus module-specific sensitive actions such as issuing a refund or exporting customer data).
4. **FR-4:** A role is a named collection of permissions. A user may hold multiple roles at once, and their effective access is the union of all their roles' permissions; users never receive permissions directly, only through roles.
5. **FR-5:** Access is decided by permissions, not by role names — each admin screen, menu item, and action is available exactly when the user holds the specific permission it requires, so two users with different roles but the same permission see the same capability.
6. **FR-6:** The admin panel is reachable only by users who hold at least one admin-area permission. Customers, and visitors who are not signed in, can never see or reach any admin screen.
7. **FR-7:** Every action is checked by the platform itself, regardless of how it is requested — a request made outside the normal screens is refused exactly as if the button had never been shown.
8. **FR-8:** Staff role assignments (everything beyond the automatic Customer role) are established through development-time configuration and store setup, and the store enforces exactly the assignments that are configured.
9. **FR-9:** Super Admin holds every permission on the platform, including the sensitive modules: Admin Users, Roles, and Settings.
10. **FR-10:** No user can change their own role assignments through the store — not even a Super Admin — and no change to roles, by any route, can leave the platform without at least one Super Admin.
11. **FR-11:** Role and permission changes take effect promptly: a permission revoked from a user blocks their very next attempt to use it, even while they are signed in — no sign-out is required.
12. **FR-12:** An unauthorized attempt — following a direct link, using a stale open screen, or any other route — is blocked with a clear access-denied message and changes nothing.

## 3. Functional Behaviors

### Behavior 1: Sign up and receive the Customer role
- **User does:** A new shopper completes sign-up through the store.
- **User sees:** A normal customer experience — browsing, cart, checkout, and their own account. Nothing admin-related is visible or reachable anywhere.

### Behavior 2: Work inside a permission boundary
- **User does:** A Support user signs in and opens the admin panel.
- **User sees:** Only the areas their permissions allow (e.g., Orders and Customers) — modules they cannot use, such as Settings or Admin Users, do not appear in their navigation at all, and action buttons they cannot perform (e.g., delete) are absent from the screens they can see.

### Behavior 3: Attempt something not permitted
- **User does:** A Support user pastes a direct link to the Settings page, or triggers an action from a screen that was open before their permission was revoked.
- **User sees:** An access-denied message explaining they don't have permission for that area, with a way back to their admin home. Nothing is changed by the attempt.

### Behavior 4: Roles are updated in configuration
- **User does:** Nothing — a staff member's roles are changed through the store's development-time configuration (e.g., promoted from Support to Admin).
- **User sees:** From their very next action, the store reflects the new roles: newly granted areas appear in their navigation, and anything revoked is blocked and disappears.

## 4. Constraints

- Built-in roles follow the principle of least privilege — each grants only the permissions its job requires. The shipped defaults are: Support can view and manage orders and view customers, but cannot touch Products, Coupons, Promotions, Settings, Admin Users, or Roles; Admin can manage all merchandising and operations modules — Products, Categories, Orders, Customers, Coupons, Promotions, Reports — but not Settings, Admin Users, or Roles, which are Super Admin only. The store owner may tune these permission sets manually later.
- Initial roles, their permission sets, and staff role assignments are put in place during development and store setup; business users have no way to administer roles or permissions at runtime this iteration.
- The platform must always have at least one Super Admin; any change that would leave zero — including a configuration change — is rejected.
- Users cannot grant, revoke, or otherwise change their own role assignments through the store under any circumstances.
- Built-in system roles cannot be renamed, deleted, or have their permission sets edited by anyone at runtime.
- Every new store capability added in the future must be covered by a specific permission from day one — no feature may ship gated by "is an Admin" shortcuts or with no gate at all.
- The role and permission structure must accommodate future custom roles, runtime role administration, and store/region-scoped assignments without a redesign of how access is granted today.
- Signed-in admin staff have shorter session lifetimes than customers: an admin session ends after 8 hours or on sign-out, whichever comes first, and returning to the admin panel after expiry requires signing in again. Customer session behavior is unchanged from the Authentication feature.
- Sensitive operations that ship in the admin panel — such as changing store settings or deactivating an account — always require an explicit confirmation dialog before taking effect; no re-entry of a sign-in code is required.
- All role names, permission names, and access-denied messages shown anywhere in the store are available in both English and French.
- The first Super Admin account is established as part of store setup, before any other admin exists — there is no in-app flow for creating it.

## 5. Edge Cases & Error Handling

- **Edge case:** A signed-in admin has a permission revoked (via a configuration change) while working in that module → **User experience:** Their very next action in that module is blocked with an access-denied message, the module disappears from their navigation, and they are guided back to their admin home.
- **Edge case:** A user's roles are changed while they are signed out → **User experience:** Nothing visible happens until they next sign in, at which point they see exactly the access their current roles allow.
- **Edge case:** A customer (or signed-out visitor) follows a direct link to an admin page → **User experience:** They never see admin content — they are shown an access-denied (or sign-in) screen with a way back to the store.
- **Edge case:** An admin with view-only access to a module tries to modify something via a direct link or stale screen → **User experience:** The change is refused with an access-denied message and nothing is modified.
- **Edge case:** A request attempts to change the requester's own roles, by any route → **User experience:** The request is refused with a message that users cannot change their own roles; nothing is changed.
- **Edge case:** A change would leave the store with zero Super Admins → **User experience:** The change is rejected and existing access is untouched; the store always retains at least one Super Admin.
- **Edge case:** A staff member leaves and their account is deactivated → **User experience:** For them, every subsequent action is blocked and they are treated as signed out; for admins, the account shows as deactivated with its roles still visible on the account.

## 6. Acceptance Criteria

- [ ] **AC-1:** A newly signed-up user automatically holds exactly the Customer role and cannot see or reach any admin screen, including by direct link. Verifies FR-1, FR-6.
- [ ] **AC-2:** The four built-in roles — Customer, Support, Admin, Super Admin — exist out of the box with their least-privilege permission sets, and offer no way to be renamed or deleted. Verifies FR-2.
- [ ] **AC-3:** Every module listed in the spec (Products, Categories, Orders, Customers, Coupons, Promotions, Reports, Settings, Admin Users, Roles) is reachable only through its own permissions, and view/create/edit/delete are separately controllable within a module. Verifies FR-3, FR-5.
- [ ] **AC-4:** An admin's navigation and screens show only the modules and actions their permissions allow — a capability they lack is absent from the UI, not merely disabled. Verifies FR-5, FR-6.
- [ ] **AC-5:** An action attempted without the required permission is refused by the platform even when the request is made outside the normal screens — hiding the control is never the only protection. Verifies FR-7, FR-12.
- [ ] **AC-6:** A staff account configured with a role during development has exactly that role's access when the store runs — e.g., a seeded Support account sees and can do only what Support allows. Verifies FR-8.
- [ ] **AC-7:** A role change made in configuration is reflected from the affected user's next action onward, and a revoked permission blocks their very next attempt to use it mid-session. Verifies FR-8, FR-11.
- [ ] **AC-8:** No user can change their own role assignments through the store — any such request is refused. Verifies FR-10.
- [ ] **AC-9:** Any change that would leave the platform with zero Super Admins is rejected with a clear message and no change is made. Verifies FR-10.
- [ ] **AC-10:** An unauthorized attempt via direct link or a stale open screen shows an access-denied message and changes nothing. Verifies FR-12.
- [ ] **AC-11:** All role names, permission names, and access-denied messages are available in both English and French. Verifies the localization constraint.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

> ⚠️ Blocking, load-bearing uncertainties do **not** belong here — those are asked before the spec is written. This list holds only cheap-to-change defaults.

---
**Status:** Confirmed   ·   **Created:** 2026-07-14   ·   **Clarified:** 2026-07-14

<!-- Status lifecycle: "Draft — N open assumption(s)" → "Confirmed" once /theshop.clarify resolves them all (N = 0). -->

# Admin Console

## 1. Problem Statement

Staff who run The Shop already have permission to reach the admin area, but they land on an individual management page with no sense of the store as a whole and no single place to move between the areas they oversee. When a catalogue manager or admin signs in, they can't see at a glance how many products, brands, categories, users, or roles exist, and they have to know each management page's location to get anywhere. Without a home base, the admin area feels like a collection of disconnected pages rather than one console, and every admin wastes time hunting for the section they need.

**Solution (one line):** A single admin dashboard at `/admin` that shows an at-a-glance overview card for each store area the admin is allowed to see — with its current record count — and takes them straight to that area's management page.

### Scope

**In scope:**
- An admin dashboard landing page reachable at `/admin`, shown to any signed-in user who holds at least one admin-area permission.
- An overview card for each of five governed modules: **Products, Categories, Brands, Users, Roles**.
- A current total count of records displayed on each module's card.
- Navigation from each card to that module's respective management page.
- Permission-aware presentation: a card appears only when the admin holds the view permission for that module.
- The admin-panel entry point (the account menu) leading to this dashboard instead of directly to a single module page.

**Out of scope:**
- Building or changing the module management pages themselves (Manage Products, Manage Brands, and the not-yet-built Categories/Users/Roles pages) — each is delivered by its own feature. This dashboard only counts and links to them.
- A Permissions overview card — permissions are a fixed, code-defined catalogue with no create/manage action (now or planned), so a Permissions tile would be a dead-end; it is deliberately excluded. Roles, which staff will manage in a future feature, is included.
- Any live/streaming metrics, charts, sales figures, order data, or analytics beyond simple record counts.
- Any runtime creation, editing, deletion, or assignment of roles, permissions, or user role assignments — the Role-Based Access Control feature keeps that at development-time configuration. The dashboard only links to each module's management page; building those pages (and honoring that RBAC constraint inside them) is each module's own feature.
- Changing who is allowed into the admin area or how permissions are defined — that is owned by the Role-Based Access Control feature.
- Customer-facing storefront pages.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Guest (not signed in) | Nothing here | Reach `/admin` — redirected to sign-in; never sees dashboard content |
| Signed-in customer | Nothing here | Reach `/admin` — shown an access-denied message with a way back to the store |
| Admin / staff (holds ≥ 1 admin-area permission) | See the dashboard and every module card their permissions allow; open any module's page from its card | See a card for a module they lack the view permission for |

## 2. Functional Requirements

1. **FR-1:** The admin console is reachable at `/admin` by any signed-in user who holds at least one admin-area permission; customers and guests can never see or reach it.
2. **FR-2:** The dashboard presents an overview card for each governed module the admin can view: Products, Categories, Brands, Users, and Roles.
3. **FR-3:** Each module card displays the current total count of records in that module (e.g., the number of products). The Users count includes all registered accounts — customers and staff — and every count includes all records regardless of status.
4. **FR-4:** Each module card provides navigation to that module's respective management page (e.g., the Products card opens the Manage Products page).
5. **FR-5:** A module card is shown only when the admin holds that module's view permission; a module they cannot view is absent from the dashboard, not shown greyed-out or disabled.
6. **FR-6:** The admin-panel entry point in the account menu leads to the `/admin` dashboard, rather than opening a single module's page directly.
7. **FR-7:** If a module's count cannot be retrieved, that card still renders with its name and navigation, showing a neutral placeholder in place of the number rather than failing the whole dashboard.
8. **FR-8:** Every label, heading, count description, and message on the dashboard is available in both English and French.

## 3. Functional Behaviors

### Behavior 1: Open the admin console
- **User does:** A signed-in admin selects the admin-panel entry from the account menu, or navigates to `/admin`.
- **User sees:** A dashboard titled for the admin console showing one overview card per module they are allowed to view, each with the module name and its current record count.

### Behavior 2: Jump to a module's page
- **User does:** The admin selects a module's card (or its navigation control).
- **User sees:** They are taken to that module's management page.

### Behavior 3: A limited admin sees only their areas
- **User does:** An admin whose permissions cover only some modules opens the dashboard.
- **User sees:** Only the cards for modules they can view; modules they lack permission for are simply not present.

### Behavior 4: An unauthorized visitor tries the dashboard
- **User does:** A customer or signed-out visitor follows a direct link to `/admin`.
- **User sees:** They never see admin content — a customer gets an access-denied message with a way back to the store; a signed-out visitor is sent to sign in.

## 4. Constraints

- The dashboard shows record counts only — no sales figures, revenue, order data, charts, or trend lines. Counts are user-facing quantities, not performance indicators.
- Access to `/admin` and the visibility of each card are governed entirely by the permissions defined in the Role-Based Access Control feature; this feature adds no new access rules of its own.
- Consistent with Role-Based Access Control, no role can be created, edited, or assigned at runtime this release; the Roles card links to its management page, which remains view-only until runtime role management is delivered as its own feature.
- The admin console renders within the store's existing shared chrome — the same header and footer customers see, including the cart and storefront navigation.
- Counts are fetched when the dashboard is opened and re-fetched on refresh; they do not update live while the page is open.
- A capability the admin lacks is absent from the dashboard, never merely disabled — hiding is by omission.
- All dashboard text (titles, module names, count labels, access-denied and unavailable messages) is available in English and French.
- The dashboard is keyboard-reachable: every card and its navigation control can be focused and activated without a mouse, with a visible focus indicator, and each count is announced to assistive technology together with its module name.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | The `/admin` dashboard is accessible only to a signed-in user holding at least one admin-area permission. | A customer is shown access-denied; a signed-out visitor is redirected to sign-in. No dashboard content is revealed. |
| **RULE-2** | A module's overview card is displayed only if the admin holds that module's view permission. | The card is omitted entirely from the dashboard. |
| **RULE-3** | Each displayed count reflects the true current total of records in that module at the time the dashboard is loaded. | If the true total cannot be determined, the card shows a neutral placeholder instead of an incorrect or zero number. |
| **RULE-4** | Selecting a module card navigates to that module's designated page and nothing else. | N/A — navigation target is fixed per module. |

## 5. Edge Cases & Error Handling

- **Edge case:** An admin holds an admin-area permission but none for the five dashboard modules (e.g., only a module not represented here) → **User experience:** The dashboard loads but shows no module cards, with a short message explaining there is nothing they can manage here yet.
- **Edge case:** A module's count cannot be loaded → **User experience:** The card still appears with its name and a working link; a neutral placeholder (e.g., "—") stands in for the number, and the rest of the dashboard is unaffected.
- **Edge case:** A module currently has zero records → **User experience:** The card shows a count of `0`, not an empty or hidden card.
- **Edge case:** An admin selects a module whose management page has not been built yet → **User experience:** They arrive at that module's page as it currently exists (a placeholder shell); the dashboard's job — count and link — still works.
- **Edge case:** A signed-in admin has a module's view permission revoked (via configuration) and reopens the dashboard → **User experience:** That module's card is no longer present from their next visit onward.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given a signed-in admin who holds the view permission for all five modules, when they open `/admin`, then they see one overview card per module (Products, Categories, Brands, Users, Roles), each showing the module name and its current record count. Verifies FR-1, FR-2, FR-3.
- [ ] **AC-2:** Given an admin viewing the dashboard, when they select a module's card, then they are navigated to that module's respective management or overview page. Verifies FR-4.
- [ ] **AC-3:** Given an admin who lacks the view permission for a module, when they open the dashboard, then that module's card is absent from the page (not shown disabled). Verifies FR-5, RULE-2.
- [ ] **AC-4:** Given a signed-in customer, when they follow a direct link to `/admin`, then they are shown an access-denied message and no dashboard content; given a signed-out visitor, they are redirected to sign-in. Verifies FR-1, RULE-1.
- [ ] **AC-5:** Given a module whose count cannot be retrieved, when the dashboard loads, then that card still renders with its name and a working link and shows a neutral placeholder instead of a number, and the other cards display normally. Verifies FR-7, RULE-3.
- [ ] **AC-6:** Given a signed-in admin, when they select the admin-panel entry in the account menu, then they land on the `/admin` dashboard. Verifies FR-6.
- [ ] **AC-7:** Given the dashboard displayed in French, when the admin views it, then all titles, module names, count labels, and messages appear in French. Verifies FR-8.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-07-23   ·   **Clarified:** 2026-07-23

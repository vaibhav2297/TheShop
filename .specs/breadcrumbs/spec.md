# Breadcrumbs

## 1. Problem Statement

Shoppers who browse several levels into The Shop — from Home into a category, a sub-category, and finally a specific product — lose track of where they are. Their only way back up is the browser's back button or starting over from the top navigation, which feels clumsy on a premium store. Admins face the same problem inside the admin panel: managing a single product or order can be three or four levels deep (Dashboard → Products → editing one item), with no quick way to step back to a parent screen. Without a persistent "you are here" trail, both customers and admins navigate by guesswork and re-traversal, adding friction to browsing and slowing day-to-day admin work.

**Solution (one line):** Every page below the top level shows a breadcrumb trail of its place in the site, letting the user see where they are and jump back to any parent level in one click.

**In scope:**
- Breadcrumb trails on storefront browsing pages **and** admin panel pages.
- Clickable parent levels, with the current page shown as the final, non-clickable item.
- Labels available in both English and French.

**Out of scope:**
- The Home page, the admin Dashboard landing, and the authentication screens — these show no trail.
- Any change to the existing top navigation / app bar.
- Any other navigation aid (e.g., a full site map or "recently viewed" list).

> **Admin sequencing:** Admin panel pages do not exist in the app yet. The admin breadcrumb requirements (FR-2, AC-2) are specified now and take effect as each admin page is built — they remain part of this feature's definition of done as those pages ship, rather than being deferred to a separate spec.

## 2. Functional Requirements

1. **FR-1:** A breadcrumb trail appears on every storefront page below the home level, showing the path from Home down to the current page.
2. **FR-2:** A breadcrumb trail appears on every admin panel page below the dashboard level, showing the path from the Dashboard down to the current page.
3. **FR-3:** Every level in a trail except the final one is a link that takes the user directly to that level's page.
4. **FR-4:** The current page is the last item in the trail and is shown as plain, non-clickable text.
5. **FR-5:** A trail reflects the page's fixed place in the site (or admin) hierarchy — not the order in which the user happened to visit pages.
6. **FR-6:** A level that represents a specific item (a category, a product, an order) shows that item's own name as the user would recognize it, not a generic label.
7. **FR-7:** A visual separator distinguishes each level from the next.
8. **FR-8:** All breadcrumb labels are shown in the site's active language and are available in both English and French.

## 3. Functional Behaviors

### Behavior 1: Browse to a product deep in a category
- **User does:** Starts at Home, opens a category, then a sub-category, then a product detail page.
- **User sees:** A breadcrumb trail near the top of the product page, e.g. "Home / Categories / Outerwear / Wool Parka", where every item except the product name is a clickable link.

### Behavior 2: Jump back to a parent level
- **User does:** On a product or sub-category page, clicks a parent level in the trail (e.g. "Outerwear").
- **User sees:** They land on that parent page, and its breadcrumb trail now ends at that level.

### Behavior 3: Admin opens a specific record to manage
- **User does:** In the admin panel, goes Dashboard → Products → opens a specific product to edit (or Dashboard → Orders → opens one order).
- **User sees:** A breadcrumb trail such as "Dashboard / Products / Wool Parka", with Dashboard and Products clickable and the edited item shown as the final, non-clickable level.

### Behavior 4: View a breadcrumbed page in French
- **User does:** Views any page that has a breadcrumb trail while the site language is French.
- **User sees:** The breadcrumb labels — both the fixed ones (Home, Categories, Dashboard, Products) and item names where applicable — appear in their French equivalents.

## 4. Constraints

- The first item of a storefront trail is always **Home**; the first item of an admin trail is always the **Dashboard**.
- The current page is always the final item in the trail and is never a link.
- The Home page, the admin Dashboard landing, and the authentication screens (sign-in / sign-up) show no breadcrumb trail.
- All breadcrumb text is drawn from the site's localized content and is available in English and French.
- A breadcrumb reflects the site hierarchy, not the user's browsing history.
- A level representing a specific item uses that item's actual name (category name, product name, order reference), not a placeholder.
- A storefront product's trail follows the product's category path — Home → Category → [Sub-category] → Product. A product that belongs to no category falls back to Home → Products → {Product}.
- A long label must not break or overflow the page layout — it is shortened (e.g. with an ellipsis) while the trail stays tidy.
- The breadcrumb trail must be operable by keyboard and announced to assistive technology as breadcrumb navigation, with the current page marked as the current location.

## 5. Edge Cases & Error Handling

- **Edge case:** A category, product, or order name is very long → **User experience:** The label is truncated with an ellipsis and the full name is available on hover (desktop) or press-and-hold (touch), so nothing is lost and the layout stays intact.
- **Edge case:** A page sits directly below the root (e.g. the Products listing) → **User experience:** The trail is short — "Home / Products" — with no intermediate levels.
- **Edge case:** The user is on the Home page or the admin Dashboard landing → **User experience:** No breadcrumb trail is shown, because they are already at the root.
- **Edge case:** A storefront product belongs to no category → **User experience:** The trail falls back to "Home / Products / {Product}" rather than showing an empty or broken level.
- **Edge case:** The user opens a deep page from a shared link or bookmark without navigating from a parent → **User experience:** The full structural trail still appears, so they can move up the hierarchy even though they arrived cold.
- **Edge case:** A parent in the trail no longer exists (e.g. its category was removed) → **User experience:** That level is shown as plain text instead of a link to a missing page, and the rest of the trail still works.
- **Edge case:** The user views a deep page on a small / mobile screen → **User experience:** Intermediate levels collapse into an expandable "…" so the trail fits on one line — the first level and the current page stay visible — without overflowing horizontally.

## 6. Acceptance Criteria

- [ ] **AC-1:** On any storefront page below the home level, a breadcrumb trail is shown that begins at Home and ends at the current page. Verifies FR-1, FR-4.
- [ ] **AC-2:** On any admin page below the dashboard level, a breadcrumb trail is shown that begins at the Dashboard and ends at the current page. Verifies FR-2, FR-4.
- [ ] **AC-3:** Clicking any non-final level in a trail navigates to that level's page. Verifies FR-3.
- [ ] **AC-4:** The final item in every trail is the current page and is not a link. Verifies FR-4.
- [ ] **AC-5:** The trail shown for a given page is identical whether the user navigated there from a parent or opened it from a direct link. Verifies FR-5.
- [ ] **AC-6:** A trail level that represents a specific item displays that item's actual name. Verifies FR-6.
- [ ] **AC-7:** No breadcrumb trail appears on the Home page, the admin Dashboard landing, or the authentication screens.
- [ ] **AC-8:** Every breadcrumb label appears in both English and French, matching the active site language. Verifies FR-8.
- [ ] **AC-9:** A very long label is shortened so the trail does not break or overflow the layout, on both desktop and small screens. Verifies the long-label constraint.
- [ ] **AC-10:** The breadcrumb trail can be navigated by keyboard, is announced to assistive technology as breadcrumb navigation, and exposes the current page as the current location.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-06-23   ·   **Clarified:** 2026-06-23

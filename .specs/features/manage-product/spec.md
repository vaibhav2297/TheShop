# Manage Product

## 1. Problem Statement

Admins need one screen to find products, manage availability, delete eligible products, and reach Add/Edit flows.

**Solution (one line):** Provide searchable, filterable, sortable product listing with pagination, single/bulk actions, and connections to existing Add/Edit flows.

### Scope

**In scope:**
- Admin product listing, pagination, name search, status/brand/category/price filters, and sorting.
- Variant price ranges and variant count beneath product name.
- Single and bulk Activate, Deactivate, and Delete.
- Add new product and edit selected product through existing flows, completed separately per user confirmation.
- Deletion follows confirmed Brands/Categories policy: permanent, confirmation required, referenced records protected, eligible bulk items processed independently.
- Existing product permissions; English/French interface text.

**Out of scope:**
- Defining or rebuilding Add/Edit fields, validation, images, pricing, stock, or variants; existing flows own those behaviors.
- Bulk field editing, import/export, restore, and trash.
- Changes to checkout, order management, or customer shopping flows beyond product availability.

### Actors & Access

Existing Role-Based Access Control spec governs permissions.

| Actor | May | Must not |
|---|---|---|
| Staff with product-view permission | Browse, search, filter, sort, and page products | Change products without corresponding permission |
| Staff with product-create permission | Use existing Add flow | Edit/delete without corresponding permission |
| Staff with product-edit permission | Use existing Edit flow; activate/deactivate individually or in bulk | Delete without product-delete permission |
| Staff with product-delete permission | Delete eligible products individually or in bulk after confirmation | Delete referenced products |
| Guest / signed-in customer / staff without product-view permission | Receive existing sign-in or access-denied experience when attempting listing access | View product management listing |

Permissions remain independent. Unauthorized controls are absent; direct attempts are refused without changes.

## 2. Functional Requirements

1. **FR-1:** Authorized staff can list Active and Inactive products. Each row shows image, name, brand, category, variant price range, and status, plus selection and permitted row actions. Show variant count beneath product name, such as "4 variants", with singular wording for one variant; no separate variant-count column. Price shows lowest through highest current variant selling price; identical prices show one amount. Apply FR-17 to prices and counts.
2. **FR-2:** Listing provides pagination and current-page indication. Page size is fixed at 10 products, matching Brands/Categories; staff cannot change it.
3. **FR-3:** Search matches product names containing entered text, ignoring capitalization and surrounding spaces. Blank or spaces-only search means no search. Description is not searched. Search combines with filters and sort.
4. **FR-4:** Staff can filter products by status, brand, category, and price. Status takes one value or no restriction. Brand and category each take one or more values, or no restriction; a product matches when it belongs to any chosen brand and any chosen category. Price takes an optional lowest and optional highest CAD amount; a blank end is unbounded. Include product only when at least one FR-17 variant price satisfies both supplied bounds, inclusively. Range overlap alone does not qualify. Keep displayed full variant price range unchanged by filtering. All restrictions combine with search.
5. **FR-5:** Staff can sort products by name ascending/descending, newest/oldest, and lowest variant price ascending/descending. Both price directions use lowest current selling price across FR-17 variants, including when price filter matches another variant. Label price options "Lowest variant price: ascending" and "Lowest variant price: descending", localized per FR-15. Name ascending is default.
6. **FR-6:** Paging retains search, filters, and sort. Changing search, any filter, or sort returns the listing to its first page. Select-all selects only current-page products, the selected count stays visible, and any page or criteria change clears selection.
7. **FR-7:** Add new product opens existing Add flow for authorized staff. Editing a row opens existing Edit flow for that product.
8. **FR-8:** Completed Add/Edit changes appear when staff next view matching listing results. Existing flows retain their completion, cancellation, and validation behavior.
9. **FR-9:** Staff with product-edit permission can activate/deactivate one product or selected products. Activation applies without confirmation. Deactivation requires confirmation naming the product or selected count and stating customers will no longer see it.
10. **FR-10:** Status changes update listing status. Inactive products leave the public catalogue; activation restores eligibility under existing catalogue rules. Product details and order history remain intact through any status change.
11. **FR-11:** Staff with product-delete permission can permanently delete an unreferenced product after confirmation naming it and stating permanence. Cancellation changes nothing.
12. **FR-12:** Deletion refuses products referenced by other store records, including orders regardless of order status. Show referencing-record count and offer deactivation; preserve referenced product and records.
13. **FR-13:** Bulk deletion confirms selected count and permanence, deletes eligible products, retains referenced products, and reports both counts with retained products named. Retained products remain selected.
14. **FR-14:** Every action checks its own product permission when used, including direct attempts and bulk actions.
15. **FR-15:** Interface labels, actions, confirmations, errors, and result messages follow active English/French language. Prices use CAD and active locale formatting, consistent with Product Catalogue.
16. **FR-16:** Listing exposes understandable progress and action results. Loading, empty/no-match, missing-product, failure, partial bulk failure, and last-page recovery follow Section 5.
17. **FR-17:** Price display, price matching, price sorting, and variant count include all non-deleted variants, including inactive and out-of-stock variants. Use each variant's current selling price, including applicable sale price. Deleted variants contribute neither prices nor count. Product-level status filter does not narrow variants used for these calculations.

## 3. Functional Behaviors

### Behavior 1: Find products
- **User does:** Opens listing, searches, filters, sorts, or changes page.
- **User sees:** Matching products in selected order, full variant price range, variant count beneath name, page indication, and permitted actions. Price filtering matches actual variant prices; FR-1 through FR-6 and FR-17 apply.

### Behavior 2: Add or edit
- **User does:** Chooses Add new product or Edit on one row.
- **User sees:** Existing corresponding flow; Edit targets chosen product. Subsequent matching listing results reflect saved changes.

### Behavior 3: Change availability
- **User does:** Activates/deactivates one product or selected products.
- **User sees:** Confirmation where required, actual updated statuses, and changed count; FR-9 and FR-10 govern confirmation and visibility.

### Behavior 4: Delete products
- **User does:** Chooses single or bulk Delete, then confirms or cancels.
- **User sees:** Permanent-deletion warning; confirmation deletes eligible products only. Referenced products remain, with counts and deactivation alternative. Cancellation preserves products and selection.

## 4. Constraints

- Product Catalogue, Role-Based Access Control, Manage Brands, and Manage Categories provide existing product context; only deletion parity is user-confirmed here.
- Add/Edit business behavior remains owned by existing flows. Locating their completed version is a Plan dependency.
- Deletion has no undo, restore, or trash. It never deletes another store record to make a product eligible.
- All listing controls, row selection, status switches, and confirmations are operable by keyboard, show a visible focus indicator, and carry an accessible name. Each confirmation takes focus when it opens and returns focus to the listing when dismissed. Screen-reader announcement of results and selected count is not required here, unlike Manage Brands.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Each capability requires its corresponding product permission at use time. | Hide unauthorized controls; refuse direct attempts without changes. Without product-view permission, reveal no listing content. |
| **RULE-2** | Single/bulk deletion requires explicit confirmation of permanence and target name/count. | Cancellation or dismissal deletes nothing and preserves selection. |
| **RULE-3** | Any reference from another store record, including an order of any status, blocks product deletion. Check each selected product. | Keep product unchanged; show reference count and suggest deactivation. |
| **RULE-4** | Bulk deletion processes eligible products independently. | Referenced products do not block eligible products; report deleted/retained counts, name retained products, keep them selected. |
| **RULE-5** | Search, filters, and sort apply together before results are paginated, using FR-2 through FR-5 defaults. | No nonmatching product appears; page changes preserve criteria. |
| **RULE-6** | Selection and page resets follow FR-6. | Bulk actions never include products from another page or previous criteria. |
| **RULE-7** | Status confirmation and visibility follow FR-9 and FR-10. | Cancelled deactivation changes nothing; never discard product details or order history through status changes. |
| **RULE-8** | Deleting a product removes its exclusively owned images, matching Brands/Categories cleanup policy. | Images still used by another record remain; never remove another record's content. |
| **RULE-9** | Displayed range and count use all FR-17 variants; range uses current selling prices, including applicable sales. Equal prices collapse to one amount. | Exclude deleted variants; include inactive/out-of-stock variants. Show count beneath name, with singular/plural wording. |
| **RULE-10** | At least one actual FR-17 variant price must meet both supplied price bounds, endpoints included. | Exclude product when only its overall range overlaps filter. Filtering never narrows displayed range. |
| **RULE-11** | Both price sort directions use lowest FR-17 variant price across product's full variant set. | Never substitute highest price or lowest matching price when filter changes. |

## 5. Edge Cases & Error Handling

FR-16 recovery states apply below except deletion outcomes already defined by FR-11 through FR-13.

- **Edge case:** Products still loading. **User experience:** Loading indication; no false empty-result message.
- **Edge case:** No products or no matches. **User experience:** Distinct explanation; no-match state offers clearing search and filters.
- **Edge case:** Deletion empties last page. **User experience:** Show nearest preceding valid page; empty-state message if no results remain.
- **Edge case:** Product already removed. **User experience:** Explain product no longer exists; refresh listing without it.
- **Edge case:** Action fails. **User experience:** Explain failure; display actual status; never report failed changes as successful.
- **Edge case:** Bulk action partly fails. **User experience:** Report changed and unchanged products separately; refresh actual results.
- **Edge case:** Every selected product is referenced. **User experience:** Delete none; report zero deleted, name retained products, show reference counts, offer deactivation.
- **Edge case:** Delete confirmation cancelled or dismissed. **User experience:** Products and selection remain unchanged.
- **Edge case:** Variants cost CAD 10 and CAD 30; price filter is CAD 15–25. **User experience:** Product is excluded; neither actual variant price matches.
- **Edge case:** Every variant has same current selling price. **User experience:** Show single amount, retaining variant count beneath name.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given product-view permission and 11 products, when listing opens, then first 10 appear with FR-1 details, name ascending, and both statuses eligible. Covers FR-1, FR-2, FR-5.
- [ ] **AC-2:** Given matching results span pages, when staff change page, then next results appear with search, filters, and sort retained. Covers FR-2, FR-6.
- [ ] **AC-3:** Given names containing "Mint", when staff search " MINT ", then only containing names appear; blank search restores unrestricted name matching. Covers FR-3.
- [ ] **AC-4:** Given mixed statuses, brands, categories, and prices, when staff combine search with a status, two brands, two categories, and a price minimum and maximum, then every result satisfies all restrictions and matches any chosen brand and any chosen category; removing one restriction retains others; a blank price end stays unbounded. Covers FR-3, FR-4.
- [ ] **AC-5:** Given matching products with distinct names, dates, and prices, when each FR-5 sort is chosen, then results follow that order and retain restrictions.
- [ ] **AC-6:** Given page two and selected products, when search/filter/sort changes, then first page appears and selection clears; changing page also clears selection. Covers FR-6.
- [ ] **AC-7:** Given multiple pages, when staff select all on current page, then selected count equals current-page products and bulk actions target only those products. Covers FR-6.
- [ ] **AC-8:** Given product-create permission, when staff choose Add new product and complete existing flow, then product is created through that flow and appears in subsequent matching listing results. Covers FR-7, FR-8.
- [ ] **AC-9:** Given product-edit permission, when staff choose Edit on a row and save through existing flow, then chosen product opens and subsequent matching listing results show saved changes. Covers FR-7, FR-8.
- [ ] **AC-10:** Given Inactive products and product-edit permission, when staff activate one or a selection, then statuses update without confirmation and changed count appears. Covers FR-9.
- [ ] **AC-11:** Given Active products, when staff deactivate one or a selection, then confirmation names product/count and visibility impact; confirming applies change, cancelling preserves statuses. Covers FR-9.
- [ ] **AC-12:** Given a publicly visible product, when deactivation completes, then product leaves public catalogue but remains listed to staff with details/history intact; reactivation restores eligibility under existing catalogue rules. Covers FR-10.
- [ ] **AC-13:** Given an unreferenced product, when staff delete it, then confirmation names it and states permanence; confirming removes product and exclusively owned images. Shared content remains. Covers FR-11, RULE-8.
- [ ] **AC-14:** Given any single/bulk Delete confirmation, when staff cancel or dismiss it, then no products are deleted and selection remains unchanged. Covers RULE-2.
- [ ] **AC-15:** Given a product referenced by an order of any status or another store record, when deletion is confirmed, then product remains unchanged; reference count and deactivation alternative appear. Covers FR-12.
- [ ] **AC-16:** Given five selected products with two referenced, when bulk Delete is confirmed, then three eligible products disappear; two remain selected and named; deleted/retained counts and reference counts appear. Covers FR-13.
- [ ] **AC-17:** Given only referenced products selected, when bulk Delete is confirmed, then none are deleted; retained products remain selected with counts, names, and deactivation alternative. Covers FR-12, FR-13.
- [ ] **AC-18:** Given absent or revoked product permission, when its capability is attempted, then unauthorized control is absent and direct action is refused without changes; guests receive existing sign-in experience. Covers FR-14, RULE-1.
- [ ] **AC-19:** Given English or French active, when staff use listing/actions, then all interface text and result messages follow language and prices use locale-appropriate CAD format. Covers FR-15.
- [ ] **AC-20:** Given keyboard-only use, when staff operate listing controls and confirmations, then every control is reachable and operable with a visible focus indicator and an accessible name; focus enters the confirmation on open and returns to the listing on dismissal.
- [ ] **AC-21:** Given loading, no products, or no matches, when listing is viewed, then corresponding FR-16 state appears; clearing no-match search/filters restores unrestricted results.
- [ ] **AC-22:** Given last page contains one eligible product, when deletion completes, then nearest preceding valid page appears, or empty state if no results remain. Covers FR-16.
- [ ] **AC-23:** Given missing product, failed action, or partially failed bulk action, when action completes, then message identifies outcome and listing reflects actual results without false success. Covers FR-16.
- [ ] **AC-24:** Given variants priced CAD 10 and CAD 30, with an applicable sale reducing second variant to CAD 25, when listing opens, then price shows CAD 10–25 in active locale format. Covers FR-1, FR-15, FR-17, RULE-9.
- [ ] **AC-25:** Given one variant or several variants all currently priced CAD 10, when listing opens, then price shows one CAD 10 amount; count beneath name reflects actual variant count with correct singular/plural wording. No separate count column appears. Covers FR-1, RULE-9.
- [ ] **AC-26:** Given a product with variant prices CAD 10 and CAD 30, when price filter is CAD 15–25, then product is excluded; when filter is CAD 10–10 or CAD 30–30, then product appears because bounds include endpoints. Covers FR-4, RULE-10.
- [ ] **AC-27:** Given variant prices CAD 10, CAD 20, and CAD 30, when price filter is CAD 15–25, then product appears with full CAD 10–30 range and count of three variants. Covers FR-1, FR-4, RULE-9, RULE-10.
- [ ] **AC-28:** Given product A has variant prices CAD 10 and CAD 100 and product B has CAD 20 and CAD 30, when price filter is CAD 25–100 and each price sort is selected, then ascending places A before B and descending places B before A; option labels identify lowest variant price. Covers FR-5, RULE-11.
- [ ] **AC-29:** Given a product has an active variant priced CAD 20, inactive variant priced CAD 10, out-of-stock variant priced CAD 30, and deleted variant priced CAD 100, when listing displays, filters, or sorts that product, then range is CAD 10–30, count is three, and price sort uses CAD 10; filters CAD 10–10 and CAD 30–30 match, while CAD 100–100 does not. Covers FR-1, FR-4, FR-5, FR-17.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-09-08   ·   **Clarified:** 2026-09-08   ·   **Amended:** 2026-09-08 (variant pricing, filtering, sorting, and count approved)

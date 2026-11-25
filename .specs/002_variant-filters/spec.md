# Variant Filters

**Feature:** `002_variant-filters`

## 1. Problem Statement

Shoppers need products with specific variant options. Staff need those same filters while managing products. Fixed filters miss newly configured options, forcing users to inspect products individually.

**Solution (one line):** Show dynamic, multi-select variant filters on public product listing and Manage Products listing.

### Scope

**In scope:**
- Derive filter groups and choices from existing product variant option types and values, using active variants publicly and active/inactive variants in management.
- Support multiple selected values per group on both listings.
- Combine variant filters with existing listing restrictions, sorting, and pagination.
- Preserve existing public visibility, staff permissions, English text, and accessible controls.

**Out of scope:**
- Add/Edit Product forms or changes to variant creation, pricing, stock, and availability.
- Product detail, cart, checkout, or choosing a purchase variant.
- Filter administration, result counts per choice, and dependent choices that shrink after each selection.
- Changes to existing price matching, price display, or sorting rules.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Guest or signed-in shopper | Filter publicly visible products by variant options | Discover unpublished products or their exclusive option values |
| Staff with product-view permission | Use variant filters in Manage Products, including Active and Inactive products and variants | Gain create, edit, or delete permission through filtering |
| User without product-view permission | Use public listing under public visibility rules | View management results or management-only option values; existing sign-in/access-denied behavior applies |

## 2. Functional Requirements

1. **FR-1:** Both listings show one multi-select filter group per relevant variant option type, labeled with its configured name. Each group shows its configured values without duplicate choices. New option types, such as Size or Flavour, require no separately maintained filter list.
2. **FR-2:** Group choices come from FR-6 eligible variants of products visible on that listing across all result pages, before search, filters, and pagination. Search and filter selections do not narrow available choices. Hide groups with no eligible values. Reflect saved variant additions, edits, activation, deactivation, and removals when listing next loads.
3. **FR-3:** Users can select multiple values in each group. Every selected value remains visibly selected until removed or cleared.
4. **FR-4:** A product matches any selected value within each selected group. Across selected groups, one eligible variant must satisfy every selected group. Separate variants cannot collectively satisfy different groups. An unselected group imposes no restriction.
5. **FR-5:** Products appear once even when several variants match. Products without matching variants are excluded while variant restrictions apply. Products without variants remain eligible when no variant restrictions apply.
6. **FR-6:** Public listing uses only active variants for filter choices and matching. Manage Products uses both active and inactive variants for filter choices and matching. A publicly visible product cannot match variant filters through an inactive variant. Values exclusive to inactive variants remain hidden publicly. Deleted products and deleted variants contribute neither choices nor matches on either listing. Existing product visibility and stock behavior remain unchanged.
7. **FR-7:** Variant restrictions combine with every applicable existing restriction: public brand/category/price filters and management search/status/brand/category/price filters. Existing sorting and price rules continue unchanged.
8. **FR-8:** Selecting or removing a value refreshes matching products and returns to first page. Paging retains selections and sort. Refresh, shared listing links, and browser Back/Forward retain variant selections alongside existing listing criteria. Management selection resets follow existing listing behavior.
9. **FR-9:** Users can remove one selected value without removing others. Removing a group's final value removes that group's restriction. Existing Clear filters action also clears variant selections under that listing's existing clearing behavior.
10. **FR-10:** No matches shows existing no-match experience and allows clearing filters. Failed loading shows a recoverable error without claiming no products match. Ignore unknown or removed option selections, explain their removal, and retain other valid criteria.
11. **FR-11:** Both listings preserve existing access restrictions. Controls expose group/value names, selected state, keyboard operation, and visible focus. Interface labels and messages appear in English; configured option names remain merchant-entered content.

## 3. Functional Behaviors

### Behavior 1: Discover available options
- **User does:** Opens either listing.
- **User sees:** Variant groups derived from products permitted on that listing, including choices represented beyond first page. Public choices use only active variants; management choices include active and inactive variants.

### Behavior 2: Combine selections
- **User does:** Selects Red and Blue under Colour, then Large under Size.
- **User sees:** Products with at least one eligible Red/Large or Blue/Large variant, once per product, subject to existing restrictions and FR-6 variant eligibility.

### Behavior 3: Refine or clear
- **User does:** Removes one choice, removes group's final choice, or clears filters.
- **User sees:** Remaining selections persist; corresponding restrictions disappear; matching first-page results update.

### Behavior 4: Navigate filtered results
- **User does:** Changes page, refreshes, opens shared listing link, or uses browser Back/Forward.
- **User sees:** Relevant variant selections restored with existing criteria; results satisfy those selections.

### Behavior 5: Recover from missing choices or loading failure
- **User does:** Opens outdated filtered link or encounters failed loading.
- **User sees:** Removed selections explained while valid criteria remain; failed loading offers retry without a false no-match result.

## 4. Constraints

- Product Catalogue, Manage Product, and Role-Based Access Control retain their existing responsibilities and access rules.
- Filtering changes product inclusion only; product cards, management rows, full price ranges, variant counts, and purchase actions retain existing behavior.
- Saved variant changes appear on next listing load; live updates while another user edits are outside scope.
- No new permission is required beyond existing listing access.
- Both desktop and mobile filter presentations expose same groups, choices, and selected state.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Choices and results derive only from products actor may view on that listing. | Hide inaccessible products and option values exclusive to those products; refuse unauthorized management access. |
| **RULE-2** | Choices cover visibility-eligible products before current criteria and pagination; exclude deleted records. | Never omit a valid choice solely because its product appears on another page or current filters produce no matches. |
| **RULE-3** | Match any selected value per group; one eligible variant satisfies all selected groups. | Exclude products whose separate variants collectively satisfy groups but no single variant does. |
| **RULE-4** | Apply variant restrictions together with existing criteria before pagination; show each matching product once. | Exclude products failing any restriction; never duplicate products for multiple matching variants. |
| **RULE-5** | Changing variant selections returns to first page; paging retains criteria; management row selection follows existing resets. | Never leave an invalid current page or retain products selected under previous criteria. |
| **RULE-6** | Empty selected group imposes no restriction; clearing filters includes variant selections. | Never leave hidden variant restrictions after their selections are cleared. |
| **RULE-7** | Unknown or removed selections cannot match unrelated choices. | Discard invalid selections with explanation; retain valid selections and existing criteria. |
| **RULE-8** | Public choices and matches use active variants only; management choices and matches include active and inactive variants. Deleted records never contribute. | Hide values exclusive to inactive variants publicly; never include a public product solely because its inactive variant matches. Preserve authorized management discovery of inactive variants. |

## 5. Edge Cases & Error Handling

- **Edge case:** No eligible variants exist. **User experience:** No variant groups; existing listing and filters remain usable.
- **Edge case:** An option value exists only on inactive variants. **User experience:** Hide value publicly; offer it to authorized staff in Manage Products.
- **Edge case:** A public product's only matching variant is inactive. **User experience:** Exclude product from filtered public results; authorized management results may include it.
- **Edge case:** Product has no variants. **User experience:** Existing visibility applies without variant restrictions; active variant restrictions exclude product.
- **Edge case:** Several variants match. **User experience:** Product appears once with existing card/row details.
- **Edge case:** Only Red/Small and Blue/Large variants exist; user selects Red and Large. **User experience:** Product does not match.
- **Edge case:** Selections match no products. **User experience:** No-match message; selected values remain removable; Clear filters remains available.
- **Edge case:** A selected option is removed before next load. **User experience:** Explain discarded selection; preserve valid criteria and recalculate results.
- **Edge case:** Variant choices or results fail to load. **User experience:** Recoverable error with retry; never present failure as empty catalogue or successful filtering.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given both listings contain products with Colour and Size variants, when either listing opens, then named multi-select groups show configured values without duplicate choices. Covers FR-1, FR-3.
- [ ] **AC-2:** Given eligible variant value exists only on a later product page, when first page opens or search/filter criteria change, then value remains offered. Covers FR-2, RULE-2.
- [ ] **AC-3:** Given staff save new option type/value or remove its final eligible use, when listing next loads, then choices reflect saved configuration and empty groups disappear. Covers FR-1, FR-2.
- [ ] **AC-4:** Given products with Red, Blue, and Green variants, when user selects Red and Blue, then products matching either selected value appear and both choices remain selected. Covers FR-3, FR-4.
- [ ] **AC-5:** Given products with Red/Large, Red/Small, and Blue/Large variants, when user selects Red and Large, then only products with a Red/Large variant match; separate Red/Small and Blue/Large variants do not qualify. Covers FR-4, RULE-3.
- [ ] **AC-6:** Given several matching variants on one product and another product without variants, when variant filters apply, then matching product appears once and product without variants is excluded; removing all variant restrictions restores its existing eligibility. Covers FR-5.
- [ ] **AC-7:** Given visible products with active Red, inactive Blue, and deleted Green variants, with no other uses of those values, when both listings load and users filter by offered values, then public offers and matches Red only, management offers and matches Red and Blue, and neither offers or matches Green. Covers FR-6, RULE-8.
- [ ] **AC-8:** Given existing brand/category/price restrictions and sort on public listing, when user selects variant values, then every result meets all restrictions, retains existing order and price behavior, and pagination counts only matching products. Covers FR-7, RULE-4.
- [ ] **AC-9:** Given management search/status/brand/category/price restrictions, when authorized staff add variant selections, then every result satisfies all restrictions while full price ranges and variant counts retain existing behavior. Covers FR-7.
- [ ] **AC-10:** Given later page and selected management rows, when variant selection changes, then first matching page appears and row selection clears; changing page retains variant criteria and sort. Covers FR-8, RULE-5.
- [ ] **AC-11:** Given multiple variant selections, when user removes one value or group's final value, then other selections remain and only corresponding restriction changes. Covers FR-9, RULE-6.
- [ ] **AC-12:** Given active variant and existing filters with zero matches, when user views results then clears filters, then no-match guidance and clear action remain available, variant selections clear, and results follow existing clearing behavior. Covers FR-9, FR-10.
- [ ] **AC-13:** Given valid selected variant values and existing listing criteria, when user refreshes, opens shared listing link, or uses Back/Forward, then corresponding criteria and matching results return. Covers FR-8.
- [ ] **AC-14:** Given no eligible variants, when either listing opens, then variant groups are absent and existing listing controls remain usable. Covers FR-1, FR-2.
- [ ] **AC-15:** Given unpublished product with exclusive option value, when guest or signed-in shopper uses public listing, then neither product nor exclusive value appears; authorized staff can filter it in management, while users lacking product-view permission receive existing sign-in/access-denied behavior. Covers FR-11, RULE-1.
- [ ] **AC-16:** Given outdated link containing valid and removed or unknown values, when listing loads, then invalid values are discarded with explanation, valid criteria remain, and no unrelated choice becomes selected. Covers FR-10, RULE-7.
- [ ] **AC-17:** Given variant choices or results cannot load, when user opens or filters listing, then recoverable error and retry appear without false no-match result. Covers FR-10.
- [ ] **AC-18:** Given desktop/mobile views and keyboard or screen-reader use, when user operates variant filters, then group/value names and selected states are available, controls work with visible focus, and both views expose same selections; interface text appears in English. Covers FR-11.
- [ ] **AC-19:** Given public product A has active Red and inactive Blue variants and public product B has active Blue, when Blue is selected, then public results include B and exclude A, while authorized management results include both, subject to other criteria. Covers FR-6, RULE-8.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-10-22   ·   **Clarified:** 2026-10-22

# Manage Fresh Drops

**Feature:** `004_fresh-drops`

## 1. Problem Statement

Homepage staff need to choose which products Fresh Drops features and control its heading, button, and visibility. Without an editor, staff cannot refresh this section when products change. Unauthorized users must not change homepage content.

**Solution (one line):** Let Homepage permission holders add or edit one Fresh Drops configuration through administration.

### Scope

**In scope:**

- One Fresh Drops configuration: Add when absent; Edit when present.
- Choose the three newest eligible products automatically or select and order up to three products manually.
- Search and select products, remove selections, change manual order, and inspect current automatic choices.
- Edit Title, Button label, Button link, and Visible/Hidden status.
- Save or cancel changes; reopen saved configuration.
- Desktop, tablet, and phone administration views from supplied Figma design, including loading and empty search states.
- Existing Homepage View, Create, and Edit permissions.

**Out of scope:**

- Public homepage rendering, shopper navigation, and product card behavior. User confirmed admin editor only.
- Multiple Fresh Drops sections, scheduling, campaign history, deletion, and permission administration.
- Product creation, inventory changes, product publishing, and editing product details from this editor.
- Editing images, brands, prices, or stock through Fresh Drops.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Staff with Homepage View and Create | Open management; add Fresh Drops when absent. | Change existing configuration without Homepage Edit. |
| Staff with Homepage View and Edit | Open management; edit existing configuration. | Add absent configuration without Homepage Create. |
| Staff with Homepage View only | Inspect saved configuration or absent state. | See mutation controls or save changes. |
| Signed-in user without Homepage View | See access-denied guidance. | Read management content or change configuration. |
| Guest | Reach sign-in when requesting management. | Open management before authentication. |

Permissions combine. Role name alone grants no access.

## 2. Functional Requirements

1. **FR-1:** Expose Fresh Drops from Homepage administration. Show Admin Console / Homepage / Fresh Drops breadcrumbs. Show Add or Edit followed by the current Title field value in the page heading; use `Fresh Drops` while Title has no non-whitespace text.
2. **FR-2:** Require Homepage View for management, Homepage Create for initial Save, and Homepage Edit for later Save. Hide unauthorized mutation controls and deny unauthorized changes.
3. **FR-3:** Offer mutually exclusive Newest automatically and Pick manually modes. Add starts in Newest automatically; switching to Pick manually starts with no products selected. Edit loads saved mode and values.
4. **FR-4:** In automatic mode, show the three newest published products. Choices update as products are published or unpublished. Staff cannot reorder automatic choices.
5. **FR-5:** In manual mode, let staff select up to three distinct products, view selected count, remove products, and reorder selections by drag or arrow controls. Unpublished selections are skipped when the configuration is used.
6. **FR-6:** Open Add Products dialog from manual mode while fewer than three products are selected. Search by product or brand name. Show product image, name, brand, and price; show selected count and a helpful no-results state. Add Products applies dialog selection to the draft; Cancel or Close discards dialog changes.
7. **FR-7:** Let staff edit plain Title, Button label, and Button link. Add pre-fills `Fresh Drops`, `View All Products`, and `/products`, respectively. Title is required. Staff may clear both button fields to save no button configuration; one field alone is invalid.
8. **FR-8:** Accept a site-relative Button link beginning with one `/` or an absolute HTTP/HTTPS URL. Reject unsafe or malformed destinations. This editor saves the link; it does not provide shopper navigation.
9. **FR-9:** Offer mutually exclusive Visible and Hidden status. Add starts Visible. Hidden configuration remains editable and occupies the sole Fresh Drops position.
10. **FR-10:** Save all draft settings as one change. Show success only after Save succeeds, return to Homepage administration, and show saved values when reopened. A failed Save retains the draft and previous saved configuration.
11. **FR-11:** Cancel discards draft changes and returns to Homepage administration. Cancelled Add creates nothing.
12. **FR-12:** Match supplied desktop, tablet, and phone administration layouts. Keep controls keyboard operable with visible focus, accessible names, field errors, and English text.

## 3. Functional Behaviors

### Behavior 1: Open editor

- **User does:** Opens Fresh Drops from Homepage administration or its direct address.
- **User sees:** Add with automatic mode and pre-filled text, Edit with saved values, or view-only state according to permissions. Page heading begins Add or Edit and shows current Title. Guest sees sign-in; signed-in user without Homepage View sees access denied. Loading shows a temporary loading state, never an Add form.

### Behavior 2: Choose automatic products

- **User does:** Selects Newest automatically.
- **User sees:** Current three newest eligible products and guidance that choices update automatically. Manual Add Product and reorder controls are unavailable. Switching back to Pick manually restores unsaved manual selections.

### Behavior 3: Choose manual products

- **User does:** Opens Add Products, searches, selects products, confirms, removes a product, or changes order.
- **User sees:** Search results or no-results guidance, selected count, and draft rows in chosen order. Selection stops at three; duplicate products cannot appear.

### Behavior 4: Edit content and status

- **User does:** Changes Title, Button label, Button link, or Visible/Hidden status.
- **User sees:** Draft values and selected status. Page heading follows Title as staff type; Title without non-whitespace text shows `Fresh Drops` in heading and still blocks Save. Saved configuration remains unchanged until Save.

### Behavior 5: Save or cancel

- **User does:** Selects Save or Cancel.
- **User sees:** Successful Save confirmation and Homepage administration. Invalid input or failed Save leaves draft available for correction. Cancel returns without changing saved configuration.

## 4. Constraints

- Design source: [Fresh Drops section, node 3185:56956](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3185-56956&t=cWBtc9BODDtj4EUK-11).
- Inspected frames: Edit desktop `3169:36411`, tablet `3169:41046`, phone `3169:41783`; Add desktop `3183:53681`, tablet `3183:53744`, phone `3183:53807`; automatic desktop `3183:53870`; product dialog desktop `3183:56126` and no-results desktop `3183:56477`; loading frames under node `3185:56956`.
- User-confirmed Add defaults override the Figma Add frames: Newest automatically, Title `Fresh Drops`, Button label `View All Products`, and Button link `/products`. Staff may change all three fields.
- User confirmed published products are eligible. Stock is not tracked; the Figma copy about in-stock products does not define eligibility.
- The design describes product selection in administration. Public homepage rendering is outside this feature.
- User-facing text is English. Selection and status must remain usable with keyboard and assistive technology.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Homepage View is required to read management content. Homepage Create permits initial Save; Homepage Edit permits later Save. Create/Edit alone does not grant View. | Deny access or change; do not expose management content or mutation controls. |
| **RULE-2** | At most one Fresh Drops configuration exists, including when Hidden. | Reject second Add; offer reload of existing configuration. |
| **RULE-3** | Manual selection contains at most three distinct products. Order is explicit. | Block fourth or duplicate selection; preserve existing draft order. |
| **RULE-4** | Automatic choices are the three newest published products. Unpublished products are skipped in either mode. Stock does not affect eligibility. | Omit unpublished products from current choices; do not replace manually selected products automatically. |
| **RULE-5** | Title must contain non-whitespace text. Button label and link are both blank or both nonblank. | Reject Save; identify missing or incomplete field. |
| **RULE-6** | Button link is a valid root-relative path beginning with one `/`, or absolute HTTP/HTTPS URL. Protocol-relative, script/data, and backslash-based destinations are invalid. | Reject Save; explain accepted destination formats. |
| **RULE-7** | Status is Visible or Hidden. Only successful Save changes saved configuration. | Reject invalid status or failed Save; retain previous saved values. |

## 5. Edge Cases & Error Handling

- **Edge case:** No saved configuration. **User experience:** Authorized creator sees Add with automatic mode and pre-filled text; view-only staff see absent state.
- **Edge case:** Hidden configuration exists. **User experience:** Authorized editor sees Edit; Add cannot create another.
- **Edge case:** Fewer than three eligible products exist. **User experience:** Automatic mode shows available choices without filler products; manual mode skips ineligible selections.
- **Edge case:** Manual selection becomes unpublished after Save. **User experience:** Saved choice remains editable but is skipped when used; other manual choices keep their order.
- **Edge case:** Product search finds nothing. **User experience:** Dialog says no products found and suggests another product or brand name; existing selections remain.
- **Edge case:** Staff tries fourth or duplicate manual selection. **User experience:** Action is unavailable or rejected; three distinct selections remain.
- **Edge case:** Title is cleared. **User experience:** Page heading falls back to Add Fresh Drops or Edit Fresh Drops; Save identifies required Title and retains draft.
- **Edge case:** Button fields are invalid. **User experience:** Save identifies field errors and retains draft.
- **Edge case:** Management loading fails. **User experience:** Error and retry appear; failure does not masquerade as absent configuration.
- **Edge case:** Save fails, permissions change, or another staff member creates Fresh Drops first. **User experience:** Clear denial or conflict appears; previous saved configuration remains unchanged and draft stays available where permitted.
- **Edge case:** Staff activates Save repeatedly. **User experience:** One Save proceeds; no duplicate configuration appears.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given no Fresh Drops configuration and Homepage View/Create, when staff open management, then Add Fresh Drops starts in Newest automatically, shows up to three newest eligible products, pre-fills Title `Fresh Drops`, Button label `View All Products`, and Button link `/products`, and shows Visible status, Save, and Cancel. When staff change Title, then Add heading follows its current value. Staff can edit all three fields; switching to Pick manually starts with zero selections. Covers FR-1 through FR-4, FR-7, and FR-9.
- [ ] **AC-2:** Given saved configuration and Homepage View/Edit, when staff open management, then Edit heading shows saved Title and editor shows saved mode, selected or automatic products, text, and status. When staff change Title, then Edit heading follows its current value. Covers FR-1 through FR-3.
- [ ] **AC-3:** Given guest or signed-in user without Homepage View, when management is requested directly or through navigation, then guest reaches sign-in and signed-in user sees access denied without management content. Covers RULE-1.
- [ ] **AC-4:** Given Homepage View without relevant Create/Edit permission, when staff inspect absent or existing configuration and attempt Save, then mutation controls are absent and changes are denied. Create/Edit without View also denies management. Covers FR-2 and RULE-1.
- [ ] **AC-5:** Given automatic mode and published products, when a newer product is published or a current choice is unpublished, then current choices show up to three newest published products without manual reorder controls. Stock does not affect eligibility. Covers FR-4 and RULE-4.
- [ ] **AC-6:** Given manual mode and fewer than three selections, when staff search by product or brand, choose distinct products, and confirm dialog, then selected count and rows update. Cancel or Close leaves draft unchanged. Switching to automatic mode and back restores unsaved manual selections. Covers FR-3, FR-5, and FR-6.
- [ ] **AC-7:** Given three manual selections or an already selected product, when staff try another or duplicate selection, then limit or duplicate guidance prevents it. Given selected rows, when staff reorder or remove one, then draft order and count update. Covers RULE-3.
- [ ] **AC-8:** Given search with no matching product, when results load, then dialog shows no-results guidance without clearing current selections. Covers FR-6.
- [ ] **AC-9:** Given blank Title or only one button field populated, when Save is selected, then field guidance appears and saved configuration stays unchanged. Blank Title keeps `Fresh Drops` in Add/Edit heading. Given both button fields blank, when Save is selected with other valid settings, then no button configuration is saved. Covers FR-1, FR-7, and RULE-5.
- [ ] **AC-10:** Given paired button fields, when staff enter a valid root-relative or HTTP/HTTPS destination and Save, then link persists. Given unsafe or malformed destination, when Save is selected, then field guidance appears. Covers FR-8 and RULE-6.
- [ ] **AC-11:** Given valid draft and required permission, when staff Save and reopen management, then mode, manual order where applicable, text, link, and Visible/Hidden status persist. Failed Save retains prior saved values. Covers FR-9, FR-10, and RULE-7.
- [ ] **AC-12:** Given unsaved Add or Edit changes, when staff Cancel, then Homepage administration opens and saved configuration remains unchanged. Covers FR-11.
- [ ] **AC-13:** Given existing or Hidden configuration, when another Add is attempted, then no second Fresh Drops configuration appears and staff can reload the existing one. Covers RULE-2.
- [ ] **AC-14:** Given loading failure, Save failure, or permission loss, when staff act, then clear retry, error, or denial appears; failed load never shows Add as though no configuration exists. Covers FR-2 and FR-10.
- [ ] **AC-15:** Given desktop, tablet, and phone views, when staff use keyboard or assistive technology, then layout, picker, reorder controls, status choices, labels, focus, and errors remain usable without horizontal overflow. Covers FR-12.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-10-30   ·   **Clarified:** 2026-10-30

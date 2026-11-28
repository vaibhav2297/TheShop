# Homepage Shop by Brand

**Feature:** `008_homepage-brand`

## 1. Problem Statement

Store staff need to choose which brands appear in the homepage Shop by Brand section and control their order and message. Brand records alone do not give staff a way to curate this section. Without an editor, the homepage selection cannot follow current merchandising priorities.

**Solution (one line):** Let authorized staff add or edit one Shop by Brand configuration with an ordered brand selection, section text, destination, and visibility.

### Scope

**In scope:**
- Add one Shop by Brand configuration when absent; edit it when present.
- Start Add with no selected brands, then select, remove, and reorder up to six active brands; show selection count and brand identity.
- Search and select active brands through the Add Brands dialog, including loading and no-results states.
- Edit section title, button label, button link, and Visible/Hidden status; save or cancel changes.
- Loading, empty, validation, and failure states across desktop, tablet, and phone admin layouts.

**Out of scope:**
- Creating, editing, deleting, or changing status or logos of brand records.
- A second Shop by Brand configuration, section deletion, or changes to other homepage sections.
- Public homepage rendering, shopper brand navigation, and the public brands page.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Staff with Homepage View and Create | Open Homepage management and add the section when absent. | Edit an existing section without Homepage Edit. |
| Staff with Homepage View and Edit | Open Homepage management and edit the existing section. | Add a section when absent without Homepage Create. |
| Staff with Homepage View only | Inspect the saved or absent section. | Change or save the section. |
| Signed-in user without Homepage View | See access-denied guidance. | Read management content or change the section. |
| Guest | Reach sign-in when requesting management. | Read management content before authentication. |

Permissions combine. Create or Edit alone does not grant management access.

## 2. Functional Requirements

1. **FR-1:** Homepage management offers Add when no Shop by Brand configuration exists and Edit when one exists. The editor shows Admin Console / Homepage / Shop by Brand breadcrumbs and an Add Shop by Brand or Edit Shop by Brand heading.
2. **FR-2:** Add starts with no selected brands and `0 of 6 selected`. Title defaults to `Shop by Brand`, Button label to `View All Brands`, Button link to `/brands`, and status to Visible. Staff can change these defaults. Edit shows saved values and brand order.
3. **FR-3:** Staff can select distinct active brands, remove selections, and change their order. The count updates after each change. At six selections, Add Brand is unavailable until one is removed.
4. **FR-4:** Each selected row shows the brand name and its one-colour logo when available. Without a logo, the brand name remains visible. Order controls show when movement is unavailable at list boundaries.
5. **FR-5:** Staff can change the section Title, Button label, Button link, and Visible/Hidden status. Save persists these fields and the brand order together; Cancel leaves the saved configuration unchanged.
6. **FR-6:** Pending Edit loading shows the supplied skeleton state. Load failure offers retry. Invalid or failed Save keeps entered values available for correction and does not claim success.
7. **FR-7:** The editor remains usable on desktop, tablet, and phone. Fields and actions use English text, accessible names, visible keyboard focus, and keyboard-operable selection and ordering.
8. **FR-8:** Add Brand opens an Add Brands dialog showing only Active brands. Inactive brands are absent from the cards and search results. Staff can search Active brands by name, select or deselect cards, inspect the selected count, then apply choices with Add Brands. Cancel or Close leaves the editor draft unchanged. The dialog shows skeleton cards while brands load, a clear no-results message for an unmatched search, and retry guidance if loading fails.

## 3. Functional Behaviors

### Behavior 1: Open the editor
- **User does:** Staff open Shop by Brand from Homepage management.
- **User sees:** Loading placeholders, then Add or Edit according to saved state and permissions. Add shows no selected brands and prefilled, editable section fields. Edit shows saved fields and ordered brands. Unauthorized users see sign-in or access-denied guidance.

### Behavior 2: Curate brands
- **User does:** Staff add, remove, or reorder brands.
- **User sees:** Brand names, available logos, order, and selected count update in the draft. A full list cannot accept another brand.

### Behavior 3: Choose brands in the dialog
- **User does:** Staff open Add Brand, search, select cards, then choose Add Brands, Cancel, or Close.
- **User sees:** Active brand cards and the staged selected count. Inactive brands never appear, including in search. No match shows guidance. Add Brands updates the editor draft. Cancel or Close discards dialog-only choices.

### Behavior 4: Configure and finish
- **User does:** Staff edit section fields or status, then choose Save or Cancel.
- **User sees:** Successful Save returns to Homepage management; reopening Edit shows saved values. Cancel returns without applying draft changes. Failed Save explains the issue and retains the draft.

## 4. Constraints

- Design source: [Shop by Brand, node 3185:56959](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3185-56959&t=QoPhLgu2hu5FaRIQ-11).
- Design covers Add, Edit, Edit loading, and Add Brands dialog states at desktop, tablet, and phone sizes. The editor uses English text only.
- Brand rows, dialog cards and search, ordering controls, status choices, Save, Cancel, and validation guidance must work by keyboard and remain readable at phone width and increased zoom.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Only one Shop by Brand configuration exists. Add is available only while absent. | A second Add is refused and staff are directed to Edit. |
| **RULE-2** | Visible requires one to six distinct active brands. Hidden allows zero to six distinct active brands. | Save identifies missing brands when Visible, or duplicate, inactive, or excess selections at either status; Add Brand stops at six. |
| **RULE-3** | Title, Button label, and Button link contain nonblank text after surrounding spaces are ignored, for Visible and Hidden. | Save identifies each missing field and retains the draft. |
| **RULE-4** | Button link is a safe site-relative path or absolute HTTP/HTTPS address. | Save rejects an unsafe or malformed destination with field guidance. |
| **RULE-5** | Save applies brand order, section fields, and Visible/Hidden status as one change. | A failed Save changes none of the previously saved values. |
| **RULE-6** | Homepage View plus Create or Edit permission is required for the corresponding change. | Controls are hidden without permission; direct change attempts are denied. |
| **RULE-7** | Dialog choices change the editor draft only after Add Brands is chosen; Cancel or Close discards dialog-only choices. | Dismissal leaves the editor selection unchanged. |
| **RULE-8** | The Add Brands dialog shows only Active brands, including search results. | Inactive brands are not displayed or selectable. |

## 5. Edge Cases & Error Handling

- **Edge case:** No brand is selected. **User experience:** Add shows `0 of 6 selected`; Save requires a brand when Visible but allows none when Hidden.
- **Edge case:** Six brands are selected. **User experience:** Add Brand is unavailable until a brand is removed.
- **Edge case:** Search matches no active brand. **User experience:** The dialog shows `No brands found` and prompts another name; staged choices remain.
- **Edge case:** Available brands are loading or fail to load. **User experience:** Dialog cards show skeletons while loading; failure offers retry without changing the editor draft.
- **Edge case:** Staff search for an Inactive brand. **User experience:** The dialog shows no matching brand and offers another search.
- **Edge case:** A selected brand becomes inactive or unavailable before Save. **User experience:** Save identifies it for replacement and retains other draft changes.
- **Edge case:** A selected brand has no logo. **User experience:** Its name identifies the row.
- **Edge case:** Section data cannot load. **User experience:** An error offers retry; no empty Add form replaces the unknown saved state.
- **Edge case:** Staff cancel or Save fails. **User experience:** Cancel preserves saved values; failed Save retains the draft and explains the problem.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given no Shop by Brand configuration, when authorized staff open Add, then no brands are selected and the count reads `0 of 6 selected`; Title shows `Shop by Brand`, Button label `View All Brands`, Button link `/brands`, and status Visible; staff can change each value (FR-2).
- [ ] **AC-2:** Given a saved configuration, when authorized staff open Edit, then saved fields, status, selected brands, and order appear (FR-2, RULE-5).
- [ ] **AC-3:** Given fewer than six selected brands, when staff add a distinct active brand, then it appears once with its name, available one-colour logo, and updated count (FR-3, FR-4, RULE-2).
- [ ] **AC-4:** Given six selected brands, when staff attempt another selection, then Add Brand is unavailable until one is removed (RULE-2).
- [ ] **AC-5:** Given a selected brand at the first or last position, when staff use ordering controls, then unavailable movement is disabled; valid movement updates draft order and persists after Save (FR-3, RULE-5).
- [ ] **AC-6:** Given no selected brand, when staff Save as Visible, then Save requires a brand; when staff Save as Hidden with valid fields, then zero brands are allowed. Given a duplicate or inactive selection at either status, when staff Save, then Save identifies it and leaves saved values unchanged (RULE-2).
- [ ] **AC-7:** Given a blank required field or unsafe Button link, when staff attempt Save, then the field is identified, the draft remains, and saved values stay unchanged (RULE-3, RULE-4).
- [ ] **AC-8:** Given a valid draft, when authorized staff Save and reopen Edit, then all brand, text, link, and status changes persist together; Hidden remains editable (RULE-5).
- [ ] **AC-9:** Given unsaved Add or Edit changes, when staff choose Cancel, then Homepage management opens and no draft change persists (FR-5).
- [ ] **AC-10:** Given pending loading or failed loading, when staff open Edit, then skeletons appear while pending and retry guidance appears on failure without premature Add state (FR-6).
- [ ] **AC-11:** Given a guest or staff without required Homepage permission, when management or Save is attempted, then sign-in or access-denied guidance appears and no unauthorized change occurs (RULE-6).
- [ ] **AC-12:** Given desktop, tablet, or phone width and keyboard use, when staff navigate the editor, then all fields, brand actions, status, Save, and Cancel remain readable, named, and operable without horizontal page overflow (FR-7).
- [ ] **AC-13:** Given Active and Inactive brands exist, when Add Brand opens or staff search by name, then only Active brand cards appear; searching for an Inactive brand shows no result. Matching Active cards show logo or name, and selection changes the visible count (FR-8, RULE-8).
- [ ] **AC-14:** Given dialog choices differ from the editor draft, when staff choose Add Brands, then the editor selection updates; when staff instead choose Cancel or Close, then the editor selection stays unchanged (RULE-7).
- [ ] **AC-15:** Given an unmatched search or pending brand load, when staff view the dialog, then they see the no-results guidance or skeleton cards respectively; load failure offers retry without changing the editor draft (FR-8).

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-11-25   ·   **Clarified:** 2026-11-25

# Add Brand

## 1. Problem Statement

The Shop's product range is organized by brand: shoppers filter the catalogue by brand, and every product belongs to one. Today there is no way for the store's staff to create a brand, so new suppliers and product lines cannot be represented in the store at all — products can't be assigned to them and customers can't find them. Without this capability, growing the catalogue stalls at the very first step of onboarding a new brand.

**Solution (one line):** Staff with the right permission can add a new brand to the store by providing its name, an optional logo, an optional description, and an active/inactive status.

### Scope

**In scope:**
- Adding a single new brand from the admin panel: brand name (required), logo image (optional), description (optional), and status — Active or Inactive.
- Validation of the entered brand details, with clear messages when something is wrong.
- Making a newly added Active brand available wherever brands appear in the store (e.g., assigning products to it, the catalogue's brand filter).

**Out of scope:**
- Editing, deleting, deactivating/reactivating, or merging existing brands — separate future features.
- Browsing, searching, or managing the brand list itself (the manage-brands page) — this feature only returns the staff member there after a successful save.
- Assigning products to a brand (delivered by product management features).
- Any customer-facing brand pages or brand marketing content.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Staff member with the brand-creation permission | Open the add-brand form, enter details, and create the brand | — |
| Staff member without that permission | — | See or reach the add-brand capability; a direct attempt is refused with the store's standard access-denied experience |
| Guest / signed-in customer | — | See or reach any part of this feature |

Adding brands is governed by a specific fine-grained permission, consistent with the store's access model. By default the Admin and Super Admin roles hold this permission.

## 2. Functional Requirements

1. **FR-1:** A staff member with the required permission can add a new brand from the admin panel.
2. **FR-2:** Creating a brand requires a brand name; a brand cannot be created without one.
3. **FR-3:** The staff member may optionally attach a logo image to the brand; a brand can be created without a logo.
4. **FR-4:** The staff member may optionally provide a description for the brand; a brand can be created without a description.
5. **FR-5:** The staff member chooses the brand's status — Active or Inactive — when creating it; the status defaults to Active.
6. **FR-6:** After a successful save, the staff member sees a confirmation and is returned to the manage-brands page, and an Active brand becomes available wherever brands are used in the store (such as product assignment and the catalogue's brand filter).
7. **FR-7:** An Inactive brand is not shown to customers anywhere in the store (including the catalogue's brand filter) and is not offered when assigning products to a brand; it remains visible to staff in the admin panel's brand list.
8. **FR-8:** Invalid input is rejected with a clear, field-specific message, and everything the staff member has already entered is preserved so they can correct and resubmit.
9. **FR-9:** All text in this feature — labels, buttons, validation messages, confirmations, and access-denied messages — is available in English and French and follows the active site language.

## 3. Functional Behaviors

### Behavior 1: Add a brand successfully
- **User does:** A permitted staff member opens the add-brand form, enters a brand name (optionally adding a logo, a description, and choosing a status), and saves.
- **User sees:** A confirmation that the brand was created, and they are returned to the manage-brands page; the new brand now exists in the store, and if Active it is available wherever brands are used.

### Behavior 2: Attach a logo
- **User does:** Selects an image file as the brand's logo before saving.
- **User sees:** A preview of the chosen logo on the form, with the option to remove or replace it before saving.

### Behavior 3: Submit invalid details
- **User does:** Tries to save with a missing name, a name already in use, or an unacceptable logo file.
- **User sees:** The save is refused with a clear message next to the offending field; everything else they entered stays in place for correction.

### Behavior 4: Attempt without permission
- **User does:** A user without the brand-creation permission tries to reach the add-brand capability, including by direct link.
- **User sees:** The store's standard access-denied experience; the capability does not appear in their navigation at all.

## 4. Constraints

- This is an admin-panel capability, gated by a specific fine-grained permission in line with the store's access model — never by an "is an admin" shortcut.
- The brand name and description are stored exactly as the staff member types them and are not translated — brand names are proper nouns, and a single description is entered (no separate English and French versions). All of the feature's own interface text is available in English and French.
- The logo must be an image file of an accepted type and within a size limit: PNG, JPG, and WebP are accepted, up to 2 MB.
- The form is fully operable by keyboard with a visible focus indicator, and validation messages are announced to screen-reader users and clearly associated with their fields.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | A brand name is required and must contain at least one visible character — a name that is empty or only spaces does not count. | The save is rejected with a "name is required" message; no brand is created. |
| **RULE-2** | Brand names must be unique regardless of capitalization and leading/trailing spaces. | The save is rejected with a message that the brand already exists; no brand is created. |
| **RULE-3** | The brand name must not exceed 100 characters, and the description must not exceed 250 characters. | The save is rejected with a message stating the limit for the offending field. |
| **RULE-4** | A logo, when provided, must be an accepted image type and within the size limit. | The file is refused with a message stating the accepted types and size limit; the rest of the form is untouched. |
| **RULE-5** | The brand's status must be either Active or Inactive — there is no other state. | Not selectable: the form only ever offers these two states, defaulting to Active. |

## 5. Edge Cases & Error Handling

- **Edge case:** The staff member enters a name that differs from an existing brand only in capitalization or surrounding spaces → **User experience:** The save is refused with a "brand already exists" message (RULE-2); their input stays on the form.
- **Edge case:** The chosen logo file is not an accepted image type or is too large → **User experience:** The file is refused with a message stating what is accepted; the name, description, and status they entered are preserved.
- **Edge case:** The staff member removes a logo they had selected before saving → **User experience:** The form returns to its no-logo state and the brand is created without a logo.
- **Edge case:** Saving fails for a reason outside the staff member's control (e.g., a connection problem) → **User experience:** A clear error message says the brand was not created and invites them to try again; their input is preserved, and no partial brand appears in the store.
- **Edge case:** A very long name or description is typed → **User experience:** The field indicates the limit and the save is refused until the text fits (RULE-3).
- **Edge case:** A user without the permission follows a direct link to the add-brand capability → **User experience:** The store's standard access-denied experience; nothing about the capability is revealed.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given a staff member with the brand-creation permission on the add-brand form, when they enter a valid brand name and save, then the brand is created, a confirmation is shown, the staff member is returned to the manage-brands page, and the brand is available in the store. Verifies FR-1, FR-2, FR-6.
- [ ] **AC-2:** Given the add-brand form, when it is saved with the name empty or containing only spaces, then the save is refused with a "name is required" message and no brand is created. Verifies FR-2, RULE-1.
- [ ] **AC-3:** Given an existing brand, when a new brand is saved whose name differs only in capitalization or surrounding spaces, then the save is refused with a "brand already exists" message and the entered input is preserved. Verifies RULE-2, FR-8.
- [ ] **AC-4:** Given a valid logo image is attached, when the brand is saved, then the brand is created with that logo, and the logo was previewed on the form before saving. Verifies FR-3, RULE-4.
- [ ] **AC-5:** Given a file that is not an accepted image type or exceeds the size limit, when it is chosen as the logo, then it is refused with a message stating the accepted types and limit, and the rest of the entered input is preserved. Verifies RULE-4, FR-8.
- [ ] **AC-6:** Given only a brand name is entered — no logo, no description — when saved, then the brand is created successfully. Verifies FR-3, FR-4.
- [ ] **AC-7:** Given the status is set to Inactive, when the brand is saved, then the brand exists in the admin panel's brand list but does not appear to customers anywhere in the store and is not offered when assigning products to a brand. Verifies FR-5, FR-7.
- [ ] **AC-8:** Given a user without the brand-creation permission, when they attempt to reach the add-brand capability — including by direct link — then they receive the store's standard access-denied experience. Verifies FR-1 (access boundary).
- [ ] **AC-9:** Given the site language is switched between English and French, when the add-brand form and its messages are viewed, then all of the feature's text follows the active language. Verifies FR-9.
- [ ] **AC-10:** Given a keyboard-only or screen-reader user with the required permission, when they complete the form, then every control is reachable and operable by keyboard with a visible focus indicator, and validation messages are announced and associated with their fields. Verifies the accessibility constraint.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-07-20   ·   **Clarified:** 2026-07-20

<!-- Status lifecycle: "Draft — N open assumption(s)" → "Confirmed" once /theshop.clarify resolves them all (N = 0). -->

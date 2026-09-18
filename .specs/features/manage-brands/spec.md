# Manage Brands

## 1. Problem Statement

The Shop's catalogue is organized by brand, and staff can now create brands — but once created, a brand is frozen. There is no way to see the brands that exist, correct a misspelled name, swap a dated logo, retire a supplier the store no longer carries, or remove a brand that was added by mistake. Staff are left maintaining brand data they cannot see or touch, and the only workaround is creating duplicate brands, which pollutes the customer-facing brand filter.

**Solution (one line):** A permission-gated admin page that lists every brand — searchable, filterable, sortable, and paginated — from which staff can add, edit, activate/deactivate, and delete brands, one at a time or several at once, with deletion refused for any brand still in use.

### Scope

**In scope:**
- A brand list showing each brand's logo, name, description, and status (Active / Inactive), 10 brands per page with pagination.
- Searching brands by name, filtering by status (All / Active / Inactive), and sorting the list.
- Editing an existing brand: name, description, logo, and status — all four are editable.
- Flipping a single brand between Active and Inactive directly from the list, without opening the edit form.
- Selecting several brands from the list and applying one action to all of them: Activate, Deactivate, or Delete.
- Deleting a brand permanently, behind an explicit confirmation, and only when no product references it.
- Independently gating view, add, edit, and delete behind their own fine-grained permissions.
- Retiring the brand's name-derived identifier: a brand is identified only by its own permanent identifier, so renaming a brand can no longer be refused because a derived identifier would clash.

**Out of scope:**
- The add-brand form itself — already delivered by the `add-brand` feature. This page only links to it.
- Reordering brands, and bulk editing of names, descriptions, or logos — Activate, Deactivate, and Delete are the only bulk actions.
- Retiring the equivalent name-derived identifier from categories or anything else that has one — brands only this time.
- Merging two brands into one, or reassigning a brand's products to another brand.
- Any customer-facing brand page, brand marketing content, or changes to how customers filter by brand.
- Assigning products to a brand (delivered by product management features).
- An audit trail / change history of who edited or deleted a brand.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Staff member with the brand-view permission | Open the manage-brands page and browse, search, filter, sort, and page through the brand list | Add, edit, or delete a brand unless they also hold that specific permission — those controls are not shown to them |
| Staff member with the brand-edit permission | Additionally open a brand's edit form and change its name, description, logo, and status; flip a brand's status from the list; and Activate or Deactivate several selected brands at once | — |
| Staff member with the brand-delete permission | Additionally delete a brand after confirming — one brand, or several selected at once — when the brand is not in use | Delete a brand that any product references, whether singly or as part of a selection |
| Staff member with the brand-create permission | Additionally reach the add-brand form from this page | — |
| Staff member without the brand-view permission | — | See or reach the manage-brands page; a direct attempt is refused with the store's standard access-denied experience |
| Guest / signed-in customer | — | See or reach any part of this feature |

Each of the four capabilities — view, add, edit, delete — is governed by its own fine-grained permission, consistent with the store's access model. Holding one does not imply another; a staff member may, for example, view and edit brands but not delete them.

## 2. Functional Requirements

1. **FR-1:** A staff member with the brand-view permission can open the manage-brands page and see the brands that exist, each showing its logo, name, description, and status.
2. **FR-2:** The brand list shows 10 brands per page, with controls to move between pages and an indication of which page is being viewed.
3. **FR-3:** The staff member can search the list by brand name; the list narrows to brands whose name matches, and search combines with the active status filter and sort order.
4. **FR-4:** The staff member can filter the list by status — All, Active only, or Inactive only. The filter opens on All, so both Active and Inactive brands are visible on arrival.
5. **FR-5:** The staff member can sort the list by brand name, ascending or descending; the list opens sorted by name A→Z. Name is the only sort field — grouping by status is done with the status filter instead.
6. **FR-6:** A staff member with the brand-create permission can reach the add-brand form from this page; without that permission the option is not shown.
7. **FR-7:** A staff member with the brand-edit permission can open a brand's edit form pre-filled with its current name, description, logo, and status, change any of the four, and save.
8. **FR-8:** After a successful edit, the staff member sees a confirmation and returns to the brand list, which reflects the change; the updated brand's details appear wherever brands are used in the store.
9. **FR-9:** A brand carries no name-derived identifier. Renaming a brand changes only its name — nothing about how the brand is identified changes, and a rename is never refused because a derived identifier would clash with another brand's.
10. **FR-10:** A staff member with the brand-edit permission can remove a brand's logo, or replace it with a different image, when editing. In both cases the image the brand no longer uses is discarded, so nothing is left stored behind.
11. **FR-11:** Changing a brand's status to Inactive removes it from everywhere customers see brands and from the list of brands products can be assigned to; it remains visible to staff on this page. Changing it back to Active restores it.
12. **FR-12:** A staff member with the brand-delete permission can delete a brand, and must explicitly confirm the deletion before it happens; the confirmation names the brand being deleted.
13. **FR-13:** A brand that any product references cannot be deleted. The attempt is refused with a message stating how many products use the brand, and the staff member is offered the alternative of making the brand Inactive instead.
14. **FR-14:** After a successful deletion, the staff member sees a confirmation, the brand no longer appears in the list or anywhere else in the store, and the brand's logo image is discarded along with it.
15. **FR-15:** Invalid input on the edit form is rejected with a clear, field-specific message, and everything the staff member has already entered is preserved so they can correct and resubmit.
16. **FR-16:** Controls that a staff member lacks permission for are not shown to them at all, and a direct attempt to use such a capability is refused.
17. **FR-17:** All text in this feature — headings, column labels, filter and sort options, buttons, confirmations, validation messages, empty-state text, and access-denied messages — is available in English and French and follows the active site language.
18. **FR-18:** A staff member with the brand-edit permission can flip a single brand between Active and Inactive directly from its row in the list, without opening the edit form. Activating takes effect immediately; deactivating asks for confirmation first, because it hides the brand from customers.
19. **FR-19:** The staff member can select one or more brands in the list, and can select or clear every brand on the page they are viewing at once. The number of selected brands is shown.
20. **FR-20:** With brands selected, a staff member with the brand-edit permission can Activate or Deactivate all of them in one action; deactivating asks for confirmation naming how many brands will be hidden from customers.
21. **FR-21:** With brands selected, a staff member with the brand-delete permission can Delete all of them in one action, after confirming a prompt that states how many brands will be permanently deleted.
22. **FR-22:** A bulk deletion deletes every selected brand no product references and keeps every selected brand that is in use. Afterwards the staff member is told how many brands were deleted and which ones were kept because they are still in use.
23. **FR-23:** The bulk-action controls appear only once at least one brand is selected, and offer only the actions the staff member holds the permission for — a staff member with edit but not delete sees Activate and Deactivate but no Delete.
24. **FR-24:** Each brand's edit form has its own address, so a staff member can bookmark or share a link that opens that specific brand for editing.

## 3. Functional Behaviors

### Behavior 1: Browse the brand list
- **User does:** A staff member with the brand-view permission opens the manage-brands page.
- **User sees:** The first 10 brands, each with its logo, name, description, status, and a way to select it, ordered by name A→Z with the status filter on All, plus pagination controls, a search box, the status filter, and a name-sort control. Controls for adding, editing, status-flipping, or deleting appear only for the permissions they hold.

### Behavior 2: Search, filter, and sort
- **User does:** Types part of a brand name, and/or picks a status filter, and/or reverses the name sort order.
- **User sees:** The list narrows and reorders to match all three choices together, and returns to the first page of results. If nothing matches, a message says no brands match and offers to clear the search and filter.

### Behavior 3: Move between pages
- **User does:** Moves to the next, previous, or a specific page.
- **User sees:** The next set of up to 10 brands, with the active search, filter, and sort still applied.

### Behavior 4: Edit a brand
- **User does:** A staff member with the brand-edit permission opens a brand's edit form, changes any of the name, description, logo, or status, and saves.
- **User sees:** A confirmation that the brand was updated and a return to the brand list showing the new details. If the brand was set to Inactive, it is now marked Inactive and has disappeared from the customer-facing brand filter.

### Behavior 5: Delete an unused brand
- **User does:** A staff member with the brand-delete permission chooses to delete a brand no product uses, and confirms.
- **User sees:** A confirmation prompt naming the brand and warning the deletion is permanent; after confirming, a confirmation message and a list without that brand.

### Behavior 6: Attempt to delete a brand that is in use
- **User does:** Chooses to delete a brand that products reference, and confirms.
- **User sees:** The deletion is refused with a message stating how many products use the brand and suggesting they make the brand Inactive instead; the brand stays in the list unchanged.

### Behavior 7: Cancel a deletion
- **User does:** Opens the delete confirmation and then dismisses or cancels it.
- **User sees:** Nothing is deleted and the list is unchanged.

### Behavior 8: Attempt a capability without permission
- **User does:** A staff member without the brand-view permission tries to reach the manage-brands page, or a viewer-only staff member tries to reach the edit or delete capability, including by direct link.
- **User sees:** The store's standard access-denied experience; the capability never appeared in their view of the page.

### Behavior 9: Flip a brand's status from the list
- **User does:** A staff member with the brand-edit permission flips an Inactive brand to Active from its row; then flips an Active brand to Inactive.
- **User sees:** The activation takes effect at once, with a brief confirmation and the row now marked Active. The deactivation first asks them to confirm, naming the brand and noting it will no longer be visible to customers; after confirming, the row is marked Inactive and the brand has disappeared from the customer-facing brand filter.

### Behavior 10: Activate or deactivate several brands at once
- **User does:** Selects several brands — individually, or all on the current page — and chooses Activate, then repeats with Deactivate.
- **User sees:** The selected count while choosing, then all selected brands switched to the new status with a confirmation naming how many changed. Deactivating asks for confirmation first, stating how many brands will be hidden from customers.

### Behavior 11: Delete several brands at once, some of them in use
- **User does:** Selects several brands — some used by products, some not — chooses Delete, and confirms the prompt stating how many brands will be permanently deleted.
- **User sees:** The unused brands are gone from the list, and a message states how many were deleted and names the ones kept because products still use them, suggesting those be made Inactive instead. The kept brands remain selected so the staff member can act on them.

## 4. Constraints

- This is an admin-panel page. Each capability is gated by its own fine-grained permission in line with the store's access model — never by an "is an admin" shortcut, and never by a single blanket "manage brands" permission.
- Deletion is permanent and cannot be undone by the staff member, whether one brand or a selection; the confirmation must say so plainly. There is no undo, restore, or trash.
- A brand's edit address identifies the brand by its own permanent identifier — the only identifier a brand has — so a saved link keeps working after the brand is renamed.
- The brand name and description are stored exactly as the staff member types them and are not translated — brand names are proper nouns, and a single description is entered (no separate English and French versions). All of the feature's own interface text is available in English and French.
- A logo, when provided, must be an image file of an accepted type and within a size limit: PNG, JPG, and WebP are accepted, up to 2 MB — the same limits as adding a brand.
- The page shows 10 brands per page; the page size is fixed and not chosen by the staff member.
- The page, its list, filters, row selection, status switches, edit form, and every confirmation prompt are fully operable by keyboard with a visible focus indicator; validation messages are announced to screen-reader users and clearly associated with their fields; each confirmation prompt takes focus when it opens and returns focus to the list when dismissed; and changes to the list from searching, filtering, sorting, paging, selecting, or applying a bulk action are announced to screen-reader users, including the current selected count.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | A brand name is required and must contain at least one visible character — a name that is empty or only spaces does not count. | The save is rejected with a "name is required" message; the brand is unchanged. |
| **RULE-2** | Brand names must be unique regardless of capitalization and leading/trailing spaces. A brand is allowed to keep its own name when edited without other changes. This is the only uniqueness rule a brand name must satisfy — two names that differ in any other way (punctuation, spacing between words) are distinct and may coexist. | The save is rejected with a message that a brand with that name already exists; the brand is unchanged and the entered input is preserved. |
| **RULE-3** | The brand name must not exceed 100 characters, and the description must not exceed 250 characters. | The save is rejected with a message stating the limit for the offending field. |
| **RULE-4** | A logo, when provided, must be an accepted image type and within the size limit. | The file is refused with a message stating the accepted types and size limit; the rest of the form is untouched. |
| **RULE-5** | The brand's status must be either Active or Inactive — there is no other state. | Not selectable: the edit form only ever offers these two states. |
| **RULE-6** | A brand may only be deleted when no product references it — a product counts whether or not it is currently available to customers, so a brand used only by discontinued products still cannot be deleted. This is checked per brand, including for each brand in a bulk deletion. | The deletion is refused with a message stating how many products use the brand, and the staff member is offered the alternative of making it Inactive; the brand is unchanged. |
| **RULE-7** | A deletion only happens after the staff member explicitly confirms it in a prompt that states the deletion is permanent and names the brand — or, for a bulk deletion, states how many brands are being deleted. | Without confirmation nothing is deleted; dismissing or cancelling the prompt leaves every brand untouched. |
| **RULE-8** | Every capability on this page is checked against its own permission at the moment it is used, not only when the page is drawn. | A capability reached without its permission — including by direct link — is refused with the standard access-denied experience. |
| **RULE-9** | Search matches a brand name that contains the entered text, ignoring capitalization and surrounding spaces; the description is not searched. | An empty or spaces-only search is treated as no search at all — the full list is shown. |
| **RULE-10** | Changing the search text, the status filter, or the sort order returns the list to its first page. | Not violable by the staff member; it is how the list behaves. |
| **RULE-11** | A logo image that a brand no longer uses — because the brand was deleted, or the logo was removed or replaced while editing — is discarded rather than kept. | Not violable by the staff member; no unused logo is ever left stored. |
| **RULE-12** | Selecting every brand at once selects only the brands on the page currently being viewed — never brands on other pages. The selection is cleared whenever the page, search text, status filter, or sort order changes. | Not violable by the staff member; a bulk action can never reach a brand they have not seen. |
| **RULE-13** | A bulk deletion proceeds brand by brand: every selected brand that passes RULE-6 is deleted, every selected brand that fails it is kept, and the staff member is told both counts with the kept brands named. | Never all-or-nothing — one in-use brand does not block the rest. If every selected brand is in use, nothing is deleted and the message says so. |
| **RULE-14** | Deactivating a brand — from its row or as a bulk action — requires confirmation, because it removes the brand from everywhere customers see brands. Activating requires no confirmation. | Without confirmation no status changes; dismissing the prompt leaves every selected brand as it was. |

## 5. Edge Cases & Error Handling

- **Edge case:** No brands exist yet → **User experience:** The list shows an empty-state message explaining no brands have been added, with the add-brand option if they hold that permission.
- **Edge case:** A search or filter matches no brands → **User experience:** A message says no brands match the current search and filter, with a way to clear them; pagination controls do not offer pages that do not exist.
- **Edge case:** The staff member is on the last page and deletes its only remaining brand → **User experience:** They are shown the new last page of results rather than an empty page.
- **Edge case:** The staff member renames a brand to a name another brand already uses, differing only in capitalization or surrounding spaces → **User experience:** The save is refused with a "brand already exists" message (RULE-2); their input stays on the form.
- **Edge case:** The staff member opens a brand's edit form and saves without changing anything → **User experience:** The save succeeds and they return to the list; nothing about the brand has changed and no duplicate-name complaint is raised (RULE-2).
- **Edge case:** The brand being edited or deleted was already removed by another staff member → **User experience:** A clear message says the brand no longer exists, and the refreshed list is shown without it.
- **Edge case:** The staff member tries to delete a brand used by products → **User experience:** The deletion is refused with a message naming how many products use it and suggesting they set it Inactive instead (RULE-6, FR-13).
- **Edge case:** The staff member removes a brand's logo while editing → **User experience:** The form returns to its no-logo state and, after saving, the brand shows without a logo everywhere it appears; the old image is gone, not merely hidden (RULE-11).
- **Edge case:** The chosen replacement logo is not an accepted image type or is too large → **User experience:** The file is refused with a message stating what is accepted; the name, description, and status they entered are preserved, and the existing logo is untouched.
- **Edge case:** An edit or delete fails for a reason outside the staff member's control (e.g., a connection problem) → **User experience:** A clear error message says the change was not made and invites them to try again; their input is preserved and the brand is left exactly as it was — never partly changed.
- **Edge case:** The brand list is still loading → **User experience:** A loading indication is shown in place of the list, and the search, filter, and sort controls do not appear to have produced an empty result.
- **Edge case:** A staff member with view-only permission follows a direct link to a brand's edit form → **User experience:** The store's standard access-denied experience; the edit control never appeared for them on the list (FR-16, RULE-8).
- **Edge case:** A staff member follows a saved edit link for a brand that has since been deleted → **User experience:** A clear message says the brand no longer exists, and they are returned to the brand list.
- **Edge case:** Every brand in a bulk deletion turns out to be in use → **User experience:** Nothing is deleted; a message says none of the selected brands could be deleted because products still use them, and suggests making them Inactive instead (RULE-13).
- **Edge case:** The staff member selects brands and then changes the page, search, filter, or sort → **User experience:** The selection is cleared and the bulk-action controls disappear, so no action can land on brands they are no longer looking at (RULE-12).
- **Edge case:** The staff member selects brands and dismisses the deactivate or delete confirmation → **User experience:** Nothing changes, and the brands stay selected so they can retry or adjust the selection (RULE-7, RULE-14).
- **Edge case:** A bulk action partly fails for a reason outside the staff member's control (e.g., a connection problem mid-way) → **User experience:** A message states plainly which brands were changed and which were not, and the list is refreshed to show the true current state — never a state the list only appears to be in.
- **Edge case:** Flipping a brand's status from the list fails → **User experience:** The switch returns to its previous position and a message says the status was not changed, inviting them to try again.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given a staff member with the brand-view permission and more than 10 brands in the store, when they open the manage-brands page, then they see the first 10 brands with each brand's logo, name, description, and status, ordered by name A→Z with the status filter on All, together with pagination controls. Verifies FR-1, FR-2, FR-4, FR-5.
- [ ] **AC-2:** Given the brand list spans multiple pages, when the staff member moves to the next page, then they see the next set of up to 10 brands with any active search, status filter, and sort order still applied. Verifies FR-2, FR-3.
- [ ] **AC-3:** Given brands whose names both do and do not contain the text "aur", when the staff member searches for "AUR", then only the matching brands are listed regardless of capitalization and the list returns to its first page. Verifies FR-3, RULE-9, RULE-10.
- [ ] **AC-4:** Given a mix of Active and Inactive brands, when the staff member filters by Inactive, then only Inactive brands are listed; and when they filter by All, then both Active and Inactive brands are listed. Verifies FR-4.
- [ ] **AC-5:** Given a search term and a status filter are both applied, when the staff member reverses the name sort order to Z→A, then the listed brands still satisfy the search and filter and are reordered by name descending, starting from the first page. Verifies FR-3, FR-5, RULE-10.
- [ ] **AC-6:** Given a staff member with the brand-edit permission on a brand's edit form, when they change the name, description, logo, and status and save, then a confirmation is shown and they return to the brand list showing the new details. Verifies FR-7, FR-8.
- [ ] **AC-7:** Given a brand's edit form, when it is saved with the name empty or containing only spaces, then the save is refused with a "name is required" message and the brand is unchanged. Verifies FR-15, RULE-1.
- [ ] **AC-8:** Given two existing brands, when one is renamed to the other's name differing only in capitalization or surrounding spaces, then the save is refused with a "brand already exists" message and the entered input is preserved. Verifies RULE-2, FR-15.
- [ ] **AC-9:** Given a brand's edit form is opened with its existing name pre-filled, when it is saved with no changes at all, then the save succeeds without a duplicate-name complaint and the brand is unchanged. Verifies RULE-2.
- [ ] **AC-10:** Given a brand with a logo, when the staff member removes the logo and saves, then the brand appears without a logo everywhere it is shown and the removed image is no longer stored; and when they instead choose a valid replacement image and save, then the new logo is shown and the image it replaced is no longer stored. Verifies FR-10, RULE-4, RULE-11.
- [ ] **AC-11:** Given a file that is not an accepted image type or exceeds the size limit, when it is chosen as the replacement logo, then it is refused with a message stating the accepted types and limit, the rest of the entered input is preserved, and the brand's existing logo is untouched. Verifies RULE-4, FR-15.
- [ ] **AC-12:** Given an Active brand, when the staff member edits its status to Inactive and saves, then the brand is marked Inactive on the manage-brands page but no longer appears to customers anywhere in the store and is not offered when assigning products to a brand; and when set back to Active, it appears to customers again. Verifies FR-11.
- [ ] **AC-13:** Given a staff member with the brand-delete permission and a brand no product references, when they choose to delete it, then a confirmation prompt naming the brand and stating the deletion is permanent is shown; and when they confirm, then a confirmation message is shown, the brand no longer appears in the list or anywhere in the store, and its logo image is no longer stored. Verifies FR-12, FR-14, RULE-7, RULE-11.
- [ ] **AC-14:** Given a delete confirmation prompt is open, when the staff member cancels or dismisses it, then nothing is deleted and the list is unchanged. Verifies RULE-7.
- [ ] **AC-15:** Given a brand referenced by at least one product — including a brand whose only products are discontinued — when the staff member confirms deleting it, then the deletion is refused with a message stating how many products use the brand and offering to make the brand Inactive instead, and the brand remains unchanged. Verifies FR-13, RULE-6.
- [ ] **AC-16:** Given a staff member who holds only the brand-view permission, when they view the manage-brands page, then no add, edit, status-flip, or delete control is shown and no bulk action is offered; and when they follow a direct link to the add or edit capability, then they receive the store's standard access-denied experience. Verifies FR-6, FR-16, FR-23, RULE-8.
- [ ] **AC-17:** Given a user without the brand-view permission, when they attempt to reach the manage-brands page — including by direct link — then they receive the store's standard access-denied experience. Verifies FR-1 (access boundary), RULE-8.
- [ ] **AC-18:** Given no brands exist, or given a search and filter that match none, when the staff member views the list, then an empty-state message explains the situation and — for the no-match case — offers to clear the search and filter. Verifies FR-3, FR-4.
- [ ] **AC-19:** Given the site language is switched between English and French, when the brand list, its filters and sort control, the edit form, the delete confirmation, and all messages are viewed, then all of the feature's text follows the active language. Verifies FR-17.
- [ ] **AC-20:** Given a keyboard-only or screen-reader user with the required permissions, when they search, filter, sort, page, select brands, flip a status, edit, and delete, then every control is reachable and operable by keyboard with a visible focus indicator, validation messages are announced and associated with their fields, every confirmation prompt takes focus when it opens and returns focus to the list when dismissed, and list changes — including the selected count — are announced. Verifies the accessibility constraint.
- [ ] **AC-21:** Given an Inactive brand and a staff member with the brand-edit permission, when they flip its status from the list, then it becomes Active immediately with a confirmation and no prompt; and when they then flip it back, then they are asked to confirm before it becomes Inactive and disappears from the customer-facing brand filter. Verifies FR-18, RULE-14.
- [ ] **AC-22:** Given a page of brands, when the staff member selects every brand on the page at once, then only the brands on that page are selected and the selected count is shown; and when they then move to another page or change the search, filter, or sort, then the selection is cleared and the bulk-action controls disappear. Verifies FR-19, RULE-12.
- [ ] **AC-23:** Given three selected brands and a staff member with the brand-edit permission, when they choose Deactivate, then they are asked to confirm with the count of brands to be hidden from customers; and when they confirm, then all three are Inactive and hidden from customers with a confirmation naming how many changed. Verifies FR-20, RULE-14.
- [ ] **AC-24:** Given five selected brands of which two are referenced by products, when the staff member chooses Delete and confirms the prompt stating how many will be permanently deleted, then the three unused brands are deleted, the two in-use brands remain and are named as still in use, the staff member is told both counts, and the kept brands stay selected. Verifies FR-21, FR-22, RULE-6, RULE-13.
- [ ] **AC-25:** Given a selection in which every brand is referenced by products, when the staff member confirms Delete, then no brand is deleted and a message says none could be deleted because products still use them, suggesting they be made Inactive instead. Verifies RULE-13.
- [ ] **AC-26:** Given a staff member who holds the brand-edit permission but not brand-delete, when they select brands, then Activate and Deactivate are offered but Delete is not; and when they attempt a bulk deletion by direct means, then it is refused with the standard access-denied experience. Verifies FR-23, RULE-8.
- [ ] **AC-27:** Given a link to a specific brand's edit form, when a staff member with the brand-edit permission opens it, then that brand's edit form appears pre-filled; and when the same link is opened after the brand has been renamed, then it still opens that brand; and when it is opened after the brand has been deleted, then a message says the brand no longer exists and they are returned to the list. Verifies FR-24.
- [ ] **AC-28:** Given an existing brand named "Test Verify", when another brand is renamed to "Test.Verify", then the save succeeds — the two names are distinct under RULE-2 and no derived-identifier clash can refuse it. Verifies FR-9, RULE-2.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-07-25   ·   **Clarified:** 2026-07-25 (bulk actions, inline status toggle, per-brand edit address; brand's name-derived identifier retired)

<!-- Status lifecycle: "Draft — N open assumption(s)" → "Confirmed" once /theshop.clarify resolves them all (N = 0). -->

# Manage Categories

## 1. Problem Statement

Categories are how The Shop's catalogue is organized — every product sits in one, and customers narrow the catalogue by them. Yet categories are invisible to the staff who depend on them: there is no place to see which categories exist, create a new one when the range expands, fix a misspelling, or retire a category the store no longer sells into. Staff maintain the store's primary organizing structure blind, and a category that stops being relevant has no way to leave the customer-facing filter short of never using it again.

**Solution (one line):** A permission-gated admin page that lists every category — searchable, filterable, sortable, and paginated — from which staff can add, edit, activate/deactivate, and delete categories, with deletion refused for any category still holding products.

### Scope

**In scope:**
- A category list showing each category's image, name, description, and status (Active / Inactive), 10 categories per page with pagination.
- Searching categories by name, filtering by status (Active or Inactive — one at a time, or neither), and sorting the list by name or by when the category was added.
- Adding a category: name, description, image, and status.
- Editing an existing category: name, description, image, and status — all four are editable.
- Flipping a single category between Active and Inactive directly from the list, without opening the edit form.
- Selecting several categories from the list and applying one action to all of them: Activate, Deactivate, or Delete.
- Deleting a category permanently, behind an explicit confirmation, and only when no product belongs to it.
- Independently gating view, add, edit, and delete behind their own fine-grained permissions.
- Retiring the category's name-derived identifier: a category is identified only by its own permanent identifier, so renaming a category can no longer be refused because a derived identifier would clash.

**Out of scope:**
- Nesting: categories are flat. No parent categories, subcategories, or category trees.
- Reordering categories, any staff-controlled display order beyond the offered sorts, and bulk editing of names, descriptions, or images — Activate, Deactivate, and Delete are the only bulk actions.
- Merging two categories into one, or moving a category's products to another category.
- Assigning products to a category (delivered by product management features).
- Any customer-facing category landing page or category marketing content, and any change to how customers filter by category beyond Inactive categories disappearing from that filter. A category's image is consequently staff-visible only in this feature — it becomes the artwork for customer-facing category surfaces when those ship.
- An audit trail / change history of who added, edited, or deleted a category.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Staff member with the category-view permission | Open the manage-categories page and browse, search, filter, sort, and page through the category list | Add, edit, or delete a category unless they also hold that specific permission — those controls are not shown to them |
| Staff member with the category-create permission | Additionally reach the add-category form and create a category | — |
| Staff member with the category-edit permission | Additionally open a category's edit form and change its name, description, image, and status; flip a category's status from the list; and Activate or Deactivate several selected categories at once | — |
| Staff member with the category-delete permission | Additionally delete a category after confirming — one category, or several selected at once — when no product belongs to it | Delete a category that any product references, whether singly or as part of a selection |
| Staff member without the category-view permission | — | See or reach the manage-categories page; a direct attempt is refused with the store's standard access-denied experience |
| Guest / signed-in customer | — | See or reach any part of this feature |

Each of the four capabilities — view, add, edit, delete — is governed by its own fine-grained permission, consistent with the store's access model. Holding one does not imply another; a staff member may, for example, view and edit categories but not delete them.

## 2. Functional Requirements

1. **FR-1:** A staff member with the category-view permission can open the manage-categories page and see the categories that exist, each showing its image, name, description, and status.
2. **FR-2:** The category list shows 10 categories per page, with controls to move between pages and an indication of which page is being viewed.
3. **FR-3:** The staff member can search the list by category name; the list narrows to categories whose name matches, and search combines with the active status filter and sort order.
4. **FR-4:** The status filter offers exactly two options — Active and Inactive — and at most one is ever selected: choosing one replaces the other, and choosing the selected one again clears it. There is no "All" or "None" option to pick; with neither option selected the list shows every category, and that is how the filter opens.
5. **FR-5:** The staff member can sort the list four ways: by category name A→Z or Z→A, and by when the category was added, newest first or oldest first. The list opens sorted by name A→Z.
6. **FR-6:** A staff member with the category-create permission can reach the add-category form from this page; without that permission the option is not shown.
7. **FR-7:** On the add-category form the staff member enters a name, and optionally a description and an image. The status starts as Active, so a category saved without touching it is immediately available to customers; the staff member can set it to Inactive before saving to hold the category back.
8. **FR-8:** After a successful add, the staff member sees a confirmation and returns to the category list, which includes the new category; if it was created Active it is immediately available to customers and for assigning products.
9. **FR-9:** A staff member with the category-edit permission can open a category's edit form pre-filled with its current name, description, image, and status, change any of the four, and save.
10. **FR-10:** After a successful edit, the staff member sees a confirmation and returns to the category list, which reflects the change; the updated category's details appear wherever categories are used in the store.
11. **FR-11:** A category carries no name-derived identifier. Renaming a category changes only its name — nothing about how the category is identified changes, and a rename is never refused because a derived identifier would clash with another category's.
12. **FR-12:** A staff member with the category-edit permission can remove a category's image, or replace it with a different image, when editing. In both cases the image the category no longer uses is discarded, so nothing is left stored behind.
13. **FR-13:** Changing a category's status to Inactive removes it from the customer-facing category filter and from the list of categories products can be assigned to; it remains visible to staff on this page, and the products already in it are not deleted or reassigned. Changing it back to Active restores it.
14. **FR-14:** A staff member with the category-delete permission can delete a category, and must explicitly confirm the deletion before it happens; the confirmation names the category being deleted.
15. **FR-15:** A category that any product belongs to cannot be deleted. The attempt is refused with a message stating how many products are in the category, and the staff member is offered the alternative of making the category Inactive instead.
16. **FR-16:** After a successful deletion, the staff member sees a confirmation, the category no longer appears in the list or anywhere else in the store, and the category's image is discarded along with it.
17. **FR-17:** Invalid input on the add or edit form is rejected with a clear, field-specific message, and everything the staff member has already entered is preserved so they can correct and resubmit.
18. **FR-18:** Controls that a staff member lacks permission for are not shown to them at all, and a direct attempt to use such a capability is refused.
19. **FR-19:** A staff member with the category-edit permission can flip a single category between Active and Inactive directly from its row in the list, without opening the edit form. Activating takes effect immediately; deactivating asks for confirmation first, because it hides the category from customers.
20. **FR-20:** The add-category form and each category's edit form have their own addresses, so a staff member can bookmark or share a link that opens the add form or that specific category for editing.
21. **FR-21:** All text in this feature — headings, column labels, filter and sort options, buttons, confirmations, validation messages, empty-state text, and access-denied messages — is available in English and French and follows the active site language.
22. **FR-22:** The staff member can select one or more categories in the list, and can select or clear every category on the page they are viewing at once. The number of selected categories is shown.
23. **FR-23:** With categories selected, a staff member with the category-edit permission can Activate or Deactivate all of them in one action; deactivating asks for confirmation naming how many categories will be hidden from customers.
24. **FR-24:** With categories selected, a staff member with the category-delete permission can Delete all of them in one action, after confirming a prompt that states how many categories will be permanently deleted.
25. **FR-25:** A bulk deletion deletes every selected category no product belongs to and keeps every selected category that still holds products. Afterwards the staff member is told how many categories were deleted and which ones were kept because they are still in use.
26. **FR-26:** The bulk-action controls appear only once at least one category is selected, and offer only the actions the staff member holds the permission for — a staff member with edit but not delete sees Activate and Deactivate but no Delete.

## 3. Functional Behaviors

### Behavior 1: Browse the category list
- **User does:** A staff member with the category-view permission opens the manage-categories page.
- **User sees:** The first 10 categories, each with its image, name, description, status, and a way to select it, ordered by name A→Z with neither status option selected, plus pagination controls, a search box, the two-option status filter, and a sort control. Controls for adding, editing, status-flipping, or deleting appear only for the permissions they hold.

### Behavior 2: Search, filter, and sort
- **User does:** Types part of a category name, and/or selects Active or Inactive, and/or picks one of the four sort orders.
- **User sees:** The list narrows and reorders to match all three choices together, and returns to the first page of results. Selecting the other status option swaps the filter over; selecting the one already chosen clears it and every category is listed again. If nothing matches, a message says no categories match and offers to clear the search and filter.

### Behavior 3: Move between pages
- **User does:** Moves to the next, previous, or a specific page.
- **User sees:** The next set of up to 10 categories, with the active search, filter, and sort still applied.

### Behavior 4: Add a category
- **User does:** A staff member with the category-create permission opens the add-category form, enters a name, optionally a description and an image, leaves the status on its Active default or sets it to Inactive, and saves.
- **User sees:** A confirmation that the category was created and a return to the category list, which now includes it. If it was created Active, it is offered to customers in the category filter and when assigning products; if Inactive, it is not.

### Behavior 5: Edit a category
- **User does:** A staff member with the category-edit permission opens a category's edit form, changes any of the name, description, image, or status, and saves.
- **User sees:** A confirmation that the category was updated and a return to the category list showing the new details. If the category was set to Inactive, it is now marked Inactive and has disappeared from the customer-facing category filter.

### Behavior 6: Delete an empty category
- **User does:** A staff member with the category-delete permission chooses to delete a category no product belongs to, and confirms.
- **User sees:** A confirmation prompt naming the category and warning the deletion is permanent; after confirming, a confirmation message and a list without that category.

### Behavior 7: Attempt to delete a category that holds products
- **User does:** Chooses to delete a category that products belong to, and confirms.
- **User sees:** The deletion is refused with a message stating how many products are in the category and suggesting they make the category Inactive instead; the category stays in the list unchanged.

### Behavior 8: Cancel a deletion
- **User does:** Opens the delete confirmation and then dismisses or cancels it.
- **User sees:** Nothing is deleted and the list is unchanged.

### Behavior 9: Flip a category's status from the list
- **User does:** A staff member with the category-edit permission flips an Inactive category to Active from its row; then flips an Active category to Inactive.
- **User sees:** The activation takes effect at once, with a brief confirmation and the row now marked Active. The deactivation first asks them to confirm, naming the category and noting it will no longer be visible to customers; after confirming, the row is marked Inactive and the category has disappeared from the customer-facing category filter.

### Behavior 10: Attempt a capability without permission
- **User does:** A staff member without the category-view permission tries to reach the manage-categories page, or a viewer-only staff member tries to reach the add or edit capability, including by direct link.
- **User sees:** The store's standard access-denied experience; the capability never appeared in their view of the page.

### Behavior 11: Activate or deactivate several categories at once
- **User does:** Selects several categories — individually, or all on the current page — and chooses Activate, then repeats with Deactivate.
- **User sees:** The selected count while choosing, then all selected categories switched to the new status with a confirmation naming how many changed. Deactivating asks for confirmation first, stating how many categories will be hidden from customers.

### Behavior 12: Delete several categories at once, some of them in use
- **User does:** Selects several categories — some holding products, some empty — chooses Delete, and confirms the prompt stating how many categories will be permanently deleted.
- **User sees:** The empty categories are gone from the list, and a message states how many were deleted and names the ones kept because products still belong to them, suggesting those be made Inactive instead. The kept categories remain selected so the staff member can act on them.

## 4. Constraints

- This is an admin-panel page. Each capability is gated by its own fine-grained permission in line with the store's access model — never by an "is an admin" shortcut, and never by a single blanket "manage categories" permission.
- Deletion is permanent and cannot be undone by the staff member, whether one category or a selection; the confirmation must say so plainly. There is no undo, restore, or trash.
- A category's edit address identifies the category by its own permanent identifier — the only identifier a category has — so a saved link keeps working after the category is renamed.
- Categories are flat. A category never has a parent or children, and nothing in the add or edit form offers to place one inside another.
- The category name and description are stored exactly as the staff member types them and are not translated — a single description is entered, with no separate English and French versions. All of the feature's own interface text is available in English and French.
- An image, when provided, must be an image file of an accepted type and within a size limit: PNG, JPG, and WebP are accepted, up to 2 MB — the same limits the store applies to a brand logo.
- A category's image is shown to staff on this page only. Customers see categories as text, so no image reaches a customer-facing surface in this feature.
- The page shows 10 categories per page; the page size is fixed and not chosen by the staff member.
- Deactivating or deleting a category never changes the products in it — no product is deleted, hidden, or moved to another category as a side effect.
- The page, its list, filters, row selection, status switches, add and edit forms, and every confirmation prompt are fully operable by keyboard with a visible focus indicator; validation messages are announced to screen-reader users and clearly associated with their fields; each confirmation prompt takes focus when it opens and returns focus to the list when dismissed; and changes to the list from searching, filtering, sorting, paging, selecting, or applying a bulk action are announced to screen-reader users, including the current selected count.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | A category name is required and must contain at least one visible character — a name that is empty or only spaces does not count. | The save is rejected with a "name is required" message; nothing is created or changed. |
| **RULE-2** | Category names must be unique regardless of capitalization and leading/trailing spaces. A category is allowed to keep its own name when edited without other changes. This is the only uniqueness rule a category name must satisfy — two names that differ in any other way (punctuation, spacing between words) are distinct and may coexist. | The save is rejected with a message that a category with that name already exists; nothing is created or changed and the entered input is preserved. |
| **RULE-3** | The category name must not exceed 100 characters, and the description must not exceed 250 characters. | The save is rejected with a message stating the limit for the offending field. |
| **RULE-4** | An image, when provided, must be an accepted image type and within the size limit. | The file is refused with a message stating the accepted types and size limit; the rest of the form is untouched. |
| **RULE-5** | The category's status must be either Active or Inactive — there is no other state. A new category starts Active unless the staff member sets it to Inactive before saving. | Not selectable: the add and edit forms only ever offer these two states. |
| **RULE-6** | A category may only be deleted when no product belongs to it — a product counts whether or not it is currently available to customers, so a category holding only discontinued products still cannot be deleted. This is checked per category, including for each category in a bulk deletion. | The deletion is refused with a message stating how many products are in the category, and the staff member is offered the alternative of making it Inactive; the category is unchanged. |
| **RULE-7** | A deletion only happens after the staff member explicitly confirms it in a prompt that states the deletion is permanent and names the category — or, for a bulk deletion, states how many categories are being deleted. | Without confirmation nothing is deleted; dismissing or cancelling the prompt leaves every category untouched. |
| **RULE-8** | Every capability on this page is checked against its own permission at the moment it is used, not only when the page is drawn. | A capability reached without its permission — including by direct link — is refused with the standard access-denied experience. |
| **RULE-9** | Search matches a category name that contains the entered text, ignoring capitalization and surrounding spaces; the description is not searched. | An empty or spaces-only search is treated as no search at all — the full list is shown. |
| **RULE-10** | Changing the search text, the status filter, or the sort order returns the list to its first page. | Not violable by the staff member; it is how the list behaves. |
| **RULE-11** | An image that a category no longer uses — because the category was deleted, or the image was removed or replaced while editing — is discarded rather than kept. | Not violable by the staff member; no unused image is ever left stored. |
| **RULE-12** | Deactivating a category — from its row, by editing it, or as a bulk action — requires confirmation, because it removes the category from the customer-facing filter. Activating requires no confirmation. | Without confirmation the status does not change; dismissing the prompt leaves every selected category as it was. |
| **RULE-13** | An Inactive category is hidden from customers and from product assignment, but keeps every product already assigned to it. Reactivating restores the category with those products intact. | Not violable by the staff member; no product is ever detached as a side effect of a status change. |
| **RULE-14** | Selecting every category at once selects only the categories on the page currently being viewed — never categories on other pages. The selection is cleared whenever the page, search text, status filter, or sort order changes. | Not violable by the staff member; a bulk action can never reach a category they have not seen. |
| **RULE-15** | A bulk deletion proceeds category by category: every selected category that passes RULE-6 is deleted, every selected category that fails it is kept, and the staff member is told both counts with the kept categories named. | Never all-or-nothing — one in-use category does not block the rest. If every selected category is in use, nothing is deleted and the message says so. |
| **RULE-16** | At most one status option is selected at a time. Selecting Active while Inactive is selected swaps to Active; selecting the option already chosen clears it. No option selected means every category is listed — the filter offers no separate "All" or "None" option to choose. | Not violable by the staff member; the filter cannot reach a state where both options are selected. |

## 5. Edge Cases & Error Handling

- **Edge case:** No categories exist yet → **User experience:** The list shows an empty-state message explaining no categories have been added, with the add-category option if they hold that permission.
- **Edge case:** A search or filter matches no categories → **User experience:** A message says no categories match the current search and filter, with a way to clear them; pagination controls do not offer pages that do not exist.
- **Edge case:** The staff member is on the last page and deletes its only remaining category → **User experience:** They are shown the new last page of results rather than an empty page.
- **Edge case:** The staff member adds or renames a category to a name another category already uses, differing only in capitalization or surrounding spaces → **User experience:** The save is refused with a "category already exists" message (RULE-2); their input stays on the form.
- **Edge case:** The staff member opens a category's edit form and saves without changing anything → **User experience:** The save succeeds and they return to the list; nothing about the category has changed and no duplicate-name complaint is raised (RULE-2).
- **Edge case:** The category being edited or deleted was already removed by another staff member → **User experience:** A clear message says the category no longer exists, and the refreshed list is shown without it.
- **Edge case:** The staff member tries to delete a category that holds products → **User experience:** The deletion is refused with a message naming how many products are in it and suggesting they set it Inactive instead (RULE-6, FR-15).
- **Edge case:** The staff member adds a category without a description or an image → **User experience:** The save succeeds; the category appears in the list with no description and a neutral placeholder in place of an image.
- **Edge case:** The staff member removes a category's image while editing → **User experience:** The form returns to its no-image state and, after saving, the category shows without an image everywhere it appears; the old image is gone, not merely hidden (RULE-11).
- **Edge case:** The chosen image is not an accepted type or is too large → **User experience:** The file is refused with a message stating what is accepted; the name, description, and status they entered are preserved, and any existing image is untouched.
- **Edge case:** An add, edit, or delete fails for a reason outside the staff member's control (e.g., a connection problem) → **User experience:** A clear error message says the change was not made and invites them to try again; their input is preserved and the category is left exactly as it was — never partly changed, and never with an image stored for a category that was not saved.
- **Edge case:** The category list is still loading → **User experience:** A loading indication is shown in place of the list, and the search, filter, and sort controls do not appear to have produced an empty result.
- **Edge case:** A staff member with view-only permission follows a direct link to the add form or a category's edit form → **User experience:** The store's standard access-denied experience; the add and edit controls never appeared for them on the list (FR-18, RULE-8).
- **Edge case:** A staff member follows a saved edit link for a category that has since been deleted → **User experience:** A clear message says the category no longer exists, and they are returned to the category list.
- **Edge case:** Flipping a category's status from the list fails → **User experience:** The switch returns to its previous position and a message says the status was not changed, inviting them to try again.
- **Edge case:** A staff member deactivates the only category a set of products belongs to → **User experience:** The deactivation succeeds and those products keep their category; the category simply no longer appears in the customer-facing filter (RULE-13).
- **Edge case:** Every category in a bulk deletion turns out to hold products → **User experience:** Nothing is deleted; a message says none of the selected categories could be deleted because products still belong to them, and suggests making them Inactive instead (RULE-15).
- **Edge case:** The staff member selects categories and then changes the page, search, filter, or sort → **User experience:** The selection is cleared and the bulk-action controls disappear, so no action can land on categories they are no longer looking at (RULE-14).
- **Edge case:** The staff member selects categories and dismisses the deactivate or delete confirmation → **User experience:** Nothing changes, and the categories stay selected so they can retry or adjust the selection (RULE-7, RULE-12).
- **Edge case:** A bulk action partly fails for a reason outside the staff member's control (e.g., a connection problem mid-way) → **User experience:** A message states plainly which categories were changed and which were not, and the list is refreshed to show the true current state — never a state the list only appears to be in.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given a staff member with the category-view permission and more than 10 categories in the store, when they open the manage-categories page, then they see the first 10 categories with each category's image, name, description, and status, ordered by name A→Z with neither status option selected so both Active and Inactive categories appear, together with pagination controls. Verifies FR-1, FR-2, FR-4, FR-5.
- [ ] **AC-2:** Given the category list spans multiple pages, when the staff member moves to the next page, then they see the next set of up to 10 categories with any active search, status filter, and sort order still applied. Verifies FR-2, FR-3.
- [ ] **AC-3:** Given categories whose names both do and do not contain the text "liq", when the staff member searches for "LIQ", then only the matching categories are listed regardless of capitalization and the list returns to its first page. Verifies FR-3, RULE-9, RULE-10.
- [ ] **AC-4:** Given a mix of Active and Inactive categories and a filter with neither option selected, when the staff member selects Inactive, then only Inactive categories are listed; and when they then select Active, then the filter swaps and only Active categories are listed; and when they select Active a second time, then the selection is cleared and every category is listed again. The filter never offers an "All" or "None" option and never has both options selected at once. Verifies FR-4, RULE-16.
- [ ] **AC-5:** Given a search term and a status filter are both applied, when the staff member changes the sort order to name Z→A, then the listed categories still satisfy the search and filter and are reordered by name descending, starting from the first page. Verifies FR-3, FR-5, RULE-10.
- [ ] **AC-6:** Given a staff member with the category-create permission on the add-category form, when they enter a valid name, a description, and a valid image and save without touching the status, then the category is created Active, a confirmation is shown, they return to the category list which now includes it, and it is offered to customers in the category filter and when assigning products. Verifies FR-6, FR-7, FR-8, RULE-5.
- [ ] **AC-7:** Given the add-category form, when it is saved with only a name and no description or image, then the category is created and appears in the list Active, with no description and a placeholder in place of an image; and when the status is instead set to Inactive before saving, then the category is created Inactive and is not offered to customers. Verifies FR-7, FR-8, RULE-5.
- [ ] **AC-8:** Given a staff member with the category-edit permission on a category's edit form, when they change the name, description, image, and status and save, then a confirmation is shown and they return to the category list showing the new details. Verifies FR-9, FR-10.
- [ ] **AC-9:** Given the add or edit form, when it is saved with the name empty or containing only spaces, then the save is refused with a "name is required" message and nothing is created or changed. Verifies FR-17, RULE-1.
- [ ] **AC-10:** Given an existing category, when another category is added with that same name differing only in capitalization or surrounding spaces, or an existing category is renamed to it, then the save is refused with a "category already exists" message and the entered input is preserved. Verifies RULE-2, FR-17.
- [ ] **AC-11:** Given a category's edit form is opened with its existing name pre-filled, when it is saved with no changes at all, then the save succeeds without a duplicate-name complaint and the category is unchanged. Verifies RULE-2.
- [ ] **AC-12:** Given a name longer than 100 characters or a description longer than 250 characters, when the form is saved, then the save is refused with a message stating the limit for the offending field and the entered input is preserved. Verifies RULE-3, FR-17.
- [ ] **AC-13:** Given a category with an image, when the staff member removes the image and saves, then the category appears without an image everywhere it is shown and the removed image is no longer stored; and when they instead choose a valid replacement image and save, then the new image is shown and the image it replaced is no longer stored. Verifies FR-12, RULE-11.
- [ ] **AC-14:** Given a file that is not an accepted image type or exceeds the size limit, when it is chosen as the category image, then it is refused with a message stating the accepted types and limit, the rest of the entered input is preserved, and any existing image is untouched. Verifies RULE-4, FR-17.
- [ ] **AC-15:** Given an Active category holding products, when the staff member edits its status to Inactive and confirms, then the category is marked Inactive on the manage-categories page but no longer appears in the customer-facing category filter and is not offered when assigning products, while every product in it keeps that category; and when set back to Active, it appears to customers again with those products intact. Verifies FR-13, RULE-12, RULE-13.
- [ ] **AC-16:** Given an Inactive category and a staff member with the category-edit permission, when they flip its status from the list, then it becomes Active immediately with a confirmation and no prompt; and when they then flip it back, then they are asked to confirm before it becomes Inactive and disappears from the customer-facing category filter. Verifies FR-19, RULE-12.
- [ ] **AC-17:** Given a staff member with the category-delete permission and a category no product belongs to, when they choose to delete it, then a confirmation prompt naming the category and stating the deletion is permanent is shown; and when they confirm, then a confirmation message is shown, the category no longer appears in the list or anywhere in the store, and its image is no longer stored. Verifies FR-14, FR-16, RULE-7, RULE-11.
- [ ] **AC-18:** Given a delete confirmation prompt is open, when the staff member cancels or dismisses it, then nothing is deleted and the list is unchanged. Verifies RULE-7.
- [ ] **AC-19:** Given a category holding at least one product — including a category whose only products are discontinued — when the staff member confirms deleting it, then the deletion is refused with a message stating how many products are in the category and offering to make it Inactive instead, and the category and its products remain unchanged. Verifies FR-15, RULE-6.
- [ ] **AC-20:** Given a staff member who holds only the category-view permission, when they view the manage-categories page, then no add, edit, status-flip, or delete control is shown and no bulk action is offered; and when they follow a direct link to the add or edit capability, then they receive the store's standard access-denied experience. Verifies FR-6, FR-18, FR-26, RULE-8.
- [ ] **AC-21:** Given a user without the category-view permission, when they attempt to reach the manage-categories page — including by direct link — then they receive the store's standard access-denied experience. Verifies FR-1 (access boundary), RULE-8.
- [ ] **AC-22:** Given no categories exist, or given a search and filter that match none, when the staff member views the list, then an empty-state message explains the situation and — for the no-match case — offers to clear the search and filter. Verifies FR-3, FR-4.
- [ ] **AC-23:** Given a link to the add-category form and a link to a specific category's edit form, when a staff member with the matching permission opens each, then the add form appears empty and the edit form appears pre-filled with that category; and when the edit link is opened after the category has been renamed, then it still opens that category; and when it is opened after the category has been deleted, then a message says the category no longer exists and they are returned to the list. Verifies FR-20.
- [ ] **AC-24:** Given an existing category named "Vape Kits", when another category is renamed to "Vape.Kits", then the save succeeds — the two names are distinct under RULE-2 and no derived-identifier clash can refuse it. Verifies FR-11, RULE-2.
- [ ] **AC-25:** Given the site language is switched between English and French, when the category list, its filters and sort control, the add and edit forms, the delete confirmation, and all messages are viewed, then all of the feature's text follows the active language. Verifies FR-21.
- [ ] **AC-26:** Given a keyboard-only or screen-reader user with the required permissions, when they search, filter, sort, page, select categories, flip a status, add, edit, and delete, then every control is reachable and operable by keyboard with a visible focus indicator, validation messages are announced and associated with their fields, every confirmation prompt takes focus when it opens and returns focus to the list when dismissed, and list changes — including the selected count — are announced. Verifies the accessibility constraint.
- [ ] **AC-27:** Given a page of categories, when the staff member selects every category on the page at once, then only the categories on that page are selected and the selected count is shown; and when they then move to another page or change the search, filter, or sort, then the selection is cleared and the bulk-action controls disappear. Verifies FR-22, RULE-14.
- [ ] **AC-28:** Given three selected categories and a staff member with the category-edit permission, when they choose Deactivate, then they are asked to confirm with the count of categories to be hidden from customers; and when they confirm, then all three are Inactive and absent from the customer-facing category filter with a confirmation naming how many changed. Verifies FR-23, RULE-12.
- [ ] **AC-29:** Given five selected categories of which two hold products, when the staff member chooses Delete and confirms the prompt stating how many will be permanently deleted, then the three empty categories are deleted, the two in-use categories remain and are named as still in use, the staff member is told both counts, and the kept categories stay selected. Verifies FR-24, FR-25, RULE-6, RULE-15.
- [ ] **AC-30:** Given a selection in which every category holds products, when the staff member confirms Delete, then no category is deleted and a message says none could be deleted because products still belong to them, suggesting they be made Inactive instead. Verifies RULE-15.
- [ ] **AC-31:** Given a staff member who holds the category-edit permission but not category-delete, when they select categories, then Activate and Deactivate are offered but Delete is not; and when they attempt a bulk deletion by direct means, then it is refused with the standard access-denied experience. Verifies FR-26, RULE-8.
- [ ] **AC-32:** Given categories added at different times, when the staff member sorts by newest first, then the most recently added category is listed first; and when they sort by oldest first, then the order is reversed with the earliest added category first; and in both cases the list returns to its first page with any active search and status filter still applied. Verifies FR-5, RULE-10.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-08-01   ·   **Clarified:** 2026-08-02 (bulk actions added; inline status toggle, brand-matched limits, and staff-only category image confirmed; status filter reduced to two single-select options with no "All", added-date sorts added, new categories default to Active)

<!-- Status lifecycle: "Draft — N open assumption(s)" → "Confirmed" once /theshop.clarify resolves them all (N = 0). -->

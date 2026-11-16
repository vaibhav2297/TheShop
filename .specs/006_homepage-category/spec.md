# Homepage Category Section

**Feature:** `006_homepage-category`

## 1. Problem Statement

Store administrators need to configure a small, ordered set of categories for a future homepage section. The section's standard text and destination should be ready to edit, while administrators choose the three categories they want to feature. Administrators also need the editor heading to reflect the section title they are working on.

**Solution (one line):** Provide prefilled section fields, an initially empty category selection, and a live Add/Edit heading for the homepage category section.

### Scope

**In scope:**
- Add and edit the homepage category section using the section field values shown in the [Figma design](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3185-56958&t=ZaH338FUob2pht8E-11) as initial values; leave the Add categories table empty.
- Change the title, button label, button destination, selected categories, their order, and section visibility.
- Present the editor on desktop, tablet, and phone.

**Out of scope:**
- Creating, editing, or deleting category records or their images.
- Adding more than one homepage category section.
- Editing other homepage sections or global navigation.
- Displaying the configured section on the shopper-facing homepage, including category cards and links.
- Building the public `/categories` destination page.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Store administrator | Add, edit, reorder, and set the saved Visible/Hidden status of the homepage category section. | Access a second category section for the same homepage. |

## 2. Functional Requirements

1. **FR-1:** The Add form opens with Title `Discover by Category`, Button label `View All Categories`, Button link `/categories`, and Visible status. These fields are editable. The categories table starts empty and shows `0 of 3 selected`.
2. **FR-2:** The Edit form opens with the saved field values and the saved category selection and order.
3. **FR-3:** The top editor heading reads `Add {title}` or `Edit {title}` as appropriate and updates as the title field changes. When the field is empty, `{title}` reads `Discover by Category`.
4. **FR-4:** Administrators can replace selected categories, remove them, and change their order using the available controls. Each selected category shows its category image and name.
5. **FR-5:** The editor shows how many of the required three categories are selected and makes it clear when a category must be removed before another can be added.
6. **FR-6:** Administrators can change the section title, button label, button destination, and Visible/Hidden status and save those changes.
7. **FR-7:** Administrators can cancel editing without applying unsaved changes.
8. **FR-8:** The saved selection order and Visible/Hidden status are restored when an administrator reopens Edit. This feature does not display the section on the public homepage.

## 3. Functional Behaviors

### Behavior 1: Start or resume editing
- **User does:** An administrator opens Add or Edit for the homepage category section.
- **User sees:** The Add form with prefilled section fields, Visible status, and an empty categories table, or the Edit form with saved fields and categories; the appropriate Add/Edit heading is visible.

### Behavior 2: Change the heading and section details
- **User does:** An administrator changes or clears the title, button label, button destination, or status.
- **User sees:** The heading reflects the current title immediately, using `Discover by Category` when the title field is empty; other fields show the administrator's changes.

### Behavior 3: Curate categories
- **User does:** An administrator removes or adds a category, or changes the order of selected categories.
- **User sees:** The category names, images, order, and selected count update. At three selected categories, adding another is unavailable until one is removed.

### Behavior 4: Save or cancel
- **User does:** An administrator saves a valid section or cancels editing.
- **User sees:** Saved values and category order appear when editing is reopened. Cancel leaves the previously saved version intact.

## 4. Constraints

- The section and editor use English text only.
- The layout follows the linked Figma desktop, tablet, and phone designs. On Add, the section fields are prefilled while the categories table remains empty; the heading updates live.
- Category and ordering controls, status choices, Save, and Cancel must be usable with a keyboard and have clear accessible names. Category images must have an accessible text equivalent.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Exactly three distinct categories must be selected to save the section. | Save is rejected with guidance to add or remove categories; a fourth category cannot be added. |
| **RULE-2** | The top heading uses the current non-empty title field after surrounding spaces are ignored; otherwise it uses `Discover by Category`. | An empty title never leaves the Add/Edit heading blank. |
| **RULE-3** | A saved section must have a non-empty button label and destination; the button is always present when the section is Visible. | Save is rejected with guidance for the missing field. |
| **RULE-4** | The saved category order is restored in Edit. | Unsaved order changes are not persisted. |
| **RULE-5** | Visible/Hidden is a saved configuration value that remains available to administrators for editing. | Changing status without Save does not change the saved value. |
| **RULE-6** | Only one homepage category section may exist. Add creates it once; later changes use Edit. | Administrators are directed to edit the existing section rather than create another. |

## 5. Edge Cases & Error Handling

- **Edge case:** The administrator clears the title while editing. → **User experience:** The top heading immediately falls back to `Add Discover by Category` or `Edit Discover by Category`; the title field remains editable.
- **Edge case:** Fewer than three categories are selected when Save is pressed. → **User experience:** The editor remains open and explains that exactly three categories are required.
- **Edge case:** Three categories are already selected and the administrator wants another. → **User experience:** Add Category is unavailable and the editor explains that one must be removed first.
- **Edge case:** The administrator removes the button label or destination and presses Save. → **User experience:** The editor remains open and identifies the missing field.
- **Edge case:** The administrator changes values and presses Cancel. → **User experience:** The saved section remains unchanged.
- **Edge case:** The section is switched to Hidden and saved. → **User experience:** Edit reopens with Hidden selected.
- **Edge case:** A selected category is later deleted. → **User experience:** Edit identifies the unavailable selection so an administrator can replace it.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given no homepage category section exists, when an administrator opens Add, then the title, button label, button destination, and Visible status are prefilled as in FR-1, while the categories table is empty and shows `0 of 3 selected`.
- [ ] **AC-2:** Given the Add or Edit form is open, when the administrator changes the title, then the top heading immediately reads `Add {title}` or `Edit {title}` with the new title.
- [ ] **AC-3:** Given the title field is empty or contains only spaces, when the administrator views the top heading, then it reads `Add Discover by Category` or `Edit Discover by Category` (RULE-2).
- [ ] **AC-4:** Given three categories are selected, when the administrator attempts to add another, then Add Category is unavailable and guidance says to remove one first (RULE-1).
- [ ] **AC-5:** Given an administrator has selected fewer than three categories, when they press Save, then the section is not saved and the editor explains the three-category requirement (RULE-1).
- [ ] **AC-6:** Given the Add categories table is empty, when an administrator selects three distinct categories, changes their order, and saves, then Edit shows those same three categories in the saved order with their category images and names (RULE-1, RULE-4).
- [ ] **AC-7:** Given the button label or destination is blank, when an administrator presses Save, then the section is not saved and the missing field is identified (RULE-3).
- [ ] **AC-8:** Given an administrator changes the section title, button label, or destination and saves, when Edit is reopened, then those saved values are shown.
- [ ] **AC-9:** Given an administrator saves the section as Hidden, when Edit is reopened, then Hidden remains selected and the section remains editable (RULE-5).
- [ ] **AC-10:** Given an administrator has unsaved edits, when they press Cancel, then the previous saved values remain when Edit is reopened.
- [ ] **AC-11:** Given an existing homepage category section, when an administrator attempts to create another, then they are directed to edit the existing section (RULE-6).
- [ ] **AC-12:** Given the editor is used at desktop, tablet, or phone width with a keyboard, when the administrator navigates its controls, then fields, category ordering, status, Save, and Cancel remain operable with accessible names.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-11-05   ·   **Clarified:** 2026-11-05

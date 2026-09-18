# Product Description

## 1. Problem Statement

Staff need formatted product descriptions and separate specification name/value rows within product management. Existing description limit is 2,000 characters, not words (`create-product`, RULE-1); this feature replaces that restriction.

**Solution (one line):** Let authorized staff write richer descriptions and manage specifications while creating or editing products.

### Scope

**In scope:**
- Rich-text description on existing add-product and edit-product forms.
- Separate product specification name/value rows.
- Saving, reopening, updating, and removing this content; preserving existing descriptions.
- Revised description limit, validation, permissions, English/French interface text, and accessible editing.

**Out of scope:**
- Rearranging existing specification rows. Rows hold the order they were added in; changing that order means removing and re-adding.
- Storefront display, storefront preview, and product detail page; handled by product detail page scope.
- Search filters, comparisons, category attribute templates, shared specification libraries, and variant-specific specifications.
- Separate manufacturing, care, or warranty fields; narrative may appear in description, facts in specification rows.
- Embedded images/video, attachments, arbitrary page layouts, and additional product translation fields.
- Changes to existing brand, category, SKU, pricing, variants, publication, or product-list behavior.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Staff with product-create permission | Enter description/specifications when creating products | Edit existing products without product-edit permission |
| Staff with product-edit permission | Edit saved description/specifications | Create products without product-create permission |
| Staff without applicable permission, guests, customers | Retain existing unrelated capabilities | Reach unauthorized product forms or save content; existing access-denied/sign-in experience applies |

Existing permissions remain independent; product-view permission alone grants neither creation nor editing.

## 2. Functional Requirements

1. **FR-1:** Authorized staff can visually edit description using paragraphs, headings, bold, italic, bulleted lists, numbered lists, and links. Supported formatting appears while editing.
2. **FR-2:** Saving and reopening either product flow preserves description text and supported formatting. Existing plain-text descriptions remain readable and editable without lost text or paragraph breaks.
3. **FR-3:** Description is optional. Replace 2,000-character restriction with maximum 20,000 text characters, counting spaces and each paragraph/line break once; formatting and link destinations do not consume text allowance. Over-limit content receives clear guidance without silent truncation.
4. **FR-4:** Staff can add, edit, and remove separate specification name/value rows. Rows keep the order in which they were added; saved rows reopen with their values in that same order. Staff cannot rearrange existing rows. Specifications are optional; each retained row requires a plain-text name and value. Units belong within value, such as "750 ml".
5. **FR-5:** Description and specifications save through existing product save action. Existing success, unsaved-change warning, and rejection-preservation behavior covers both. Content changes alone do not alter unrelated product details.
6. **FR-6:** Existing product-create and product-edit permissions govern these capabilities; unauthorized attempts cannot change saved content.
7. **FR-7:** Editing and reopening description cannot execute active content supplied through product text. Pasted content retains supported text formatting without introducing arbitrary fonts, colors, or page layouts. Unsupported formatting is removed with notice; readable text and supported formatting remain.
8. **FR-8:** New labels, editing controls, help, validation, and result messages follow active English/French interface language. Staff-entered content remains as authored; switching interface language does not translate it.
9. **FR-9:** Every formatting and specification action is keyboard operable, has an accessible name and visible focus, and exposes validation and row changes to screen-reader users.

## 3. Functional Behaviors

### Behavior 1: Write description
- **User does:** Creates or edits product, writes description, applies supported formatting, and saves.
- **User sees:** Formatting while editing; existing save confirmation; saved content and formatting when reopening edit form.

### Behavior 2: Manage specifications
- **User does:** Adds "Material: Stainless steel" and "Capacity: 750 ml", edits values, or removes a row; saves.
- **User sees:** Separate rows reflecting changes; same retained values and order when reopening.

### Behavior 3: Correct rejected content
- **User does:** Saves an over-limit description or invalid specification row.
- **User sees:** Field/row-specific guidance; entered content remains available for correction; saved product remains unchanged.

## 4. Constraints

- This feature supersedes only description-length portions of `create-product` RULE-1 and AC-21. Other product rules remain applicable.
- Description remains optional, including for publication, under existing product rules. Removing all description text must remain possible.
- Confirmed 20,000-character allowance gives ten times current room for detailed copy while retaining a finite authoring boundary.
- Row editing and formatting controls must meet applicable WCAG 2.2 AA keyboard, focus, naming, and error-identification expectations.
- Editor selection and content-handling mechanisms belong to technical plan. Product scope grants no exception to project implementation rules.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Creation requires product-create permission; editing requires product-edit permission. | Existing denied-access experience; no saved changes. |
| **RULE-2** | Description text must satisfy FR-3 allowance; empty description is valid. | Over-limit save rejected with limit identified; input preserved without truncation. |
| **RULE-3** | Every retained specification row requires plain-text, non-whitespace name and value. Units belong within value. No rows is valid. | Incomplete row identified; save rejected with entered content preserved. |
| **RULE-4** | Specification names are unique within product after ignoring capitalization and surrounding whitespace. Put multiple values in one value field. | Duplicate rows identified; save rejected with correction guidance. |
| **RULE-5** | Only supported formatting from FR-1 is retained; product text must not execute active content. | Unsupported formatting removed with notice; readable text and supported formatting remain. Unsafe content cannot run during editing or reopening. |
| **RULE-6** | Validation rejection cannot partially save description, specification rows, or other product changes. | Saved product remains unchanged; form preserves edits for correction. |

## 5. Edge Cases & Error Handling

- **Edge case:** Existing description contains literal angle brackets or multiple paragraphs. **User experience:** Text remains text; paragraph breaks survive editing and saving.
- **Edge case:** Staff remove description text or every specification row. **User experience:** Empty content saves when other existing product rules pass.
- **Edge case:** Pasted content exceeds allowance or contains unsupported formatting. **User experience:** Formatting notice follows FR-7; over-limit save is rejected without truncating editable text.
- **Edge case:** Save fails or another staff member has changed product. **User experience:** Existing failure/conflict guidance applies; no false success or silent overwrite; local description and rows remain available for correction.
- **Edge case:** Focused specification row is removed. **User experience:** Focus remains on a usable specification control; removal is announced.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given product-create permission and otherwise valid product, when staff enter every FR-1 format plus specification rows and save, then creation succeeds; reopening shows same text, formatting, rows, and order. Covers FR-1, FR-2, FR-4, FR-5.
- [ ] **AC-2:** Given product-edit permission, when staff edit description, add/edit/remove specification rows, save, and reopen, then retained content and order match edits; unrelated saved product details remain unchanged. Covers FR-2, FR-4, FR-5.
- [ ] **AC-3:** Given existing plain-text description containing literal angle brackets and paragraph breaks, when staff open and save product, then text and paragraph breaks remain intact, with no interpretation as active content. Covers FR-2, FR-7.
- [ ] **AC-4:** Given otherwise valid product, when descriptions containing 2,001 and exactly 20,000 text characters are saved separately, then both save and reopen without truncation; formatting and link destinations do not reduce allowance. Covers FR-3, RULE-2.
- [ ] **AC-5:** Given 20,001-character description, when staff save, then description-specific message identifies 20,000-character maximum; full editable content remains; saved product remains unchanged. Covers FR-3, RULE-2, RULE-6.
- [ ] **AC-6:** Given otherwise valid product, when staff remove all description text and all specification rows and save, then empty content persists after reopening without introducing a publication requirement. Covers FR-4, RULE-2, RULE-3.
- [ ] **AC-7:** Given retained row with missing or whitespace-only name or value, when staff save, then offending row/field is identified; all entered content remains and saved product stays unchanged. Covers RULE-3, RULE-6.
- [ ] **AC-8:** Given rows named "Material" and " material ", when staff save, then duplicate guidance identifies affected rows; input remains and saved product stays unchanged. Covers RULE-4, RULE-6.
- [ ] **AC-9:** Given missing or revoked permission, when creation or editing is attempted through controls or directly, then unauthorized action is refused without saved changes; create-only cannot edit and edit-only cannot create. Covers FR-6.
- [ ] **AC-10:** Given pasted styled text containing supported emphasis and unsupported fonts/colors, when staff paste, save, and reopen, then readable text and emphasis remain, unsupported styling is absent, and removal notice appeared during editing. Covers FR-7.
- [ ] **AC-11:** Given supplied description containing executable content or a link intended to execute code, when staff edit, save, reopen, or activate that link, then supplied code never executes. Covers FR-7, RULE-5.
- [ ] **AC-12:** Given English or French active, when staff use formatting, row editing, validation, and save feedback, then all interface messages use selected language; authored description/specifications remain unchanged. Covers FR-8.
- [ ] **AC-13:** Given keyboard-only or screen-reader use, when staff apply every supported format and add/edit/remove rows, then controls are operable with accessible names and visible focus; row changes and field-associated errors are announced; row removal retains usable focus. Covers FR-9.
- [ ] **AC-14:** Given unsaved description or specification changes, when staff attempt to leave through existing protected navigation, then existing unsaved-change warning applies and choosing to stay preserves edits. Covers FR-5.
- [ ] **AC-15:** Given failed save or conflicting product edit, when staff attempt save, then existing failure/conflict guidance appears without false success or silent overwrite; local content remains available. Covers FR-5.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-09-09   ·   **Clarified:** 2026-09-10

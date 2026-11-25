# Manage Homepage Featured

**Feature:** `005_homepage-featured`

## 1. Problem Statement

Homepage staff need to refresh the Featured section's two promotional tiles without developer help. They need to manage artwork, text, destinations, and section visibility together. On long desktop forms, the section settings and actions should remain reachable while staff continue through tile content.

**Solution (one line):** Let authorized Homepage staff add or edit the Featured configuration using the supplied responsive design and the product detail page's sidebar scrolling behavior.

### Scope

**In scope:**

- One Featured configuration, with two fixed positions named Tile 1 and Tile 2; Add when absent and Edit when present.
- Each tile's image, Label, Title, Button label, and Button link.
- Section Title, dynamic Add/Edit heading, Visible/Hidden status, Save, and Cancel.
- Image browsing, drag-and-drop, preview, replacement, and removal in the draft.
- Saved configuration, initial loading states, validation, and failure guidance.
- Desktop, tablet, and phone administration layouts, including the requested desktop Section panel scrolling behavior.
- Existing Homepage View, Create, and Edit permissions.

**Out of scope:**

- Public homepage rendering, shopper tile navigation, and public responsive tile behavior; the supplied frames describe administration.
- Additional Featured configurations, adding/removing tile positions, reordering tiles, section deletion, scheduling, and campaign history.
- Product or category destination pickers, product editing, image cropping, and image generation.
- Changes to other Homepage editors or the product detail page.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Staff with Homepage View and Create | Inspect management; add Featured when absent. | Edit an existing configuration without Homepage Edit. |
| Staff with Homepage View and Edit | Inspect management; edit an existing configuration. | Add an absent configuration without Homepage Create. |
| Staff with Homepage View only | Inspect saved configuration or its absent state. | See mutation controls or save changes. |
| Signed-in user without Homepage View | See access-denied guidance. | Read management content or change configuration. |
| Guest | Reach sign-in when requesting management. | Read management content before authentication and required permissions. |

Permissions combine. Create/Edit alone does not grant management access, and role name alone grants no access.

## 2. Functional Requirements

1. **FR-1:** Expose Featured from Homepage administration with Admin Console / Homepage / Featured breadcrumbs. Page heading shows Add or Edit followed by current Section Title, updating as staff type. Blank or whitespace-only Section Title uses Featured in heading: Add Featured or Edit Featured. Heading fallback does not satisfy required Title validation.
2. **FR-2:** Require Homepage View to read management, plus Homepage Create for initial Save or Homepage Edit for later Save. Hide unauthorized mutation controls and deny unauthorized changes attempted directly.
3. **FR-3:** Show Tile 1 and Tile 2 in fixed order. Each contains an image area and plain-text Label, Title, Button label, and Button link fields. Section panel contains Title, mutually exclusive Visible/Hidden choices, Save, and Cancel. Add pre-fills Section Title with Featured; tile images and text start empty, Visible selected. Edit loads saved values, including Section Title.
4. **FR-4:** Let staff browse or drop an image for each tile, inspect its preview, replace it, or remove it from the draft. Replacement and removal affect only that tile. Removing an image keeps the tile position and its text fields available.
5. **FR-5:** Accept valid PNG, JPG, or WebP images up to 5 MB (5,242,880 bytes) each, square and at least 1200 × 1200. Show 5 MB upload guidance. Show design guidance: products only, no people or lifestyle scenes, room for bottom-left text. Keep previews proportional. Rejected images preserve accepted artwork. Composition guidance is advisory; no automatic scene rejection.
6. **FR-6:** Require nonblank Section Title, both tile Titles, and both images. Labels are optional. Each tile's button pair may be blank; otherwise require both Button label and Button link.
7. **FR-7:** Treat each Button link as free-entry destination text. Accept a valid site-relative path beginning with one `/` or an absolute HTTP/HTTPS URL; reject unsafe or malformed destinations. Save destination text without checking whether its target is available.
8. **FR-8:** Save the complete draft as one change, including both tiles, section title, and visibility. Confirm success only after saving succeeds, return to Homepage administration, and show saved values when reopened. Hidden configuration remains editable and occupies the sole Featured position.
9. **FR-9:** Cancel discards all pending changes and returns to Homepage administration. Cancelled Add creates nothing; cancelled Edit preserves saved text, artwork, links, and status.
10. **FR-10:** While loading an existing configuration, show the supplied skeleton arrangement without editable placeholder values or premature Add state. Loading failure shows an error and retry. Failed image selection or Save retains entered text and previously accepted artwork for correction.
11. **FR-11:** On desktop, place tile editors on the left and the Section panel on the right. Both columns initially scroll naturally. A panel taller than the visible scroll area stops once its final content is visible, leaving 24px clearance below its last action, while continued downward scrolling moves only the left content. A shorter panel stays below the shell header. Match product detail behavior: keep the panel within the editor boundary, release it before following content/footer, support upward scrolling to earlier fields, and adapt to resizing and content-height changes without hiding controls.
12. **FR-12:** On tablet, place Tile 1 and Tile 2 beside each other with Section settings/actions below them. On phone, stack Tile 1, Tile 2, then Section settings/actions. Tablet and phone use normal page scrolling without the desktop panel pinning. Preserve current draft values when the layout changes.
13. **FR-13:** Provide English text, associated field labels and errors, accessible image action names, visible keyboard focus, and keyboard operation for every control. Image browsing remains available without drag-and-drop. All fields and actions remain reachable while scrolling and at increased zoom.

## 3. Functional Behaviors

### Behavior 1: Open Featured management

- **User does:** Opens Featured from Homepage administration or its direct address.
- **User sees:** Loading skeletons followed by Add, Edit, or view-only state according to saved configuration and permissions. Add shows pre-filled Section Title Featured and heading Add Featured. Edit heading uses saved Section Title. Guest reaches sign-in; signed-in user without Homepage View sees access denied. Load failure offers retry rather than Add form.

### Behavior 2: Prepare tile content

- **User does:** Adds, replaces, or removes a tile image and edits that tile's text or destination.
- **User sees:** Only the chosen tile's draft changes. Its image preview and removal action reflect the accepted selection. Missing required fields or rejected images produce relevant guidance; neither tile's saved content changes before successful Save.

### Behavior 3: Configure the section

- **User does:** Edits Section Title and chooses Visible or Hidden.
- **User sees:** Draft title and selected status. Page heading follows current Section Title immediately, retaining Add/Edit prefix. Blank or whitespace-only Title falls back to Add Featured or Edit Featured; Save still requires Title. Changes remain pending until Save. Hidden configuration opens in Edit rather than allowing another Add.

### Behavior 4: Scroll through the editor

- **User does:** Scrolls down a desktop editor with tile content longer than its Section panel, then scrolls back up or changes viewport size.
- **User sees:** The Section panel follows the product detail page's end-of-content stop behavior in FR-11 while left content continues. Its fields and actions remain reachable; it does not overlap following content. Tablet and phone show the stacked arrangements and ordinary scrolling in FR-12.

### Behavior 5: Save or cancel

- **User does:** Selects Save or Cancel.
- **User sees:** Successful Save confirmation and Homepage administration, or errors with the draft retained. Cancel returns without applying pending changes. Reopening after success shows the complete saved configuration.

## 4. Constraints

- Design source: [Featured section, node 3185:56957](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3185-56957&t=cWBtc9BODDtj4EUK-11).
- Inspected frames: Edit desktop `3169:36840`, tablet `3169:42518`, phone `3169:43241`; Add desktop `3218:63016`, tablet `3218:63464`, phone `3218:63886`; Edit skeleton desktop `3194:33156`, tablet `3194:33595`, phone `3194:34008`.
- The user's scrolling instruction supplements the static Figma design. Product detail's established behavior defines tall/short panel handling, 24px lower clearance, editor containment, and normal stacked scrolling below desktop.
- User confirmed dynamic Add/Edit heading and pre-filled Section Title Featured on Add. This overrides empty Section Title in Figma Add frames. Other Figma sample content is not pre-filled; truncated example destinations are not accepted links.
- User confirmed 5 MB (5,242,880 bytes) per Featured image, matching Hero Banner. This overrides Figma's 2 MB text; upload guidance shows 5 MB.
- English is the only supported storefront language. Keep admin labels and guidance consistent with existing Homepage management.
- Appearance follows the supplied design and is reviewed manually by the user. Passing behavioral or accessibility checks does not imply visual approval.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Management requires Homepage View. Initial Save additionally requires Homepage Create; later Save additionally requires Homepage Edit. | Deny unauthorized access or mutation; preserve saved configuration and hide unavailable mutation controls. |
| **RULE-2** | At most one Featured configuration exists, including when Hidden. It has exactly two fixed tile positions. | Reject another Add or an invalid tile count; offer reload when a configuration already exists. |
| **RULE-3** | Section Title and both tile Titles contain non-whitespace text; both tiles have accepted images. Labels are optional. | Reject Save with field-specific guidance; retain draft and previous saved values. |
| **RULE-4** | Each selected image is valid PNG, JPG, or WebP, no larger than 5,242,880 bytes, square, and at least 1200 × 1200. | Reject selection with relevant requirement; preserve accepted artwork. |
| **RULE-5** | Each tile's Button label and Button link are both blank or both nonblank. | Reject Save for the incomplete pair and identify the missing counterpart. |
| **RULE-6** | A nonblank Button link is a valid root-relative path beginning with one `/` or an absolute HTTP/HTTPS URL. Protocol-relative, script/data, and backslash-based destinations are invalid. | Reject Save and explain accepted destination formats. |
| **RULE-7** | Saved status is Visible or Hidden, and only successful Save applies draft changes. | Reject invalid status or failed Save; keep the previous saved configuration intact. |
| **RULE-8** | Desktop panel stopping applies only within the editor and must keep every field/action reachable. Tablet and phone scroll normally. | Release or adjust the panel as required to prevent clipped controls, covered content, or trapped scrolling. |

## 5. Edge Cases & Error Handling

- **Edge case:** No configuration exists. **User experience:** Authorized creator sees Add Featured, Section Title pre-filled with Featured, empty tile fields, and Visible selected. View-only staff see absent state.
- **Edge case:** Section Title becomes blank or whitespace-only. **User experience:** Heading falls back to Add Featured or Edit Featured. Save identifies required Section Title; draft remains available.
- **Edge case:** A Hidden configuration exists or another creator saves first. → **User experience:** No duplicate is created; staff can reload the existing configuration for permitted inspection or editing.
- **Edge case:** One image is removed without replacement. → **User experience:** The tile and its text remain in the draft; Save identifies the missing image and leaves saved artwork intact.
- **Edge case:** Image is corrupt, unsupported, oversized, undersized, or non-square. → **User experience:** Relevant selection guidance appears and the previous accepted image remains available.
- **Edge case:** File browsing is cancelled or a replacement fails. → **User experience:** Existing draft content and accepted artwork remain unchanged.
- **Edge case:** Required text is blank, a button pair is incomplete, or a destination is invalid. → **User experience:** Save identifies the affected field and tile without clearing either tile's draft.
- **Edge case:** A syntactically valid destination is currently unavailable. → **User experience:** Staff may save the destination; this editor does not verify or open it for shoppers.
- **Edge case:** Loading or Save fails, or permissions change during editing. → **User experience:** Clear retry, failure, or denial guidance appears; saved content remains unchanged and permitted draft content stays available.
- **Edge case:** Save is activated repeatedly. → **User experience:** One Save proceeds without duplicate configurations or partial tile updates.
- **Edge case:** Section panel becomes taller after errors appear, viewport resizing, or zoom. → **User experience:** Its stopping position adjusts so all content is readable before its end stops; keyboard focus can reveal every field/action.
- **Edge case:** Left content ends, the user reverses scrolling, or the layout becomes tablet/phone. → **User experience:** The panel stays within the editor, earlier fields can be reached, following content is uncovered, and stacked layouts scroll normally without losing draft values.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given no Featured configuration and Homepage View/Create, when staff open management, then heading is Add Featured, Section Title is pre-filled with Featured, breadcrumbs appear, two empty tile editors retain order, and Visible, Save, and Cancel appear. Given Add mode, when staff change Section Title to Summer Picks, then heading updates immediately to Add Summer Picks. Covers FR-1 through FR-3.
- [ ] **AC-2:** Given saved configuration and Homepage View/Edit, when staff open management, then heading shows Edit followed by saved Section Title, and both saved tiles, Section Title, and status appear, including when Hidden. Given Edit mode, when staff change Section Title to Summer Picks, then heading updates immediately to Edit Summer Picks. Covers FR-1, FR-3, and FR-8.
- [ ] **AC-3:** Given a guest or signed-in user without Homepage View, when management is requested directly or through navigation, then the guest reaches sign-in and the signed-in user sees access denied without management content. Create/Edit alone does not grant access. Covers RULE-1.
- [ ] **AC-4:** Given Homepage View without the relevant Create/Edit permission, when staff inspect the absent or saved configuration and attempt a change, then mutation controls are absent and unauthorized changes are denied. Covers FR-2 and RULE-1.
- [ ] **AC-5:** Given either tile editor, when staff browse or drop an accepted image, replace it, or remove it, then only that tile's draft preview changes, text and tile position remain, and saved artwork stays unchanged before Save. Cancelling file selection preserves accepted artwork. Covers FR-4.
- [ ] **AC-6:** Given valid square PNG, JPG, and WebP images at least 1200 × 1200 and no larger than 5,242,880 bytes, when selected, then images are accepted with proportional previews, 5 MB upload guidance, and design composition guidance. Covers FR-5 and RULE-4.
- [ ] **AC-7:** Given an image over 5,242,880 bytes, below 1200 × 1200, non-square, corrupt, or unsupported, when selected, then relevant guidance appears without replacing accepted artwork; an otherwise valid file exactly at size limit is accepted. Covers RULE-4.
- [ ] **AC-8:** Given missing image or blank/whitespace-only Section Title or tile Title, when Save is selected, then affected fields are identified, draft remains, and saved configuration does not change. Given Add or Edit mode, when Section Title becomes blank or whitespace-only, then heading falls back to Add Featured or Edit Featured without filling Title or bypassing validation. Blank Labels alone do not block Save. Covers FR-1 and RULE-3.
- [ ] **AC-9:** Given both button fields blank or only one populated on either tile, when Save is selected with other valid settings, then a blank pair saves without a button configuration and an incomplete pair is rejected with field guidance. Covers RULE-5.
- [ ] **AC-10:** Given paired button fields, when staff save valid root-relative or absolute HTTP/HTTPS destinations, then entered links persist; when a malformed, protocol-relative, script/data, or backslash-based destination is submitted, then Save is rejected with field guidance. Covers FR-7 and RULE-6.
- [ ] **AC-11:** Given a complete valid draft and required permissions, when staff Save and reopen management, then success appears only after saving succeeds, Homepage administration opens, and all tile and section values persist together. Saving Hidden then Visible preserves content and selected status. Covers FR-8 and RULE-7.
- [ ] **AC-12:** Given unsaved Add or Edit changes including image replacements/removals, when staff Cancel, then Homepage administration opens, cancelled Add creates nothing, and existing saved content remains unchanged. Covers FR-9.
- [ ] **AC-13:** Given an existing or concurrently created configuration, when another Add or repeated Save is attempted, then no duplicate configuration or partial tile update appears and a creation conflict offers reload. Covers RULE-2 and FR-8.
- [ ] **AC-14:** Given pending loading or a load failure, when staff open management, then responsive skeletons appear while loading and an error with retry appears on failure without prematurely exposing Add or editable placeholder values. Covers FR-10.
- [ ] **AC-15:** Given draft changes, when image selection or Save fails or permission is lost, then clear failure or denial guidance appears, saved configuration stays intact, and previously entered text and accepted artwork remain where access permits. Covers FR-2 and FR-10.
- [ ] **AC-16:** Given desktop tile content longer than a Section panel that exceeds the visible scroll area, when staff scroll downward beyond the panel's final content, then both columns initially scroll together, the panel stops with 24px clearance below its last action, and only the left content continues; it releases at the editor boundary without covering following content. Covers FR-11 and RULE-8.
- [ ] **AC-17:** Given a desktop Section panel shorter than the visible scroll area, when staff scroll through longer tile content, then it remains below the shell header within the editor boundary and Save/Cancel remain accessible. Covers FR-11 and RULE-8.
- [ ] **AC-18:** Given a stopped desktop panel, when staff reverse scrolling, reveal errors, resize the viewport, increase zoom, or move keyboard focus to an earlier field, then every panel field/action remains reachable, the stop position adapts, and no draft values are lost. Covers FR-11 and FR-13.
- [ ] **AC-19:** Given tablet or phone layout, when staff scroll the editor or change between layouts, then tablet shows tile editors side by side above Section settings, phone stacks Tile 1 then Tile 2 then Section settings, and both use normal scrolling without desktop panel pinning or lost draft values. Covers FR-12.
- [ ] **AC-20:** Given any supported layout, when staff use keyboard or assistive technology, then fields, errors, image actions, status, Save, and Cancel have accessible names, visible focus, and English text, browsing works without drag-and-drop, and content remains usable without horizontal page overflow. Covers FR-13.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-11-01   ·   **Clarified:** 2026-11-01

# Manage Hero Banner

**Feature:** `003_manage-hero-banner`

## 1. Problem Statement

Homepage staff need control over featured artwork, messaging, and navigation. Without dedicated management, campaigns remain outdated or require developer changes. Unauthorized users must not change homepage content.

**Solution (one line):** Let Homepage permission holders add or edit one hero banner through administration and save its content, artwork, and status.

### Scope

**In scope:**

- One homepage hero banner: Add when absent; Edit when present.
- Desktop artwork, optional mobile artwork, heading, description, button label, button link, and Visible/Hidden status.
- Image selection, previews, replacement, removal, Save, and Cancel.
- Persisted banner settings, available when authorized staff reopen management.
- Separate Homepage View, Create, and Edit permissions.
- Desktop, tablet, and phone management layouts from supplied design.

**Out of scope:**

- Multiple banners, carousel behavior, ordering, scheduling, and campaign history.
- Storefront/public homepage rendering, responsive storefront artwork selection, and shopper button navigation.
- Banner deletion; administration supports saved Visible/Hidden status.
- Product/category selection for button destinations, rich text, image editing, and automatic image generation.
- Other Homepage management features and permission administration screens.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Staff with Homepage View and Create | Open management; add banner when absent. | Modify existing banner without Homepage Edit. |
| Staff with Homepage View and Edit | Open management; edit existing banner, including artwork and visibility. | Add absent banner without Homepage Create. |
| Staff with Homepage View only | Inspect saved banner or absent state. | See mutation controls or save changes. |
| Signed-in user without Homepage View | See access-denied guidance. | Open management or read management content. Create/Edit alone does not grant page access. |
| Guest | Reach sign-in when requesting management. | Open management before authentication and required permissions. |

Permissions combine: staff holding all three capabilities may add and edit. Role name alone grants no access.

## 2. Functional Requirements

1. **FR-1:** Expose Hero Banner management within Homepage administration. Show breadcrumbs Admin Console / Homepage / Hero Banner. Show Add Hero Banner when absent; Edit Hero Banner when present.
2. **FR-2:** Require Homepage View to access management. Require only Homepage Create for the Create command; only Homepage Edit for the Update command, including artwork and status. Hide unauthorized mutation controls. Deny unauthorized operations even when attempted outside visible controls.
3. **FR-3:** Show Desktop image, Mobile image (optional), Content, and Status sections. Content contains Heading, Description, Button label, and Button link. Edit loads saved values, previews, and status. Add starts with empty content and images; Visible is selected as shown in design.
4. **FR-4:** Allow image selection through browsing or drag-and-drop. Show selected image preview, Replace Image, and an accessible image-removal action. Stage changes until Save. Removing desktop artwork requires replacement before saving; mobile artwork may be removed and saved as absent.
5. **FR-5:** Accept desktop artwork in PNG, JPG, or WebP, at least 1920 × 1080, with 16:9 proportions. Accept optional mobile artwork with 4:5 proportions. Display size/format guidance and advice to keep desktop artwork's left third clear for text. Retain design's mobile guidance about use below 600px and desktop center-crop fallback as explanatory text only. Storefront behavior is outside scope. Apply RULE-4 upload limits.
6. **FR-6:** Let staff edit plain heading and description text. Require nonblank heading; description is optional.
7. **FR-7:** Provide Button link as a simple text field where staff enter navigation URL. Provide Button label as plain text. Allow both fields empty to save no button configuration; otherwise require both.
8. **FR-8:** Accept site-relative destinations starting with a single `/`, including `/products`, and absolute HTTP/HTTPS destinations. Reject unsafe or malformed destinations. Save entered destination for later use; no shopper navigation is delivered here.
9. **FR-9:** Offer mutually exclusive Visible and Hidden choices. Save selected status alongside artwork and content. Hidden banner remains editable and occupies single banner position.
10. **FR-10:** Save validates all entered content and selected artwork, then saves banner as one change. Show success only after saving succeeds, then return to Homepage administration. Reopening management shows saved values.
11. **FR-11:** Cancel discards pending changes and returns to Homepage administration. Existing saved banner remains unchanged; cancelled Add creates nothing.
12. **FR-12:** Follow supplied responsive design: desktop places images beside content/status/actions; tablet and phone stack images before content/status/actions. Preserve readable labels, keyboard operation, visible focus, associated errors, and accessible names for image actions. User-facing text is English.

## 3. Functional Behaviors

### Behavior 1: Open management

- **User does:** Opens Hero Banner from Homepage administration or its direct address.
- **User sees:** Authorized Add/Edit form or view-only state under FR-1 through FR-3. Guest sees sign-in; signed-in user without Homepage View sees access denied.

### Behavior 2: Prepare artwork

- **User does:** Adds, replaces, or removes desktop/mobile artwork.
- **User sees:** Updated draft previews and guidance. Invalid selection shows actionable error and keeps previously accepted artwork. Saved banner changes only after successful Save.

### Behavior 3: Configure content and navigation

- **User does:** Enters heading, description, button label, and URL text; selects Visible or Hidden.
- **User sees:** Editable content and selected status. Button link remains free-entry text. No destination picker appears.

### Behavior 4: Save or cancel

- **User does:** Selects Save or Cancel.
- **User sees:** Successful Save confirms persisted changes and returns to Homepage administration. Failed Save displays errors while retaining draft. Cancel returns to Homepage administration without persisting draft.

## 4. Constraints

- Design source: [Hero Banner section, node 3185:56955](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3185-56955).
- Inspected design frames: Edit desktop `3169:36055`, tablet `3169:39766`, phone `3169:40407`; Add desktop `3176:51656`, tablet `3176:51697`, phone `3176:51738`.
- User clarification overrides any destination-selector appearance: Button link is a simple URL text field.
- User confirmed one banner and separate Homepage View/Create/Edit permissions. No banner-delete capability belongs to this feature.
- Uploaded artwork remains intact; admin previews never stretch artwork. Left-third composition advice is guidance, not an automatic rejection rule.
- Both image fields accept PNG, JPG, or WebP up to 5 MB each (5,242,880 bytes). This confirmed limit overrides design's 2 MB text; admin upload guidance must show 5 MB.
- Deliver banner administration only. Storefront rendering, publication effects, and shopper navigation require separate scope.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Management page access requires Homepage View. Each Create command requires only Homepage Create; each Update command requires only Homepage Edit. | App route handles denied page access. Commands deny missing action permission; preserve saved banner. Hide unauthorized mutation controls. |
| **RULE-2** | At most one saved hero banner exists. Hidden banner still occupies that position. | Reject second Add; retain existing banner and direct staff to reload existing banner. |
| **RULE-3** | Saving requires desktop artwork and nonblank heading. Description remains optional. | Reject save; identify missing field; retain draft. |
| **RULE-4** | Each upload is valid PNG, JPG, or WebP and no larger than 5,242,880 bytes. Desktop meets 16:9 and minimum 1920 × 1080; mobile meets 4:5. | Reject selection with relevant requirement; preserve previously accepted artwork. |
| **RULE-5** | Button label and link are either both blank or both nonblank. | Reject save; identify missing counterpart. |
| **RULE-6** | Button destination is valid root-relative path beginning with one `/`, or absolute HTTP/HTTPS URL. Script/data destinations, protocol-relative URLs, and backslash-based destinations are invalid. | Reject save; explain accepted destination formats. |
| **RULE-7** | Only successful Save changes saved banner settings. Saved status must be Visible or Hidden. | Reject invalid status or failed save; preserve previous saved banner. |

## 5. Edge Cases & Error Handling

- **Edge case:** No saved banner. **User experience:** Authorized creator sees empty Add form; view-only staff see absent state.
- **Edge case:** Hidden banner exists. **User experience:** Authorized staff edit existing banner; Add does not create another.
- **Edge case:** Mobile artwork absent or removed. **User experience:** Optional mobile upload area appears empty; saving absence succeeds; explanatory fallback guidance remains visible.
- **Edge case:** Desktop artwork removed without replacement. **User experience:** Save reports required desktop artwork; saved banner remains intact.
- **Edge case:** Unsupported, corrupt, oversized, undersized, or wrong-ratio artwork. **User experience:** Relevant guidance appears; rejected artwork does not replace prior accepted image.
- **Edge case:** Button label/link incomplete or destination invalid. **User experience:** Save identifies field error; draft remains available for correction.
- **Edge case:** Loading fails. **User experience:** Error and retry appear; failure never masquerades as absent banner or authorizes Add.
- **Edge case:** Upload or Save fails. **User experience:** Clear failure message; pending text and previously accepted artwork remain available; previous saved banner stays unchanged.
- **Edge case:** Permission lost during editing. **User experience:** Save denied; saved banner remains unchanged; unavailable management capabilities disappear when access updates.
- **Edge case:** Another creator saves first. **User experience:** Second Add rejected under RULE-2; staff can reload existing banner.
- **Edge case:** Repeated Save activation while saving. **User experience:** One operation proceeds; duplicate banner cannot appear.
- **Edge case:** Entered destination is syntactically valid but unavailable. **User experience:** Staff may save valid URL text; destination availability is not verified.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given no saved banner and Homepage View/Create permissions, when staff open management, then Add Hero Banner shows FR-3 fields, empty images/content, Visible selected, Save, and Cancel. Covers FR-1 through FR-3.
- [ ] **AC-2:** Given existing banner and Homepage View/Edit permissions, when staff open management, then Edit Hero Banner shows saved content, image previews, and status. Covers FR-1 through FR-3.
- [ ] **AC-3:** Given guest or signed-in user without Homepage View, when management access is attempted directly or through navigation, then guest is directed to sign-in and signed-in user sees access denied without management content. Covers FR-2, RULE-1.
- [ ] **AC-4:** Given Homepage View without relevant Create/Edit permission, when staff inspect absent/existing banner and attempt unauthorized changes, then mutation controls are absent and attempted changes are denied. Create/Edit without View also denies management. Covers FR-2, RULE-1.
- [ ] **AC-5:** Given valid desktop and optional mobile files, when staff browse or drop files and replace accepted artwork, then corresponding draft previews and removal actions update while saved artwork remains unchanged before Save. Covers FR-4, FR-5.
- [ ] **AC-6:** Given desktop/mobile uploads at and above 5,242,880 bytes or with invalid format, content, dimensions, or proportions, when selected, then valid boundary-size files are accepted and invalid files show relevant errors without replacing prior artwork; upload guidance shows 5 MB. Covers FR-5, RULE-4.
- [ ] **AC-7:** Given missing desktop artwork or blank heading, when Save is selected, then required-field guidance appears, draft remains, and saved banner does not change. Description may remain blank. Covers FR-6, RULE-3.
- [ ] **AC-8:** Given both button fields blank or only one populated, when Save is selected, then blank pair saves without button configuration and incomplete pair is rejected with field guidance. Covers FR-7, RULE-5.
- [ ] **AC-9:** Given paired button fields, when staff enter `/products` or valid HTTP/HTTPS URL as plain text and save, then destination persists; malformed, script/data, protocol-relative, and backslash-based destinations are rejected. Covers FR-7, FR-8, RULE-6.
- [ ] **AC-10:** Given valid banner draft and required permissions, when staff save, then success appears and they return to Homepage administration. When they reopen management, complete saved content, artwork, URL text, and status persist. Covers FR-10, RULE-7.
- [ ] **AC-11:** Given saved banner, when authorized staff save Hidden then later Visible and reopen management after each save, then selected status persists and artwork/content remain available for editing. Covers FR-9, RULE-7.
- [ ] **AC-12:** Given unsaved Add or Edit changes including image removal/replacement, when Cancel is selected, then staff return to Homepage administration and saved content remains unchanged. Covers FR-11.
- [ ] **AC-13:** Given selected mobile artwork, when staff remove it and save then reopen management, then mobile artwork remains absent and explanatory fallback guidance appears; removing desktop artwork without replacement blocks Save. Covers FR-4, FR-5, RULE-3.
- [ ] **AC-14:** Given draft changes, when upload or Save fails or permission is lost, then actionable failure/denial appears and previously saved banner remains unchanged. Given management loading failure, when staff open management, then retry appears instead of Add state. Covers FR-2, FR-4, FR-10.
- [ ] **AC-15:** Given repeated Save attempts or existing banner created by another staff member, when Add is submitted, then no second banner is created and conflict offers reload. Hidden existing banner also prevents another Add. Covers RULE-2.
- [ ] **AC-16:** Given desktop, tablet, and phone management views, when staff use keyboard or assistive technology, then layout follows FR-12, image previews preserve proportions, FR-5 guidance appears, fields/actions have accessible names, focus remains visible, errors identify fields, and English content remains usable without horizontal overflow. Covers FR-5, FR-12.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-10-27   ·   **Clarified:** 2026-10-27

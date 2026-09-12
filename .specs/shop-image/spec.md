# Shop Image

## 1. Problem Statement

Shoppers and staff need consistent image proportions across screen sizes, covering all ten requested image uses.

**Solution (one line):** Provide reusable image treatments with flexible display width, proportional height, and predictable cropping.

### Scope

**In scope:**

- All ten presets in FR-1; apply corresponding treatments to existing image placements.
- Responsive display, whole-image preservation, photography crops, and mobile banner composition.
- Accessible image descriptions and English/French presentation.

**Out of scope:**

- Building absent product-detail, cart, order, editorial, hero, or other destination screens merely to demonstrate a preset.
- Image editing tools, new upload workflows, automatic artwork generation, and changes to image ownership or access permissions.
- Changes to product selection, shopping actions, or surrounding page content.

### Actors & Access

Guests and signed-in customers receive identical treatments on pages they may view. Staff receive corresponding treatments in permitted administration screens. Existing page and image access rules remain authoritative; presets grant no additional access.

## 2. Functional Requirements

1. **FR-1:** Support every treatment below. Ratios describe width:height of displayed frames, except logos, which retain original proportions. Presets remain available for destination screens delivered separately.

   | Use case | Ratio | Visible treatment |
   |---|---|---|
   | Product card | 1:1 | Whole product image visible; empty space allowed |
   | Product detail gallery | 1:1 | Whole product image visible; empty space allowed |
   | Cart, order, admin thumbnail | 1:1 | Whole image visible within allocated thumbnail space |
   | Category tile | 1:1 | Photography fills frame; edges may crop |
   | Category banner, desktop | 16:5 | Photography fills frame; edges may crop |
   | Hero, desktop | 16:9 | Photography fills frame; edges may crop |
   | Hero/banner, mobile | 4:5 | Mobile composition fills frame; dedicated crop supported |
   | Editorial/lifestyle card | 4:3 | Photography fills frame; edges may crop |
   | Brand logo | Original ratio | Entire logo visible within allocated space |
   | Social sharing image | 40:21 | Prepared composition retains its proportions |

2. **FR-2:** Display width follows available page space; height follows selected ratio. Images remain inside their allocated space on phone, tablet, and desktop screens. Thumbnail space may be constrained by its row; no preset imposes one universal display width.
3. **FR-3:** Never stretch images. Whole-image treatments preserve all source content; photographic treatments may crop edges without altering source images. Center photographic crops, trimming excess edges evenly. Dedicated compositions retain their supplied framing when source and frame ratios match; otherwise center the remaining crop.
4. **FR-4:** Heroes and category banners use their desktop ratios in desktop presentation and 4:5 in mobile presentation. Use dedicated mobile artwork when supplied. When mobile artwork is absent, reuse desktop artwork with centered 4:5 cropping under FR-3.
5. **FR-5:** Image arrival does not move surrounding content after its frame appears. Missing or unviewable images use the existing named-placeholder treatment, displaying the associated name or supplied label. Placeholder proportions match selected preset; logo placeholders fit allocated logo space. Preserve frame size and responsive display width.
6. **FR-6:** Meaningful images have understandable descriptions in active English/French language; product and brand names remain identifiable. Decorative images do not add redundant screen-reader descriptions. Placeholder labels follow active language where translated; proper product and brand names remain unchanged.
7. **FR-7:** Image presentation preserves existing access restrictions and surrounding actions. Noninteractive images add no keyboard stops; existing image-linked actions retain keyboard activation, accessible names, and visible focus.

## 3. Functional Behaviors

### Behavior 1: Browse images

- **User does:** Opens an available shopping or administration screen.
- **User sees:** Corresponding FR-1 treatment; products and logos remain whole, photography fills designated frames.

### Behavior 2: Change available screen space

- **User does:** Resizes window or changes device orientation.
- **User sees:** Image width follows available space; height follows ratio. Mobile hero/banner presentation uses FR-4.

### Behavior 3: View unavailable imagery or use assistive technology

- **User does:** Opens content with missing imagery, changes language, or navigates using keyboard/screen reader.
- **User sees:** Stable image space and FR-5 named placeholder; localized descriptions and preserved actions under FR-6 and FR-7.

## 4. Constraints

- Ratios govern presentation; source/export pixel dimensions are not fixed screen dimensions.
- Square catalogue treatment follows existing project image guidance. Preserve original assets; presentation must not permanently crop them.
- Supplied social composition keeps 40:21 proportions; appearance inside third-party sharing interfaces is outside this feature's control.
- Product Catalogue and Manage Product retain their existing shopping behavior and access rules.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Preset frame follows FR-1 ratio within available space. | Adjust displayed dimensions; do not stretch image or overflow allocated space. |
| **RULE-2** | Product images and logos remain wholly visible. | Fit entire source inside frame; leave empty space instead of cropping. |
| **RULE-3** | Crop-enabled photography fills frame proportionally. | Crop excess edges under FR-3; never distort or alter original asset. |
| **RULE-4** | Presets confer no viewing or editing permission. | Preserve existing sign-in or access-denied experience for restricted destinations. |

## 5. Edge Cases & Error Handling

- **Edge case:** Very tall or wide product. **User experience:** Entire image remains visible; empty space surrounds it as needed.
- **Edge case:** Wide logo. **User experience:** Original proportions remain within allocated space; no square crop.
- **Edge case:** Different source and photographic frame ratios. **User experience:** Excess edges crop under FR-3; source remains intact.
- **Edge case:** Missing mobile artwork. **User experience:** FR-4 fallback applies; desktop artwork remains available for desktop presentation.
- **Edge case:** Missing or unviewable image. **User experience:** FR-5 named placeholder matches preset proportions without collapsing frame or obscuring surrounding actions.
- **Edge case:** Narrow screen or orientation change. **User experience:** Displayed dimensions adapt without image-caused horizontal overflow.
- **Edge case:** Destination screen not yet delivered. **User experience:** Preset remains supported; this feature does not introduce that screen.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given product-card and product-detail imagery, when each treatment is viewed with tall and wide sources, then frames are 1:1 and entire sources remain visible without stretching. Covers FR-1, FR-3, RULE-2.
- [ ] **AC-2:** Given cart, order, and admin thumbnail treatments, when available row space changes, then each frame remains 1:1, stays within its allocated space, and preserves whole image. Covers FR-1, FR-2.
- [ ] **AC-3:** Given category-tile photography, when source ratio differs from frame, then image fills a 1:1 frame with proportional cropping under FR-3. Covers FR-1, RULE-3.
- [ ] **AC-4:** Given desktop category-banner and hero treatments, when viewed, then frames are respectively 16:5 and 16:9 with proportional photography crops. Covers FR-1.
- [ ] **AC-5:** Given hero/banner content with dedicated mobile artwork, when viewed in mobile presentation, then dedicated artwork fills a 4:5 frame; desktop presentation retains its corresponding desktop treatment. Covers FR-1, FR-4.
- [ ] **AC-6:** Given hero/banner content without mobile artwork, when viewed in mobile presentation, then desktop artwork fills a 4:5 frame using FR-3 cropping. Covers FR-4.
- [ ] **AC-7:** Given editorial/lifestyle photography, when viewed, then image fills a 4:3 frame without stretching. Covers FR-1, FR-3.
- [ ] **AC-8:** Given a wide or tall brand logo, when allocated display space changes, then entire logo remains visible in original proportions within that space. Covers FR-1, FR-2.
- [ ] **AC-9:** Given a prepared social sharing image, when presented for sharing, then supplied composition remains 40:21 without distortion. Covers FR-1.
- [ ] **AC-10:** Given each ratio-based treatment, when available width changes across phone, tablet, and desktop presentation, then height follows selected ratio and image stays inside allocated space; FR-4 mobile changes still apply. Covers FR-2, RULE-1.
- [ ] **AC-11:** Given each image treatment, when its source is missing or fails to display, then a placeholder displays associated name or supplied label in preset proportions; logo placeholders fit allocated logo space. Image arrival or replacement preserves frame size, surrounding content positions, and actions. Covers FR-5.
- [ ] **AC-12:** Given meaningful, decorative, and unavailable imagery, when active language changes between English and French, then descriptions and placeholder labels follow FR-6; proper product and brand names remain unchanged, and decorative images add no redundant description. Covers FR-6.
- [ ] **AC-13:** Given an existing image-linked action, when reached and activated by keyboard, then accessible name, visible focus, and existing outcome remain available; noninteractive images add no keyboard stops. Covers FR-7.
- [ ] **AC-14:** Given a guest, customer, or staff member without access to a restricted destination, when attempting to view it, then existing sign-in or access-denied experience remains; image presets grant no access. Covers FR-7, RULE-4.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-09-11   ·   **Clarified:** 2026-09-11

# Product Detail

**Feature:** `001_product-detail`

## 1. Problem Statement

Shoppers need product information before choosing a variant. Missing details, images, or variant pricing prevent informed choices.

**Solution (one line):** Show product details, browsable images, and pricing matching selected variant.

### Scope

**In scope:**
- Product brand, name, variants, pricing, description, and specifications.
- Desktop gallery initially shows first 4 images in a grid, primary image first. Show More expands gallery to all images when more than 4 exist.
- Tablet and phone galleries show one large image at a time, with horizontal scrolling/swiping and position indicator.
- No initial variant selection; show variant price range, or single amount when equal, with primary image first. Products without variants show their own price.
- Variant selection updates displayed pricing and identifies matching image within responsive gallery.
- Visible, enabled Add to Bag and Favourite controls; Add to Bag briefly shakes variant-selection block when selection is missing, without visible error text.
- Separate collapsible description and specifications sections, shown only when populated.

**Out of scope:**
- Actual bag additions or favourite changes, persistence, counters, and success messages. Missing-variant validation remains in scope.
- Checkout, quantity selection, reviews, recommendations, and product administration.
- Image zoom, video, and additional product information beyond stated details.

### Actors & Access

Product detail belongs to public storefront. Guests and signed-in shoppers share viewing and selection behavior. No sign-in required.

## 2. Functional Requirements

1. **FR-1:** Show product name, brand when supplied, and pricing. With variants but none selected, show lowestâ€“highest current variant selling price; identical prices show one amount. After selection, show selected variant's exact current selling price. Products without variants show their own current selling price.
2. **FR-2:** Show available variant choices with none selected initially; clearly identify variant after shopper selects one.
3. **FR-3:** Desktop initially shows up to 4 product and variant images in a grid. When more than 4 exist, Show More expands gallery to all images. Tablet and phone show horizontally scrollable images, one large image at a time, with position indicator. Primary image appears first in each layout.
4. **FR-4:** On tablet and phone, scrolling or swiping to another image displays it at large size and updates position indicator without changing selected variant or price.
5. **FR-5:** Selecting a variant updates displayed pricing and targets its first associated image. Tablet and phone show that image and update indicator. Desktop scrolls to image only when outside viewport, without highlighting or reordering. If image is hidden beyond first 4, expand gallery before scrolling.
6. **FR-6:** Show enabled Add to Bag and Favourite controls. When product has variants but none selected, Add to Bag briefly shakes variant-selection block instead of showing error text. Actual bag additions and favourite changes remain excluded.
7. **FR-7:** Show description only when populated; shoppers can expand and collapse its content.
8. **FR-8:** Show specifications only when populated; shoppers can expand and collapse its content independently.
9. **FR-9:** Support keyboard operation and meaningful accessible names for variant choices, gallery navigation, expandable sections, and action controls. Current image position must be perceivable without relying only on visual indicator.
10. **FR-10:** Show understandable empty or unavailable states when product or images cannot be displayed.

11. **FR-11:** Desktop buy box scrolls naturally before sticking at the end of its own content while the gallery continues. Keep all content reachable, contain stickiness within the product section, recalculate for content/viewport changes, and retain normal stacked scrolling on tablet/phone.

## 3. Functional Behaviors

### Behavior 1: Open product detail
- **User does:** Opens a product.
- **User sees:** Name, supplied brand, pricing, and variant choices. Desktop shows first 4 images in a grid with primary image first, plus Show More when additional images exist. Tablet and phone initially show primary image at large size, with position indicator and other images available by horizontal scrolling/swiping. Add to Bag and Favourite controls remain visible.
- **Initial selection:** No variant selected, even when product has variants. Show lowestâ€“highest current variant selling price, or one amount when all prices match. Product without variants shows its own current selling price and no variant choices. Variant products require no product-level price.

### Behavior 2: Browse images
- **User does:** Views desktop grid and activates Show More when available, or scrolls/swipes tablet or phone gallery.
- **User sees:** Desktop initially shows up to 4 images; Show More expands grid to every image in existing order and becomes Show Less. Show Less restores the first four images; selected variant and price remain unchanged. With 4 or fewer images, Show More is omitted. Tablet and phone show chosen image at large size and update position indicator. Price and selected variant stay unchanged. No thumbnail-preview strip required.

### Behavior 3: Change variant
- **User does:** Selects a variant, initially or after another selection.
- **User sees:** Selected variant indicated; its exact current selling price replaces initial range or shared amount. Tablet and phone navigate to first associated image and update position indicator. Every image remains available; primary image keeps first position.
- **Desktop response:** Keep grid order fixed; never highlight matching image. Scroll to first associated image only when outside viewport. If image lies beyond first 4 in collapsed gallery, automatically expand gallery to all images before scrolling. If target is already visible, keep current scroll position.
- **Missing variant image:** Show product primary image. First associated variant image takes precedence after selection; no associated image falls back to product primary.
- **Selection validation:** Selecting a variant clears missing-selection attention state and any static emphasis used for reduced motion.

### Behavior 4: Read description or specifications
- **User does:** Expands or collapses either populated section.
- **User sees:** Both sections start collapsed. Corresponding content appears or hides. Other section keeps its current state; sections expand independently.

### Behavior 5: Activate action controls
- **User does:** Activates Add to Bag or Favourite.
- **User sees:** Both controls remain enabled. When product has variants but none selected, Add to Bag brings variant-selection block into view if needed, focuses variant choices without selecting one, and briefly shakes block. No visible error text appears. With a variant selected, or for products without variants, Add to Bag has no effect. Favourite always has no effect. No bag or favourite changes, navigation, or success messages occur.

## 4. Constraints

- User-facing text is English only.
- Product details reflect supplied information; no invented brand, description, specifications, or prices.
- Current selling price uses sale price when present, otherwise original price. Initial variant range uses lowest and highest of those amounts; never substitute a nonexistent product-level price for variant products.
- Variant selection updates price and gallery response together; subsequent manual image browsing preserves variant and price.
- Variant validation applies only to Add to Bag on products with variants; Favourite never requires variant selection.
- Missing-selection feedback respects reduced-motion preference: use static outline instead of shake. Assistive technology announces selection is required without adding visible error text. Each invalid activation provides attention feedback; animation remains brief and never loops continuously.
- Gallery navigation and section expansion must support keyboard users; current image and expanded states must be perceivable.
- Existing product visibility restrictions apply; this feature adds no product editing capability.
- Responsive design reference: [Product Detail in Figma](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-61089). Desktop grid with Show More and tablet/phone swipe indicator replace earlier large-image-plus-previews requirement.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Primary image occupies first grid position on desktop and initial displayed position on tablet/phone; no variant is initially selected. | Restore initial unselected state and primary-first order; never automatically select first variant. |
| **RULE-2** | Desktop initially shows first 4 images, or all when fewer exist; Show More reveals remaining images. Tablet/phone make every image reachable by gallery navigation. Variant selection preserves gallery membership and order. | Restore missing images to gallery; never filter out or reorder images solely because variant changed. |
| **RULE-3** | With variants but none selected, display lowestâ€“highest current variant selling price, collapsing equal prices to one amount. After selection, display that variant's exact price. Without variants, display product's own current selling price. | Recalculate incorrect range or amount; never require product-level price for a variant product or retain another variant's price. |
| **RULE-4** | Variant selection targets first associated image, falling back to product primary. Tablet/phone display target and update indicator. Desktop expands if target is hidden beyond first 4, then scrolls only when target is outside viewport; no image highlight. | Reveal correct target without reordering or highlighting; preserve scroll position when target is already visible. |
| **RULE-5** | Manual gallery navigation changes only viewed image and corresponding position indicator. | Preserve variant selection and displayed price. |
| **RULE-6** | Description and specifications appear only when their respective content is nonblank. | Omit empty section and its expansion control. |
| **RULE-7** | Expanding or collapsing one content section preserves other section's state. | Leave other section unchanged. |
| **RULE-8** | Add to Bag and Favourite remain enabled; only missing-variant validation has an effect. | Keep controls enabled; no actual bag additions, favourite changes, navigation, or success messages. |
| **RULE-9** | Add to Bag requires variant selection only when product has variants. | Bring selection block into view if needed, focus choices without selecting, and briefly shake block; reduced motion uses static outline. Announce requirement to assistive technology without visible error text; preserve price and gallery state. |
| **RULE-10** | Selecting a variant clears missing-selection attention state. | Remove static emphasis once selection exists; never leave stale missing-selection feedback. |
| **RULE-11** | Tablet/phone position indicator tracks displayed image after manual navigation and variant selection. | Update indicator to current image; never leave stale position. |
| **RULE-12** | Show More appears only on desktop when images remain hidden beyond first 4; activation expands to all images. | Omit both controls with 4 or fewer images; after expansion show Show Less, which collapses to the first four without changing selection or price. |

## 5. Edge Cases & Error Handling

- **Edge case:** Product has no variants. **User experience:** Show product's own current selling price and images; omit variant choices. Add to Bag triggers no missing-selection feedback and performs no bag action.
- **Edge case:** Every variant has same current selling price. **User experience:** Before selection, show one amount instead of repeating equal range endpoints; no variant is automatically selected.
- **Edge case:** Product has variants but none selected when Add to Bag is activated. **User experience:** Reveal and focus variant choices; briefly shake selection block without visible error text or changes to price or gallery state. No variant is automatically selected.
- **Edge case:** Shopper prefers reduced motion. **User experience:** Missing-selection feedback uses static outline and focus instead of shake; selection requirement is announced to assistive technology. Selecting a variant clears outline.
- **Edge case:** Product has one image. **User experience:** Show sole image within desktop grid or tablet/phone image area; no unavailable image choices or scrolling to nonexistent images.
- **Edge case:** Desktop variant image is hidden beyond first 4 displayed images. **User experience:** Automatically expand gallery to all images, then scroll to target only when outside viewport; no image highlight.
- **Edge case:** Desktop variant image is already visible in viewport. **User experience:** Update selected variant and price without scrolling, image highlight, or grid reordering.
- **Edge case:** Product lacks primary designation or images cannot display. **User experience:** Use first available image when no primary exists; show product name in Subtitle 1 when no image displays. Keep product details usable.
- **Edge case:** Selected variant lacks associated image. **User experience:** Show product primary image, or product-name placeholder in Subtitle 1 when needed.
- **Edge case:** Description or specifications contain only whitespace. **User experience:** Omit corresponding section; populated section remains usable.
- **Edge case:** Product cannot be found or viewed. **User experience:** Show clear product-unavailable message; no stale product details or selectable variants.

## 6. Acceptance Criteria

- [ ] **AC-25:** Given desktop product content taller than its buy box, when the shopper scrolls, then both columns initially scroll together; a tall buy box stops with 24px clearance below its last control at the visible scroll area bottom after all its content can be read, while the gallery continues. A shorter buy box sticks below the shell header. Both release at the product boundary, remain keyboard-accessible, adapt to resizing/content changes, and scroll normally below 1024px. User-requested behavior, 2026-10-21.

- [ ] **AC-1:** Given a viewable product with brand, name, variants with different current selling prices, and no product-level price, when a shopper opens product detail, then those details and variant choices appear with no variant selected, lowestâ€“highest variant price range displayed, and primary image first in desktop grid or initially displayed on tablet/phone; verifies RULE-1 and RULE-3.
- [ ] **AC-2:** Given the same viewable product, when guests and signed-in shoppers open product detail, then both receive identical viewing and selection capabilities without sign-in prompt.
- [ ] **AC-3:** Given more than 4 product and variant images and a designated primary image, when product detail opens on desktop, then first 4 images appear in grid with primary first and Show More available, without thumbnail-preview strip; verifies RULE-1, RULE-2, and RULE-12.
- [ ] **AC-4:** Given tablet or phone gallery with multiple images, when shopper scrolls or swipes to another image, then chosen image appears at large size, position indicator updates, and price and variant remain unchanged; verifies RULE-5 and RULE-11.
- [ ] **AC-5:** Given variants with different prices and associated images, when shopper selects a variant initially or changes selection, then selected variant and displayed price match that variant, tablet/phone display matching image with updated indicator, desktop reveals target image and scrolls only when outside viewport without image highlight, and every image remains available in original order; verifies RULE-2, RULE-3, RULE-4, and RULE-11.
- [ ] **AC-6:** Given a variant without associated images, when shopper selects it, then its price appears and gallery targets product primary image instead of previous variant image; verifies RULE-3 and RULE-4.
- [ ] **AC-7:** Given a product without variants, when shopper opens product detail and activates Add to Bag, then product's own current selling price and gallery appear without variant choices, no missing-selection feedback occurs, and no bag action occurs; verifies RULE-3, RULE-8, and RULE-9.
- [ ] **AC-8:** Given populated description and specifications, when shopper opens product detail and expands or collapses either section, then both start collapsed and only operated section changes; verifies RULE-7.
- [ ] **AC-9:** Given absent or whitespace-only description or specifications, when shopper opens product detail, then each empty section and its expansion control are omitted while populated content remains available; verifies RULE-6.
- [ ] **AC-10:** Given a viewable product, when shopper activates Favourite regardless of variant selection or Add to Bag after selecting a variant, then both controls remain enabled and produce no bag or favourite changes, navigation, or success messages; verifies RULE-8.
- [ ] **AC-11:** Given keyboard-only interaction, when shopper navigates variant choices, desktop Show More, tablet/phone gallery, populated content sections, and action controls, then controls have meaningful names, visible focus, and operable selection, navigation, expansion, or activation with perceivable state, including gallery expansion and current image position.
- [ ] **AC-12:** Given one product image, when shopper opens product detail on desktop, tablet, or phone, then sole image appears in corresponding gallery layout without unavailable image choices or scrolling to nonexistent images.
- [ ] **AC-13:** Given no designated primary image or no displayable images, when shopper opens product detail, then first available image serves as primary or product-name placeholder appears in Subtitle 1, and product details remain usable. Failed gallery images use the same named fallback.
- [ ] **AC-14:** Given a product that cannot be found or viewed, when shopper opens product detail, then clear product-unavailable message appears without stale details or selectable variants.
- [ ] **AC-15:** Given a product with variants, none selected, and no reduced-motion preference, when shopper activates enabled Add to Bag using pointer or keyboard, then variant-selection block comes into view if needed, choices receive focus without selection, block briefly shakes, and assistive technology receives selection-required announcement without visible error text, price or gallery changes, or bag additions; verifies RULE-8 and RULE-9.
- [ ] **AC-16:** Given missing-selection attention feedback, when shopper selects a variant, then attention state and any static outline clear, selected variant price and matching gallery response appear, and subsequent Add to Bag activation produces no missing-selection feedback or bag action; verifies RULE-3, RULE-4, RULE-8, and RULE-10.
- [ ] **AC-17:** Given multiple product images, when product detail opens on tablet or phone, then primary image appears first at large size with position indicator, every other image is reachable by horizontal scrolling/swiping, and no thumbnail-preview strip is required; verifies RULE-1, RULE-2, and RULE-11.
- [ ] **AC-18:** Given desktop gallery with more than 4 images and only first 4 displayed, when shopper activates Show More, then all images appear in original order, Show Less replaces Show More, and selected variant and price remain unchanged; activating Show Less restores first four images and Show More, retaining keyboard focus on the toggle; verifies RULE-2 and RULE-12.
- [ ] **AC-19:** Given a variant whose image is already visible within desktop viewport, when shopper selects that variant, then price and selected variant update without scrolling, image highlight, or grid reordering; verifies RULE-3 and RULE-4.
- [ ] **AC-20:** Given collapsed desktop gallery and a variant whose image lies beyond first 4, when shopper selects that variant, then gallery expands to all images, scrolls to target only when outside viewport, applies no image highlight, and preserves original image order; verifies RULE-2, RULE-4, and RULE-12.
- [ ] **AC-21:** Given desktop gallery with 4 or fewer images, when shopper opens product detail, then all available images appear with no Show More button; verifies RULE-2 and RULE-12.
- [ ] **AC-22:** Given a product whose variants share same current selling price, when shopper opens product detail without selecting a variant, then one amount appears instead of equal range endpoints, and no variant is selected; verifies RULE-1 and RULE-3.
- [ ] **AC-23:** Given an initial variant price range and a variant with sale price, when shopper selects that variant, then its exact sale price replaces range; selecting another variant without sale price shows that variant's original price; verifies RULE-3.
- [ ] **AC-24:** Given a product with variants, none selected, and reduced-motion preference, when shopper activates Add to Bag, then block does not shake, choices receive focus and static outline, assistive technology receives selection-required announcement, and no visible error text, automatic selection, price or gallery changes, or bag additions occur; verifies RULE-8 and RULE-9.

- [ ] **AC-26:** Given product details are loading, when page renders at desktop/tablet/phone widths, then static ShopSkeleton placeholders match the supplied gallery, name, options, price, actions and collapsed details layout, with a meaningful loading status and no interactive placeholder controls.
- [ ] **AC-27:** Given a published product with gallery images and no legacy image_path, when catalogue loads, then thumbnail shows its primary image, falling back to first gallery image by position and then legacy image when no gallery exists. Description and specification values use body-1 Regular400,16px,0.25px tracking, normal line height and primary text color. Desktop image-grid gaps are8px; narrow swipe gallery remains gap0.

- [ ] **AC-28:** Given a storefront product tile, when shopper views or hovers it, then brand14px Medium/title16px Regular/current20px Medium/original16px Medium use named styles, and fine-pointer hover shows1px outline8px outside without moving content. Clicking image, badge, text, price or empty tile space opens product detail with native link behavior; Bag/Favourite activate only their own actions. Keyboard focus stays visible; loading skeleton has no navigation or hover action.

---

## Assumptions & Open Questions

None â€” all assumptions confirmed.

---
**Status:** Confirmed   Â·   **Created:** 2026-10-20   Â·   **Clarified:** 2026-10-20

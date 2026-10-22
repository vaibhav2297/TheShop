# Execute evidence — Product Detail

**Feature:** `001_product-detail`
**Date:** 2026-10-21

Latest refinement: ProductCard now follows supplied Product Tile3022:23195 typography and outside hover border. Native link/selection button covers image, text and whitespace; Bag/Favourite remain separate. Shared component CSS and its existing class contract own the change.

Desktop buy box now scrolls naturally before sticking at its content end. Native sticky positioning stays inside the product grid; intrinsic height prevents the sidebar stretching to gallery height. Existing page JS measures the actual scrollport and box, using a negative top offset only for content taller than the viewport. Desktop-only24px outer bottom padding leaves clearance below actions when pinned; inter-section spacing remains parent gap. Resize observers/listeners are disposed with the page. Tablet/phone retain normal stacked scrolling.

## Files and phases

| Phase | Changes |
|---|---|
| Domain | Existing entities/value objects reused; unchanged. |
| Application | Previously completed public detail DTOs, mapper, variant resolver, query/validator/handler and repository contract. Unchanged by this request. |
| Infrastructure | Explicit published lookup and shared hydration retained. ProductRecord now left-embeds product_images; ProductMapper resolves primary/first gallery object before legacy image_path. No migration/RLS changes or per-product lookup. |
| Web — prior detail work | `Styles/layouts/_product-detail.scss`: intrinsic-height sticky grid item at desktop breakpoint. `wwwroot/js/productDetail.js`: measured sticky offset, box/scrollport ResizeObserver, viewport/breakpoint updates and disposal. Also corrected responsive loading skeleton, reversible Show More/Show Less and resource/up-chevron, and description/specification text styles. No scroll handlers, fixed overlays, duplicate controls, inner scrollbar or new dependency. |
| Web — tile refinement | `Components/Products/ProductCard.razor.cs`: whole-tile contract/class. `Styles/components/_producttile.scss`:14px brand,16px name,8px gaps,1px outside hover rectangle, stretched native link, token-based full-tile keyboard focus and separate button stacking. Existing Razor/image/icon-button composition reused. |
| Artifacts | Spec FR-11/AC-25–28 and plan TASK-016–018 record the user-requested behavior. Local browser journal, fresh Figma exports, captures and reviews under `evidence/`. |

## Current validation

- [Current solution build](evidence/tile-build.log): 0 errors; existing package/analyzer warnings remain. Feature tests remain with explicit Test stage.
- Infrastructure/Web scope and design-rule gates clean; `git diff --check` clean. Current solution build:6 existing package warnings,0 errors; no feature tests run.
- [Browser observations](evidence/sticky-inspection.json), [pinned desktop screenshot](evidence/sticky-desktop.png): at1440x600, scroll0/50 moves box top170/120 together with gallery; at scroll350/500 box bottom stays600 while gallery top moves -180/-330. Box height675px includes24px bottom padding; action row ends576px, leaving24px clearance. Expanded gallery scroll650/1000 keeps that clearance. Product end releases the box before the footer; reverse scrolling restores original position.
- Short buy box pins below shell top106. Resize to500px viewport updates outer bottom to500 and action bottom to476, preserving24px. Variant-free and short-gallery cases respect available content. Widths1023/834/390 use static positioning and0px bottom padding;1024 uses sticky. Focus after Add to Bag remains on visible5mg control at y182–214 inside viewport106–600; selected hidden image still reveals correctly. Zero document overflow and zero unhandled browser errors.
- [Graph update](evidence/tile-graph.log): `graphify update .` completed after code changes;10,235 nodes /25,016 edges /636 communities. Existing SQL parser/non-code extraction warnings remain unrelated to feature checks.

## Visual evidence

Earlier visual files were absent at this request's start. Re-exported all 21 distinct Figma frames through live Desktop Bridge on 2026-10-21 at native1x; restored exact fixture artwork from prepared local storage and retained provenance. Recreated all34 planned captures against current source/spec/plan. No reference stretching, ignored regions or screenshot artwork in implementation.

Approved element-specific differences remain in plan: unselected initial chips and aggregate price; eight-image track segments; variant/image5 action state; optional announcement absent (32px desktop/tablet,48px phone); native normal text metrics; no reviews/recommendations; reduced-motion outline. Current price uses the inspected 600 face,48px/24px sizes,0.25px tracking and12px separation. Current box gaps42px desktop/tablet and32px phone remain intact. Current Figma description uses body-1/primary text. Specification values use body-1 Regular400 per latest user instruction, overriding Figma Medium500. Desktop gallery references re-exported after live8px-grid inspection. Native details bounds466/487/678px versus Figma458/478/662px preserve named AUTO/normal line metrics; all content remains visible. New sticky scroll state follows the user's instruction and is verified by browser geometry, without claiming a designed Figma scroll state.

Final [visual gate](evidence/visual-gate.log): clean,34/34 current reviewed targets. Final source/spec/plan captures retain all four inspected image hashes; [comparison journal](evidence/final-image-comparison.json). Seven updated/new comparisons inspected in latest pass;27 unchanged comparisons retain byte-identical previously inspected images. Refreshed catalogue references after current component changes; desktop content crop begins at y793, thumbnail native rounding uses349x348px without stretching. Full references retained. No material unresolved mismatch or waiver.

| State | Desktop review | Tablet review | Phone review |
|---|---|---|---|
| Initial | [PASS](evidence/visual/detail-desktop/review.md) | [PASS](evidence/visual/detail-tablet/review.md) | [PASS](evidence/visual/detail-phone/review.md) |
| Expanded details | [PASS](evidence/visual/detail-expanded-desktop/review.md) | [PASS](evidence/visual/detail-expanded-tablet/review.md) | [PASS](evidence/visual/detail-expanded-phone/review.md) |
| Fifth image | [PASS](evidence/visual/detail-many-desktop/review.md) | [PASS](evidence/visual/detail-many-tablet/review.md) | [PASS](evidence/visual/detail-many-phone/review.md) |
| No image | [PASS](evidence/visual/detail-no-image-desktop/review.md) | [PASS](evidence/visual/detail-no-image-tablet/review.md) | [PASS](evidence/visual/detail-no-image-phone/review.md) |
| Variant-free | [PASS](evidence/visual/detail-no-variants-desktop/review.md) | [PASS](evidence/visual/detail-no-variants-tablet/review.md) | [PASS](evidence/visual/detail-no-variants-phone/review.md) |
| Unavailable | [PASS](evidence/visual/detail-unavailable-desktop/review.md) | [PASS](evidence/visual/detail-unavailable-tablet/review.md) | [PASS](evidence/visual/detail-unavailable-phone/review.md) |
| Catalogue entry | [PASS](evidence/visual/detail-entry-desktop/review.md) | [PASS](evidence/visual/detail-entry-tablet/review.md) | [PASS](evidence/visual/detail-entry-phone/review.md) |
| Selected price | [PASS](evidence/visual/detail-price-desktop/review.md) | [PASS](evidence/visual/detail-price-tablet/review.md) | [PASS](evidence/visual/detail-price-phone/review.md) |
| Selection attention | — | — | [PASS](evidence/visual/detail-attention-phone/review.md) |
| Loading skeleton | [Review](evidence/visual/detail-skeleton-desktop/review.md) | [Review](evidence/visual/detail-skeleton-tablet/review.md) | [Review](evidence/visual/detail-skeleton-phone/review.md) |
| Catalogue thumbnail | [PASS](evidence/visual/detail-thumbnail-desktop/review.md) | [PASS](evidence/visual/detail-thumbnail-tablet/review.md) | [PASS](evidence/visual/detail-thumbnail-phone/review.md) |
| Tile hover corner | [PASS](evidence/visual/detail-tile-hover-desktop/review.md) | [PASS](evidence/visual/detail-tile-hover-tablet/review.md) | [PASS](evidence/visual/detail-tile-hover-phone/review.md) |

## Four requested fixes —2026-10-21

- Skeleton: inline static ShopSkeleton composition follows supplied new frames3116:43231/43768/44257. Four desktop squares; one narrow square; responsive heading lines, options, price/actions and collapsed detail rows. Actual product requests are held pending for captures; no synthetic DOM or app state. Loading buy box stays static.
- Gallery: Show More becomes Show Less after explicit or variant-driven expansion. Collapse restores four images; keyboard focus stays on toggle, choices and price remain unchanged. Button remains desktop-only and aria-expanded follows state. [Screenshot](evidence/gallery-show-less.png).
- Catalogue: gallery-only fixture has null image_path and a primary side.png object; browser loads500x500 artwork through existing ShopImage. Optional gallery relation uses LEFT join so no-image products remain visible. Separate legacy-only fixture still resolves blue.png. No external placeholder replaces a valid gallery image.
- Text: current Figma body-1 Regular400,16px,0.25px tracking,normal line height and primary rgb(23,23,23). Specification values use body-1 Regular400,16px per latest explicit user instruction.
- [Current browser journal](evidence/fixes-inspection.json):8 images expand,4 remain after keyboard collapse, focus retained; selected5mg/Blackberry Cranberry and sale/original price retained. No overflow at320/390/767/768/834/1023/1024/1440px; no browser errors. Loading placeholders have no interactive controls.
- Sticky observations refreshed against final build: gallery keeps scrolling while action row ends576px in600px viewport, preserving24px clearance; zero browser errors.

## Gallery spacing / body-1 refinement

- `_product-detail.scss`: loaded desktop grid consumes `--shop-space-2` (8px); specification `dd` consumes `text-style.apply(body-1)`. Skeleton keeps its independently designed16px grid; narrow swipe gap remains0.
- Current browser journal records8px row/column gaps,458px tiles, and specification values Space Grotesk16px/400,0.25px tracking,normal line height,primary rgb(23,23,23). Responsive/keyboard/thumbnail observations remain clean.
- [Build](evidence/tile-build.log), Web scope and design-rule gates pass; graph updated after source change. Current34-surface capture refresh recorded in `tile-captures.log` plus native special captures.

## Product tile refinement

- Live Default2009:5162 / Hover3123:44757 exports: brand subtitle-2 14px/500; title body-1 16px/400; current h6 20px/500; original subtitle-1 16px/500,tracking0.25px,AUTO.8px content gaps; removed old negative title margin. Ratings remain excluded without product rating data.
- Hover rectangle:1px Primary inside stroke at8px outside card. Fine-pointer hover only; no size change, scale, shadow or motion. Full-card screenshots retained in `tile-hover-{desktop,tablet,phone}.png`;64px corner comparisons prove regional border/color/offset rather than whole-component pixel parity.
- [Current tile journal](evidence/tile-inspection.json): image, image padding, brand, name, price, empty content and image/content gap all hit native A and navigate to detail. Bag/Favourite hit BUTTON and remain on listing with one browser page. Ctrl-click opens detail in new tab; original page remains listing. Keyboard Enter navigates; keyboard focus is solid2px with3px offset across tile. Touch tap navigates; coarse-pointer hover border stays transparent. Skeleton has zero interactive elements and no hover pseudo-content. Zero browser errors.
- Native content bounds102/102/123px versus Figma100/100/120px retain named normal-line metrics. Desktop screenshot rounds fractional bounds to349x103px; tablet246x102px and phone171x123px. Wrapping and complete price content inspected.
- A late stylesheet save invalidated first capture fingerprints. Preserved current workspace, rebuilt/restarted owned host, then refreshed captures against stable source. `tile-source-snapshot.json` records diagnostic hashes; no gate relaxation or stale-source rebinding.

## Handoff

Accepted risk unchanged: independently hydrated detail reads may overlap administrative edits. No transactional snapshot or live synchronization added. Earlier access checks were recorded in the prior Execute pass; this styling/geometry change does not alter that path, and those historical logs are no longer present. Current fresh checks above are distinguished from that history.

Formal regression coverage, including AC-25, remains for `$theshop-test 001_product-detail`. No feature tests authored or run here. Bag/favourite persistence remains excluded. No implementation blocker.

## Product-name placeholders —2026-10-21

User correction complete. ProductDetail passes product.Name to both empty-gallery ShopImage and carousel PlaceholderLabel. Shared image label consumes Subtitle1 tokens: Space Grotesk16px/500,0.25px tracking,normal line-height. Catalogue already supplies product names; failed tile images inherit corrected typography. Native error handling, reserved geometry and meaningful alt text remain intact.

Source delta: `src/TheShop.Web/Pages/Products/ProductDetail.razor`; `src/TheShop.Web/Styles/components/_image.scss`. AC-13 and component mapping updated. Re-fetched no-image Figma desktop/tablet/phone nodes and exports; inspected live Subtitle1 placeholder component. No new public API, resources, dependencies or business behavior.

Validation: baseline and final solution builds pass,6 existing package warnings,0 errors. Design rules, Web scope, plan and visual gates clean. Twelve browser observations cover empty gallery, failed carousel image and failed tile image at1440/834/390/320px; every label equals product name, no overflow or browser errors. Evidence: [placeholder-inspection.json](evidence/placeholder-inspection.json), [build](evidence/placeholder-build.log), [visual gate](evidence/visual-gate.log).

All34 planned targets rebuilt/captured after final source/spec/plan edits. Three changed no-image comparisons manually inspected against refreshed live references, including actual/overlay/difference and geometry: [desktop](evidence/visual/detail-no-image-desktop/review.md), [tablet](evidence/visual/detail-no-image-tablet/review.md), [phone](evidence/visual/detail-no-image-phone/review.md). Other31 four-image sets are byte-identical to prior manually inspected sets; original findings retained with current capture hashes. [Comparison record](evidence/placeholder-image-comparison.json). Failed-image diagnostic screenshots also inspected. All34 current reviews pass; no new waiver.

Graph updated:10235 nodes,25020 edges,642 communities. No formal feature tests authored/run; Test remains next. Owned5225 browser host stopped after evidence; user5218 host untouched.

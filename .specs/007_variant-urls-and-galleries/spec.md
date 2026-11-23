# Variant URLs and Galleries

**Feature:** `007_variant-urls-and-galleries`

## 1. Problem Statement

Customers selecting variants still see images belonging to other variants. Scrolling to one pinned image leaves product details cluttered and can misrepresent selected packaging or appearance. Shared product links cannot reopen an exact variant.

**Solution (one line):** Give each variant a shareable address and assigned gallery, keeping displayed options, images, price, and availability aligned.

### Scope

**In scope:**

- Distinct readable addresses using product and variant slugs (readable names within URLs).
- Direct variant selection through links; switching variants updates address and displayed details together.
- Variant galleries with ordered images; first image is variant cover/primary. Reuse uploaded images across variants.
- Admin assignment, bulk assignment, ordering, and removal of image assignments; no separate variant cover selection.
- Default variant on parent entry; browser history, refresh, and existing product-link compatibility.
- Existing content transition, missing images, unavailable variants, and accessible gallery interaction.

**Out of scope:**

- Separate product records for every variant; changes to product grouping, pricing rules, or inventory rules.
- Cart, checkout, payment, review, or recommendation features.
- Image generation, video, image editing, or automatic recognition of matching photos.
- Manual address editing, address rename management, search-engine indexing policy, or catalogue redesign.
- Broad product-page layout redesign or new permissions.

### Actors & Access

| Actor | May | Must not |
|---|---|---|
| Customer, guest or signed in | Browse published products; open, select, and share variants; browse matching galleries. | Edit catalogue content or view unpublished products through guessed links; see unavailable state for inaccessible content. |
| Admin with existing product-management access | Manage variant image assignments and order through existing product management. | Bypass product ownership or publication restrictions; invalid changes show clear errors. |

## 2. Functional Requirements

1. **FR-1:** Each parent product and exact variant has a distinct readable address using product and variant slugs. Variants remain grouped under their parent product. Example: `/products/protein-powder` identifies parent; `/products/protein-powder/vanilla-500g` identifies exact variant.
2. **FR-2:** Opening a published variant address preselects its complete option combination and shows matching price, availability, SKU, and assigned gallery.
3. **FR-3:** Selecting another complete variant updates address, selected options, purchase details, and gallery together without full page reload or automatic page scrolling.
4. **FR-4:** Gallery contains only images assigned to displayed variant, including thumbnails, expanded views, and mobile swipe navigation. First image in saved variant order is its cover/primary. Product primary serves product listing thumbnails and appears in a variant gallery only when explicitly assigned.
5. **FR-5:** Admins can assign multiple uploaded product images to a variant, order them, and remove assignments. Moving an image to first position makes it cover; no separate cover selector. Uploading through variant editor adds image to shared product image library and assigns it to edited variant, without automatically assigning it elsewhere.
6. **FR-6:** Admins can assign selected images to multiple variants in one action without uploading duplicates. Images explicitly deselected from the edited variant are removed from the chosen variants; other existing assignments retain their order and cover unless the cover was removed. During the current edit session, dialogs for variants sharing the same option value show that group's last applied Apply To choice; Cancel leaves it unchanged.
7. **FR-7:** Parent product address opens with first available variant in existing variant order clearly selected and its gallery displayed. If none available, select first variant and show unavailable state.
8. **FR-8:** Refresh, copied links, and browser Back/Forward restore exact variant selection and matching content.
9. **FR-9:** Variant without assigned images shows neutral placeholder. Broken assigned images show fallback within that gallery; unrelated product images never substitute.
10. **FR-10:** Existing product links remain usable and open the same product with default-variant behaviour. Products without variants retain their product gallery.
11. **FR-11:** All existing image uploads remain reusable during transition. Each valid variant pin becomes that variant's sole initial gallery image and therefore cover; unpinned variants start without assigned images.
12. **FR-12:** Invalid, deleted, mismatched, or unpublished variant links show clear unavailable state. A published but unavailable variant retains its own identity and gallery.
13. **FR-13:** Customers can select variants and navigate gallery with keyboard and touch; assistive technology identifies selected variant and gallery position.
14. **FR-14:** Generate readable product and variant slugs from initial names; collisions receive a distinguishing suffix. Saved slugs and addresses remain stable after product or option label changes.

## 3. Functional Behaviors

### Behavior 1: Open an exact variant

- **User does:** Opens a shared Vanilla 500g address.
- **User sees:** Vanilla and 500g selected; matching SKU, price, availability, and Vanilla 500g photos. Chocolate photos do not appear.

### Behavior 2: Switch variant

- **User does:** Selects Chocolate 500g from available variant choices.
- **User sees:** Chocolate 500g address and details; its cover appears first. Page stays at current browsing position.

### Behavior 3: Open parent product

- **User does:** Opens parent product address or existing product link.
- **User sees:** First available variant in existing order selected with matching gallery and address. If all unavailable, first variant appears unavailable. Products without variants show product gallery.

### Behavior 4: Manage gallery

- **User does:** Assigns or uploads images, orders them, and saves variant.
- **User sees:** Saved selection and order reflected on that variant's page; first image is cover/primary. Reordering changes cover automatically. Uploaded images enter shared product image library. Removing assignment leaves image available for other variants.

### Behavior 5: Reuse images

- **User does:** Selects product images and several variants for bulk assignment.
- **User sees:** Images available in each selected variant gallery without duplicate uploads. Explicitly deselected images are removed from the chosen variants; other existing assignments remain.

### Behavior 6: Restore selection

- **User does:** Copies address, refreshes, or uses browser Back/Forward after switching variants.
- **User sees:** Variant identified by address with corresponding gallery and purchase details.

### Behavior 7: Encounter missing content

- **User does:** Opens variant lacking assigned images or follows a deleted variant link.
- **User sees:** Respectively, neutral image placeholder with valid variant details, or unavailable state without another variant being silently substituted.

## 4. Constraints

- Customer-facing text remains English.
- Shared product information remains consistent across variants.
- Product image library contains all uploaded images; each variant gallery contains only its assigned images in its own order.
- Product primary and variant cover have separate roles. Changing variant order does not change product listing thumbnail or other variant orders.
- Address, visible selection, gallery, and purchase details must agree; do not present mixed variant content during changes.
- Gallery remains usable on desktop and mobile; selection does not move keyboard focus unexpectedly.
- Existing catalogue-management permissions and publication controls apply.
- Parent entry selects a default only when no exact variant was requested.
- Default selection follows existing order and availability: first available variant wins; first variant remains visible when all unavailable.
- Existing valid pins seed sole initial gallery images and covers; absent pin means empty gallery. All uploaded images remain reusable.
- Initial names generate readable addresses; distinguishing suffixes resolve collisions. Saved addresses survive display-name changes.

### Business Rules

| ID | Rule | Outcome when violated |
|---|---|---|
| **RULE-1** | Every variant address identifies exactly one variant under its own parent product. | Collision must be resolved before address is exposed; wrong-parent or unknown address shows unavailable state. |
| **RULE-2** | Gallery may contain only explicitly assigned images belonging to that parent product. | Reject foreign-product assignments; exclude all unassigned images from every gallery view. |
| **RULE-3** | Each image appears at most once in a variant gallery; first image in saved variant order is its sole cover/primary. No independent cover selection exists. | Prevent duplicate assignments; always derive displayed cover from first position. Empty gallery has no cover. |
| **RULE-4** | Removing an assignment affects only selected variant; removing its cover promotes first remaining image in saved order. | Preserve other assignments; empty gallery has no cover and shows placeholder. |
| **RULE-5** | Only published products are visible through parent or variant addresses; unavailable variants cannot be presented as purchasable. | Inaccessible product shows unavailable state; unavailable variant shows its own details with unavailable purchase state. |
| **RULE-6** | Only existing product-management actors may modify galleries. | Deny unauthorized changes; retain saved assignments. |
| **RULE-7** | A saved variant address stays attached to its original variant while that variant exists. | Name edits must not invalidate links; deleted variant links show unavailable state. |
| **RULE-8** | Bulk assignment appends missing images in selected order and removes images explicitly deselected in the edited variant from chosen variants. | Preserve other existing assignments and their order; skip already assigned images. If the cover is removed, the next remaining image becomes cover. |
| **RULE-9** | Deleting a product image removes its assignments from all affected variants without deleting those variants. | Promote remaining covers under RULE-4; show placeholder where no images remain. |

## 5. Edge Cases & Error Handling

- **Edge case:** Variant has no images. **User experience:** Neutral placeholder; correct variant details remain visible.
- **Edge case:** Assigned image cannot display. **User experience:** Image fallback; other assigned images remain browsable; no unrelated substitute.
- **Edge case:** Direct link targets unavailable variant while another variant is available. **User experience:** Requested variant stays selected with unavailable state; customer may choose another.
- **Edge case:** All variants unavailable. **User experience:** Parent entry shows first variant, clearly unavailable.
- **Edge case:** Published product has no variants. **User experience:** Parent gallery and existing product details remain usable.
- **Edge case:** Exact variant is deleted, unknown, or belongs to another product. **User experience:** Unavailable state; no silent default substitution.
- **Edge case:** Product becomes unpublished after link was shared. **User experience:** Unavailable state; unpublished content remains hidden.
- **Edge case:** Product image is deleted while used by multiple variants. **User experience:** Affected galleries lose that image; remaining covers or placeholders appear.
- **Edge case:** Gallery save fails. **User experience:** Clear failure message; editing choices remain available for retry; previously saved storefront content remains intact.
- **Edge case:** Rapid variant changes finish in different order. **User experience:** Latest selected variant controls address and visible content; earlier selection cannot overwrite it.
- **Edge case:** Variant choice would form a nonexistent combination. **User experience:** Choice cannot become a completed selection; clearly identify unavailable combination without presenting mismatched purchase details.

## 6. Acceptance Criteria

- [ ] **AC-1:** Given two variants of one published product, when customer opens each variant address, then each selects its exact options and shows matching SKU, price, availability, and assigned images.
- [ ] **AC-2:** Given Vanilla and Chocolate have different galleries, when customer selects Chocolate, then address and all variant details update together, Chocolate cover appears first, and Vanilla images disappear without full reload or page scrolling.
- [ ] **AC-3:** Given selected variant has multiple images, when customer browses desktop gallery, thumbnails, expanded views, or mobile swipe gallery, then only assigned images appear in saved order with cover first (RULE-2).
- [ ] **AC-4:** Given authorized admin and uploaded product images, when admin assigns images, orders them, and saves, then variant page shows saved order with first image as cover/primary and no separate cover selection (RULE-3).
- [ ] **AC-5:** Given several variants with existing galleries, when admin bulk-applies a selection, then missing images append in selected order and images explicitly deselected from the edited variant are removed only from chosen variants; all other assignments keep their order and no duplicates appear (RULE-8).
- [ ] **AC-6:** Given one image assigned to two variants, when admin removes assignment from one variant, then other variant retains image and upload remains reusable (RULE-4).
- [ ] **AC-7:** Given a nonempty gallery, when admin removes its cover assignment, then first remaining image becomes cover; when final assignment is removed, then neutral placeholder appears (RULE-4).
- [ ] **AC-8:** Given a product with ordered available and unavailable variants, when customer opens parent address, then first available variant is visibly selected with its address and gallery; if all unavailable, then first variant appears unavailable.
- [ ] **AC-9:** Given customer switches between two variants, when customer uses Back, Forward, refresh, or opens copied address, then matching variant selection, gallery, and purchase details are restored.
- [ ] **AC-10:** Given variant has no assigned images, when its page opens, then neutral placeholder appears without another variant's photos; given broken assigned image, when browsed, then fallback appears and remaining assigned images stay usable.
- [ ] **AC-11:** Given existing product link, when customer opens it after release, then same product remains reachable with default variant; given product without variants, when opened, then original product gallery remains visible.
- [ ] **AC-12:** Given existing uploads and a valid pinned image, when feature becomes available, then uploads remain usable and pinned image becomes variant's initial cover and sole image; unpinned variants receive no inferred image assignments.
- [ ] **AC-13:** Given unknown, deleted, wrong-parent, or unpublished variant address, when opened, then unavailable state appears without exposing restricted content or silently selecting another variant (RULE-1, RULE-5).
- [ ] **AC-14:** Given published unavailable variant and another available variant, when unavailable variant's direct link opens, then requested variant and its gallery remain selected with unavailable purchase state (RULE-5).
- [ ] **AC-15:** Given guest or customer without product-management access, when gallery modification is attempted, then change is denied and saved gallery remains unchanged (RULE-6).
- [ ] **AC-16:** Given image belonging to another product, when admin attempts assignment, then change is rejected with clear guidance; duplicate image assignment cannot create duplicate gallery entries (RULE-2, RULE-3).
- [ ] **AC-17:** Given product or option name changes, when customer reopens saved variant address, then same variant remains reachable; given matching generated names, when addresses are created, then distinct addresses identify correct variants (RULE-1, RULE-7).
- [ ] **AC-18:** Given keyboard or assistive-technology user, when selecting variant and navigating gallery, then controls have meaningful names, selection and position are conveyed, and focus remains usable; touch navigation provides same image scope.
- [ ] **AC-19:** Given several quick variant selections, when content settles, then latest selection alone determines address, options, gallery, and purchase details.
- [ ] **AC-20:** Given product image used by multiple variants, when admin deletes image, then affected variants remain, image disappears from their galleries, and remaining covers or placeholders appear (RULE-9).
- [ ] **AC-21:** Given admin edits gallery and save fails, when failure is shown, then editing choices remain available for retry and previous saved storefront gallery remains intact.
- [ ] **AC-22:** Given nonexistent option combination, when customer attempts selection, then unavailable combination is clearly identified and cannot be presented as a completed purchasable variant.
- [ ] **AC-23:** Given variant gallery ordered A, B, C, when admin moves B first and saves, then B becomes cover and gallery follows saved order; product primary and other variant orders remain unchanged (RULE-3).
- [ ] **AC-24:** Given product primary and two variants, when admin uploads an image through one variant editor, then image enters shared product image library and edited variant's assignments only; product primary remains unchanged and appears in variant galleries only when explicitly assigned.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-11-17   ·   **Clarified:** 2026-11-17

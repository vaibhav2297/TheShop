# Implementation Plan — Shop Image

**Feature:** `shop-image`

Companion to `.specs/shop-image/spec.md`. Technical HOW for confirmed FR-1–FR-7.

## 1. Objective

Provide ten reusable image treatments in Web. Apply treatments to existing placements; preserve source assets, access checks, and surrounding actions. Reserve frame geometry before image arrival and show named placeholders for missing or failed sources.

## 2. Tech Stack

- **Web:** existing .NET 10 Blazor WebAssembly, MudBlazor 9.7.0, SCSS through AspNetCore.SassCompiler.
- **Browser:** CSS `aspect-ratio`, `object-fit`, media queries; small JS interop module for image failure events.
- **Tests:** existing bUnit 2.7.2, xUnit, FluentAssertions in `TheShop.Web.Tests`; browser checks for actual geometry.
- **Domain, Application, Infrastructure, persistence:** no changes.

## 3. High-level Architecture

Existing queries supply image URLs and names. Presentation stays entirely in Web; no new MediatR request or repository needed.

```text
Existing page / ProductCard / ShopImageUpload
    supplies source, name, preset, optional mobile source
ShopImage (MudComponentBase)
    reserves MudStack frame; renders MudImage or MudText placeholder
SCSS selects frame ratio and responsive artwork
Browser image failure event updates only corresponding source state
Existing page actions and authorization remain authoritative
```

Reuse justified by ProductCard and three administration lists, plus edit and preview placements. New component owns image presentation only.

## 4. Data Model

### Domain entities & value objects

None added or modified. Ratios are presentation concerns.

### DTOs (Application → Web)

Keep existing `ProductSummaryDto.ImageUrl`, admin product `PrimaryImageUrl`, category `ImageUrl`, and brand `LogoUrl` contracts. No mobile artwork field added to DTOs or storage.

### Web presentation contract

Planned files under `src/TheShop.Web/Components/Common/`:

- `ShopImagePreset.cs`: enum values in table below.
- `ShopImage.razor` and `.razor.cs`: `MudComponentBase`; parameters `Src` (`string?`), `MobileSrc` (`string?`), `Preset` (`ShopImagePreset`, default `ProductCard`), `Alt` (`string`, default empty), `PlaceholderLabel` (`string`, required at call sites).
- Inherited `Class`, `Style`, `UserAttributes` forwarded to root `MudStack`. Root class uses `CssBuilder`; style uses `StyleBuilder`, consumer values last.
- Source-failure state belongs to component, separately for desktop/mobile source. Reset when corresponding source changes; dispose event subscriptions on removal.
- `BrandLogo` callers reserve width and height before load; contain whole logo inside allocation without cropping or stretching. Empty space allowed.

| Preset | Frame | Fit | Responsive source |
|---|---|---|---|
| `ProductCard` | 1:1 | Contain | `Src` |
| `ProductDetail` | 1:1 | Contain | `Src` |
| `Thumbnail` | 1:1 | Contain | `Src` |
| `CategoryTile` | 1:1 | Cover, center | `Src` |
| `CategoryBanner` | 16:5 desktop; 4:5 mobile | Cover, center | `MobileSrc` when supplied on mobile |
| `Hero` | 16:9 desktop; 4:5 mobile | Cover, center | `MobileSrc` when supplied on mobile |
| `MobileBanner` | 4:5 | Cover, center | `Src` |
| `Editorial` | 4:3 | Cover, center | `Src` |
| `BrandLogo` | Caller-reserved space; no imposed source ratio | Contain | `Src` |
| `SocialSharing` | 40:21 | Contain | `Src` |

### Database tables and indexes

None added or modified.

## 5. Core Design Decisions

1. **Decision:** compose existing MudBlazor components, not raw image or text primitives.
   - **Why:** Rules 14, 16, 23–25; repeated ratio and failure behavior needs one owner.
   - **Rejected:** scattered inline fixes; fallback and geometry would drift between callers.
2. **Decision:** reserve ratio through CSS on root; image fills reserved frame with preset fit and centered position.
   - **Why:** FR-2, FR-3, FR-5 and RULE-1–RULE-3. Width is `100%` within caller allocation, with `max-width:100%` and `min-width:0`.
   - **Rejected:** universal pixel widths or intrinsic image height; overflow and layout shifts remain possible.
3. **Decision:** SCSS map generates preset classes in `Styles/components/_image.scss`; import from `Styles/TheShop.scss`.
   - **Why:** Rules 26–28; MudBlazor parameters supply fit/position, SCSS supplies ratios and responsive geometry.
   - **Rejected:** per-page CSS, inline ratio strings, or new theme tokens for existing colors.
4. **Decision:** CSS switches desktop/mobile image branches at 600 CSS pixels: below 600 uses 4:5; 600 and above uses desktop ratios. Desktop source is mobile fallback only when `MobileSrc` absent.
   - **Why:** FR-4; resize works without delayed breakpoint interop. Dedicated mobile source failure shows named placeholder, not an unapproved alternate crop.
   - **Rejected:** new mobile-artwork persistence, `<picture>` primitive, and new destination pages; outside scope or Rule 14.
5. **Decision:** show `MudText` named placeholder inside same reserved root on absent/failed source. Never substitute storefront logo for missing product artwork.
   - **Why:** confirmed FR-5 supersedes current `ProductCard` logo fallback. Repository inspection finds logo fallback, not shared named-placeholder component; compose specified name treatment here.
   - **Rejected:** collapse frame, broken-image icon, or generic unnamed placeholder.
6. **Decision:** use small `wwwroot/js/shopImage.js` event bridge with existing `MudImage`.
   - **Why:** pinned MudImage handles `onerror` internally and exposes no public failure callback. Capture image error events through listener on component root; identify source branch, notify component, and handle already-completed failed images during attachment.
   - **Rejected:** assume `UserAttributes["onerror"]` overrides internal handler, modify MudBlazor, or add custom image primitive.
   - **Source:** [MudImage 9.7.0 code](https://raw.githubusercontent.com/MudBlazor/MudBlazor/v9.7.0/src/MudBlazor/Components/Image/MudImage.razor.cs), [markup](https://raw.githubusercontent.com/MudBlazor/MudBlazor/v9.7.0/src/MudBlazor/Components/Image/MudImage.razor).
7. **Decision:** meaningful `Alt` comes from existing typed English resource formatting or identifiable product/brand name. Empty `Alt` means decorative; placeholder label still remains visible, with redundant decorative description hidden from assistive technology.
   - **Why:** FR-6; Rules 11–12. No new prose strings needed when callers supply existing names/labels.
   - **Rejected:** hardcoded generic descriptions, added locale switching, or duplicate image keyboard targets.

## 6. Core Functional Flow

### Flow 1: Browse images

1. Existing page loads through current query and permission checks.
2. Caller supplies preset, URL, description, and name/label.
3. Component reserves frame immediately and uses preset fit.
4. Existing buttons, links, badges, selection, and upload controls remain outside image component.

### Flow 2: Change available screen space

1. Root follows caller allocation; ratio sets height before image decode.
2. CSS media query selects 4:5 for `Hero` and `CategoryBanner` below 600 CSS pixels; desktop ratios apply at 600 and above.
3. Mobile branch uses supplied artwork, otherwise desktop URL. Source ratio mismatch crops centrally.
4. `BrandLogo` remains contained within stable caller width/height; source proportions stay intact.

### Flow 3: View unavailable imagery or use assistive technology

1. Empty/whitespace source renders named placeholder immediately.
2. JS bridge reports failed load; component replaces affected image branch inside unchanged frame.
3. Source replacement resets failure state; stale events for prior URLs cannot invalidate replacement.
4. Component adds no links, buttons, `tabindex`, spinner, or page busy state. Existing `BusyState`, `BusyKeys`, routes, and authorization are unchanged.
5. English-only README policy governs spec's historical language-change wording; no language switch added.

## 7. Development Plan

### Step 1 — Domain

Skipped: no domain impact.

### Step 2 — Application

Skipped: no commands, validators, handlers, DTO changes, or error keys.

### Step 3 — Application contract checkpoint

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| Existing image URL/name DTO fields | Application | Web | Stable |
| Existing queries and permission outcomes | Application | Web | Stable |

### Step 4 — Infrastructure

Skipped: no storage, repository, migration, upload processing, or RLS changes.

### Step 5 — Web

**Depends on:** resolved plan and stable Application contract checkpoint.

- [ ] **TASK-001** — Add `ShopImagePreset`, MudBlazor-based `ShopImage`, generated `_image.scss` preset geometry, responsive branches, and `shopImage.js` failure bridge. Document public contract. Add focused `Components/Common/ShopImageTests.cs` for presets, missing/failed sources, replacement, branch isolation, forwarding, and disposal.
- [ ] **TASK-002** — Integrate `ProductCard` and `ManageProducts` using `ProductCard`/`Thumbnail`. Update `_producttile.scss` broad image selectors so they cannot override contain behavior; preserve padding, badges, cart/wishlist/selection callbacks. Extend `ProductCardTests` and `ManageProductsTests`.
- [ ] **TASK-003** — Integrate `ManageCategories` and `EditCategory` as whole-image admin `Thumbnail`; `ManageBrands` and `EditBrand` as `BrandLogo`. Preserve current avatar/edit allocations and remove/delete controls. Extend matching admin page tests.
- [ ] **TASK-004** — Integrate `ShopImageUpload` previews and `VariantImageDialog` as `Thumbnail`; brand upload callers select `BrandLogo` via new optional `PreviewPreset` parameter. Keep existing `PreviewSize` allocation, file labels, selection, upload validation, and pin/remove actions. Convert app bar/footer/auth logos to `BrandLogo` inside their existing reserved dimensions. Extend upload and affected caller tests.
- [ ] **TASK-005** — Add browser geometry checks using deterministic tall/wide product, logo, photography, exact-ratio artwork, delayed-load, and failed-source fixtures. Exercise all ten presets in test-only composition; no production demo route. Check 375, 768, 1440 pixel widths, breakpoint boundaries, and orientation resize. Measure frame dimensions and adjacent content positions before/after arrival/failure. Verify keyboard actions and denied admin content with existing fixtures.

**Completion gate:** Web builds; bUnit tests pass; actual browser geometry proves ratios, centered crops, whole-image fit, stable frames, and no overflow. MudBlazor-only; typed English resources; Class/Style forwarding; stable contracts; existing denied experience preserved. No Figma-dependent evidence requirement.

### Step 6 — Integration & pipeline

**Depends on:** Web tasks complete.

- [ ] **TASK-006** — Run solution build, focused Web tests, design rules, and `graphify update .` after code changes. Record AC evidence for subsequent `$theshop-test shop-image` and `$theshop-verify shop-image`; stages remain separate explicit invocations.

**Completion gate:** planned checks pass; each AC has behavioral evidence; no unplanned production screens or persistence changes.

### Deviation procedure

- **Accept:** equivalent behavior using verified existing conventions, same layer boundaries, no scope expansion or weaker access checks.
- **Reject:** business changes, new destinations, custom UI primitives without Rule 14 approval, or silent contract changes.
- **Contract change:** stop dependent work, update checkpoint and affected TASK ids, then resume only after contract re-frozen. Preserve task IDs; append new IDs.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: square whole product image | TASK-001, TASK-002, TASK-005 |
| AC-2: constrained square thumbnails | TASK-001, TASK-002, TASK-003, TASK-004, TASK-005 |
| AC-3: square category photography crop | TASK-001, TASK-005 |
| AC-4: desktop banner/hero ratios | TASK-001, TASK-005 |
| AC-5: dedicated mobile artwork | TASK-001, TASK-005 |
| AC-6: absent mobile artwork fallback | TASK-001, TASK-005 |
| AC-7: editorial 4:3 | TASK-001, TASK-005 |
| AC-8: original logo proportions | TASK-001, TASK-003, TASK-004, TASK-005 |
| AC-9: social composition 40:21 | TASK-001, TASK-005 |
| AC-10: responsive frame dimensions | TASK-001, TASK-002, TASK-003, TASK-004, TASK-005 |
| AC-11: stable named placeholders | TASK-001, TASK-002, TASK-003, TASK-004, TASK-005 |
| AC-12: English descriptions/decorative semantics | TASK-001, TASK-002, TASK-003, TASK-004, TASK-005 |
| AC-13: existing keyboard actions | TASK-002, TASK-003, TASK-004, TASK-005 |
| AC-14: access restrictions | TASK-002, TASK-003, TASK-005, TASK-006 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

None: no new business input. Component parameters describe presentation; defined enum presets only.

### Domain exceptions

None.

### Result.Fail error keys (new entries in Strings.resx)

None. Image failures produce local named placeholder, not business error toast. Reuse `Strings.Product_ImageAlt`, existing upload preview descriptions, product/brand/category names, and existing typed image labels.

| Rule or edge case | Handling | Proof |
|---|---|---|
| RULE-1; narrow width/orientation | Root bounded by allocation; fixed ratio except logos; mobile override | TASK-005 |
| RULE-2; tall/wide product | Contain entire source, allow empty space | TASK-001, TASK-005 |
| RULE-2; wide/tall logo | Contain within caller-reserved space, no square source crop | TASK-003, TASK-005 |
| RULE-3; different photography ratios | Cover with center position; original URL unchanged | TASK-001, TASK-005 |
| RULE-4; restricted destination | Retain route/UI permission checks, Application authorization and RLS | TASK-003, TASK-005 |
| Missing mobile artwork | Mobile branch reuses desktop URL with centered 4:5 crop | TASK-001, TASK-005 |
| Missing/unviewable image | Name/label inside unchanged preset frame; no broken-image icon | TASK-001, TASK-005 |
| Source replacement/race | Reset branch state; ignore callbacks belonging to prior source | TASK-001, TASK-005 |
| Destination absent | Preset tests prove support; no new production page | TASK-001, TASK-005 |
| Arrival/layout shift | Reserve ratio or caller logo allocation before load; long placeholder label cannot expand frame | TASK-001, TASK-005 |
| Decorative/meaningful image | Empty decorative alt; English named meaningful alt; no redundant placeholder description | TASK-001, TASK-005 |
| Surrounding actions | Image adds no keyboard stop; preserve callbacks, focus, accessible names | TASK-002, TASK-003, TASK-004, TASK-005 |

## 10. Database Schema & RLS Policies

### Schema

No persistence changes. No SQL migration, indexes, storage buckets, or DTO mobile-artwork fields required.

### RLS policies

Existing policies remain authoritative. Component receives only URLs already supplied by authorized page queries; no extra fetching endpoint or permission bypass added.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

- **⚠️ Risk — ✅ Accepted:** MudImage internal error handler and cached failures can race JS listener attachment. Accept residual timing risk with existing mitigations: TASK-001 checks completed image state and source identity, cleans subscriptions; TASK-005 exercises actual network failure and replacement. These checks remain required.

---
**Status:** Resolved · **Spec:** `.specs/shop-image/spec.md` · **Created:** 2026-09-30 · **Resolved:** 2026-09-30

# Implementation Plan — Product Details

**Feature:** `002_product-details`

Companion: `.specs/002_product-details/spec.md`.

## 1. Objective

Add public product-details page with first available variant selected, variant pricing, shared image gallery, and independent detail sections. Create separate reusable image-gallery component as explicitly requested. Preserve enabled Add to Bag and Favourite controls without actions.

## 2. Tech Stack

- **Domain:** Existing C# product aggregate and value objects; no new business entities.
- **Application:** MediatR, FluentValidation, existing `Result<T>`, immutable DTOs, static mapping and selection projection.
- **Infrastructure:** Existing Supabase repository, records, child mappers, and `IFileStorage` URL resolution.
- **Web:** Blazor WebAssembly, MudBlazor, `ShopImage`, `ShopBreadcrumbs`, `CurrencyFormatter`, SCSS.
- **Tests:** xUnit, NSubstitute, bUnit, existing PostgreSQL fixtures and Playwright journeys.
- **Persistence:** Existing published-product read policies; no schema changes.

## 3. High-level Architecture

Page dispatches new `GetProductDetailsQuery(Guid Id)` through MediatR. New repository read loads published aggregate with gallery, options, specifications, and variants. Application maps customer DTO and resolves initial selection. Subsequent variant changes project from loaded DTO without database writes or further reads.

```text
ProductDetails page
  IMediator.Send(GetProductDetailsQuery)
    GetProductDetailsHandler
      IProductRepository.GetPublishedByIdAsync
        SupabaseProductRepository: published parent plus owned children
      ProductDetailsDtoMapper
      Result<ProductDetailsDto>
  ProductDetailsSelection.Resolve: selected variant, price, linked image
  ShopImageGallery: large image, previews, selected-image callback
```

Names marked new below are planned additions, not existing repository APIs. No Domain or Application dependency on Web, routes, MudBlazor, or Supabase.

## 4. Data Model

### Domain entities & value objects

Reuse `Product`, `ProductVariant`, `ProductImage`, `ProductOptionType`, `ProductSpecification`, `ProductPricing`, and `ProductDescription`. Variant has one `PinnedImageId` referencing shared product gallery; no separate variant gallery. Reuse existing position ordering and gallery ownership invariants. No new Domain methods or exceptions.

### DTOs (Application to Web)

New records under `Application/Features/Products/DTOs/`:

- **`ProductDetailsDto`:** `Guid Id`, `string Name`, `string? BrandName`, `string DescriptionHtml`, `string DescriptionText`, `decimal? OriginalPrice`, `decimal? SalePrice`, `string Currency`, `IReadOnlyList<ProductImageDto> Images`, `IReadOnlyList<ProductOptionTypeDto> OptionTypes`, `IReadOnlyList<ProductSpecificationDto> Specifications`, `IReadOnlyList<ProductDetailsVariantDto> Variants`.
- **`ProductDetailsVariantDto`:** `Guid Id`, `int Position`, `decimal? OriginalPrice`, `decimal? SalePrice`, `string Currency`, `bool IsAvailable`, `Guid? PinnedImageId`, `IReadOnlyList<Guid> OptionValueIds`.
- **`ProductDetailsSelectionDto`:** `Guid? VariantId`, `decimal? OriginalPrice`, `decimal? SalePrice`, `string Currency`, `Guid? SelectedImageId`.

Reuse existing image, option, and specification DTO shapes without changing admin contracts. Product prices remain null when variants exist. No SKU registry or edit concurrency token exposed to customer page.

New repository contract: `Task<Product?> GetPublishedByIdAsync(Guid id, CancellationToken ct)` on existing `IProductRepository`. Return null for missing or unpublished parent, including privileged callers.

### Database tables (new or modified)

None. Read existing `products`, `brands`, `categories`, `product_images`, `product_option_types`, `product_option_values`, `product_specifications`, `product_variants`, and `product_variant_option_values`.

### Indexes

Reuse parent primary key and existing child ownership indexes. No index migration.

## 5. Core Design Decisions

1. **Decision:** Dedicated published-product query and repository method; reuse existing aggregate assembly patterns from `GetForEditAsync`.
   - **Why:** Storefront must reject unpublished products even for admins. Rules 4, 5, 7, 8 apply.
   - **Rejected:** Calling admin edit query; permission checks and edit payload do not match customer access.
2. **Decision:** Application owns deterministic variant and image selection through new `Features/Products/ProductDetailsSelection.cs`.
   - **Why:** RULE-1 through RULE-4 stay outside page business logic. Order variants by Position then Id; initially prefer first `IsAvailable` variant, falling back to first configured variant if no flag is set. This flag affects initial preference only; all configured variants remain viewable. No stock/inventory gate or unavailable state. Order previews primary first, then Position and Id. Linked image wins selected display; primary or first image is fallback.
   - **Rejected:** Replicating selection rules in page, mapper, and gallery.
3. **Decision:** Shared gallery persists across variant changes; all previews remain visible.
   - **Why:** User confirmed existing linked-image behavior during planning. `PinnedImageId` is sufficient.
   - **Rejected:** Adding variant-image tables or hiding other product previews.
4. **Decision:** New `Components/Common/ShopImageGallery.razor` and `.razor.cs` compose existing `ShopImage` with MudBlazor controls.
   - **Why:** Explicit user request authorizes extraction at first call site, overriding default Rule 25. Single responsibility: image browsing. Inherit `MudComponentBase`; forward Class, Style, UserAttributes. No fabricated second caller.
   - **Rejected:** Generic carousel framework or duplicating image failure handling.
5. **Decision:** Gallery parameters: `IReadOnlyList<ProductImageDto> Images`, `Guid? SelectedImageId`, `EventCallback<Guid?> SelectedImageIdChanged`, `string ImageAlt`, `string PlaceholderLabel`. Page owns current selection; gallery owns thumbnail-window position only.
   - **Why:** Controlled selection resets reliably when variant or product changes. Existing ShopImage handles missing/failed URLs.
   - **Rejected:** Gallery choosing variants or prices.
6. **Decision:** MudButton previews and MudIconButton previous/next controls move thumbnail window. Use MudStack/MudPaper layout and component SCSS for exact gallery geometry.
   - **Why:** Fits Figma without custom UI primitive. Thumbnail navigation does not silently change selected image; selected preview becomes visible after variant switch.
   - **Rejected:** Adding carousel library or page-wide horizontal overflow.
7. **Decision:** Page uses `Routes.ProductDetailsPattern = "/products/{id:guid}"` and new `Routes.ProductDetails(Guid id)` helper. New `BusyKeys.Products.Details` isolates detail load.
   - **Why:** Existing catalogue uses Guid product IDs and has no slug contract. `[Route]` lives on code-behind; load runs through BusyState and BusyFor.
   - **Rejected:** Hardcoded route strings, repository injection, or `_isBusy` fields.
8. **Decision:** Option groups follow existing option Position. New Application selection helper resolves option clicks to configured variant: preserve other selected values when matching variant exists; otherwise choose first configured variant containing clicked value. Disable only values occurring in no configured variant, never based on stock or availability flag.
   - **Why:** Figma shows separate nicotine/flavour groups; every accepted option click yields one valid variant and coherent price/image. Inventory and unavailable-product UI are excluded by user decision.
   - **Rejected:** Displaying price for incomplete or nonexistent combinations.
9. **Decision:** Two MudExpansionPanels allow multiple expansion; both start collapsed. Mapper validates persisted markup with existing `ProductDescription.Create` before exposing DescriptionHtml; on existing unsupported-content or too-long exception, expose empty HTML and retain DescriptionText for encoded MudText fallback. Show section only when DescriptionText is nonblank. Specification rows use MudGrid and MudText.
   - **Why:** RULE-5 and RULE-7; user-authored markup remains subject to existing grammar and database protection. No new editor.
   - **Rejected:** Empty sections or forced accordion behavior.
10. **Decision:** Purchase buttons have no command, navigation, toast, state subscription, or persistence effect.
    - **Why:** RULE-6; user explicitly requires enabled buttons that do nothing.
    - **Rejected:** Bag/favourite integration.
11. **Decision:** User approves price-specific structural typography: MudText with Barlow Condensed, 48px, weight 600, and 0.25px letter spacing. Use existing `fs-48` / `fw-600` utilities; set price-only letter spacing through page-owned SCSS and theme tokens. No nested MudThemeProvider or global typography change.
    - **Why:** Matches inspected Figma price; explicit approval permits scoped structural styling. Title keeps existing h3 typography.
    - **Rejected:** Global h3 change or unverified nested theme-provider isolation.
12. **Decision:** Mobile means MudBlazor `Breakpoint.Xs` (actual enum casing), below 600 CSS pixels. Stack gallery above information only at Xs; use 16px horizontal padding, proportionate large image, contained thumbnail scrolling, wrapping options/buttons, and one-column specifications. Sm and larger retain two columns with fluid widths; verify boundary at 599px/600px.
    - **Why:** User explicitly selects Xs responsive behavior; no mobile Figma frame exists.
    - **Rejected:** Previous 960px mobile threshold or invented mobile Figma parity.

## 6. Core Functional Flow

### Flow 1: Open product details

1. `ProductDetails.razor.cs` receives Id; clears previous product and cancels previous load on parameter change/disposal.
2. Run `BusyState.RunAsync(BusyKeys.Products.Details, ...)`; send query with CancellationToken.
3. Query validator rejects empty Guid using existing ProductErrorKeys.NotFound; handler returns same key for missing/unpublished product.
4. Repository explicitly filters parent `is_published = true`, then loads owned children using existing records/mappers and passes CancellationToken throughout.
5. Mapper resolves storage URLs through `IFileStorage.GetPublicUrl(StorageArea.ProductImages, ObjectKey)` and emits customer DTO.
6. Selection helper resolves first available variant and linked image/fallback; page renders DTO. Ignore stale cancelled result after route changes.
7. Missing result renders localized not-found state; unexpected load exception renders localized retry state. No admin or sign-in restriction.

### Flow 2: Browse images

1. Gallery renders primary-first preview order and selected large image.
2. Preview MudButton emits image Id; page updates selected image only.
3. Previous/next MudIconButton changes thumbnail window; keyboard focus and selected indication remain visible.
4. No images renders ShopImage placeholder without preview/navigation controls; single image retains selected preview without active navigation.

### Flow 3: Choose variant

1. Option MudButton emits option-value Id.
2. Selection helper resolves configured variant from loaded DTO, respecting existing other selections when possible. No inventory/availability restriction on viewing.
3. Update selected options, pricing, and linked/fallback image together. All previews remain visible; prior manual image choice resets.
4. CurrencyFormatter shows effective sale or original price; show original struck through only for valid discount.

### Flow 4: Read description or specifications

1. Render only populated sections; both initially collapsed.
2. MudExpansionPanel updates chosen section's expanded state; other section retains state.
3. Expanded description and ordered specifications remain keyboard accessible.

### Flow 5: Inspect purchase controls

1. Render enabled filled Add to Bag and outlined Favourite MudButtons with resource labels and ShopIcons.
2. Pointer or keyboard activation has no business effect.

## 7. Development Plan

### Step 1 — Domain

No Domain changes. Reuse aggregate and description/gallery invariants; report existing API to Application step.

### Step 2 — Application

**Depends on:** Resolved plan and existing Domain API.

- [ ] **TASK-001** — Add customer details/variant/selection records, `ProductDetailsDtoMapper`, persisted-description grammar validation with encoded text fallback, and `IProductRepository.GetPublishedByIdAsync` contract.
- [ ] **TASK-002** — Add `Queries/GetProductDetails/GetProductDetailsQuery.cs`, handler, validator; use existing ProductErrorKeys.NotFound and Result wrapping.
- [ ] **TASK-003** — Add ProductDetailsSelection projection and option-resolution methods; centralize initial variant preference, configured-variant browsing, ordering, price, pin, and fallback rules without inventory gates.

**Completion gate:** Application builds; query folder convention holds; DTOs contain no entities or outer-layer types; error key contract fixed. Test stage covers handler, mapper, and projection.

### Step 3 — Application contract checkpoint

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| GetProductDetailsQuery / Result<ProductDetailsDto> | Application | Web | Blocked until TASK-002 compiles; then Stable |
| IProductRepository.GetPublishedByIdAsync | Application | Infrastructure | Blocked until TASK-001 compiles; then Stable |
| ProductDetailsDto and ProductDetailsVariantDto | Application | Web | Blocked until TASK-001 compiles; then Stable |
| ProductDetailsSelection and ProductDetailsSelectionDto | Application | Web | Blocked until TASK-003 compiles; then Stable |

No later layer starts before consumed rows become Stable. Existing admin DTOs and methods remain stable.

### Step 4 — Infrastructure

**Depends on:** Application contracts Stable.

- [ ] **TASK-004** — Implement published aggregate read in `Persistence/Repositories/SupabaseProductRepository.cs`, reusing records and child mappers. Extract only common assembly required by detail/edit reads; retain admin semantics and public filtering.

**Completion gate:** Infrastructure builds; explicit publication filter precedes child reads; cancellation propagated; interface satisfied; no migration or permission widening. Test stage proves published/missing/unpublished behavior for guest and privileged sessions.

### Step 5 — Web

**Depends on:** Application contracts Stable and Infrastructure step complete.

**Visual scope:** required

### Visual targets

| Surface | Route | Viewport | State | Reference |
|---|---|---|---|---|
| details-desktop-main | /products/22222222-2222-4222-8222-222222222201 | 1440x900 | first available variant, linked image, both sections collapsed; crop main content | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2872-14181&t=wATD46fBcRhyXwu6-11 |
| details-desktop-expanded | /products/22222222-2222-4222-8222-222222222201 | 1440x1814 | description and specifications expanded | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2872-14181&t=wATD46fBcRhyXwu6-11 |
| details-desktop-gallery | /products/22222222-2222-4222-8222-222222222201 | 1440x900 | gallery with overflow and selected first linked image; crop gallery | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2872-14217 |
| details-mobile-main | /products/22222222-2222-4222-8222-222222222201 | 390x844 | Xs stacked layout, collapsed sections; compare selected 150x120 preview crop and inspect full viewport | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2872-14226 |

**Figma references**

- **File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2872-14181&t=wATD46fBcRhyXwu6-11
- **Nodes:** `2872:14181` entire expanded desktop frame; `2872:14187` main content; `2872:14217` gallery; `2872:14226` selected 150x120 preview; `2872:14188` product information; `2872:14238` description; `2872:14244` specifications.
- **Inspection:** 2026-10-01, live Figma Desktop Bridge. REST structure request returned HTTP 429; read-only figma_execute inspection and bridge screenshot succeeded. Screenshot was tool-scaled; Execute must export references at actual 1x dimensions.
- **Measured desktop:** Frame 1440x1814; content padding 32px horizontal and 64px vertical. Main content 1376x636; gallery width 806.4px, information width 537.6px, column gap 32px. Large image frame 806.4x500; contain artwork in 758.4x450 inner area. Previews 150x120 with 16px gaps; 28x120 navigation controls with 8px gaps. Information vertical gaps 42px. Detail section padding 32px and gap 24px; main-to-details gap 64px.
- **Typography:** Title MudText Typo.h3 (Barlow Condensed 48px/800/3px tracking); section titles Typo.h4; body/subtitles Space Grotesk. Price uses approved MudText heading family with fs-48/fw-600 and price-only 0.25px tracking; existing h3 remains at 3px.
- **Component geometry:** Existing ShopImage ProductDetail and Thumbnail presets are square. Gallery-owned SCSS changes only gallery instances to measured frame and preview geometry; no global preset changes. Preserve containment and placeholders. TASK-009 verifies square geometry remains unchanged for other callers.
- **Responsive decision:** Use Breakpoint.Xs, below 600px, per Decision 12. Mobile reference is exact Figma selected-preview node at 150x120, not desktop frame stretched to mobile. Capture matching preview crop for component fidelity. Also capture and inspect full 390x844 viewport and scrolled detail sections against approved responsive geometry; save mobile-viewport.png and findings with visual review. Check no page overflow, stacked order, options/buttons wrapping, contained preview scrolling, readable sections, and 599px/600px boundary. Full mobile layout is approved responsive inference, not Figma frame parity.
- **Scope conflicts:** Supplied frame shows expanded sections; initial state stays collapsed as confirmed. Hidden review and short-description nodes stay excluded. Shared header/footer use existing layout. User expressly authorizes reusable gallery despite first caller.
- **Deterministic data:** Local fixture Id above; exact title “Peak Pro Vaporizer & Dual Smooth Puff”, brand “PUFFCO”, price 345.29, nicotine/flavour labels and specifications matching reference. First available seeded variant must match reference selected options (15mg / Orange Gummy Bear) and blue artwork. Seed additional linked, unlinked, no-variant, missing-image, and empty-detail products for behavior checks. Verify legacy availability flags never introduce stock labels or block variant viewing. Export exact image assets from Figma for local fixture; do not use production data or screenshot as implementation.
- **Readiness:** `[data-testid="product-details-ready"]` appears only after current product and selection resolve. Main crop `[data-testid="product-details-main"]`; gallery crop `[data-testid="product-image-gallery"]`; mobile crop `[data-testid="product-image-preview-selected"]` at 150x120. Await all fonts/images. Expanded actions click `[data-testid="product-description-toggle"]` and `[data-testid="product-specifications-toggle"]`; finish with `[data-testid="product-details-expanded"]`.
- **Capture:** Use `.sdd/scripts/capture-ui.ps1` per visual-loop reference. Export matching main/gallery/selected-preview crops from exact nodes and full expanded frame; reference and actual dimensions must match. Export test for selected-preview node succeeded via Desktop Bridge on 2026-10-01 (150x120 PNG, 7309 bytes). TASK-008 preflights every reference and exact image asset before Web fidelity work; missing export blocks visual completion. Inspect reference, actual, overlay, difference, and browser geometry plus full mobile viewport; bind review to capture SHA256. Final capture required after last source edit. Variant switch, alternate preview, no images, and missing sections receive behavior tests plus browser inspection; do not compare unsupported states against mismatched Figma artwork.

- [ ] **TASK-005** — Add ShopImageGallery component and `Styles/components/_imagegallery.scss`; reuse ShopImage, accessible MudButtons, selection callback, placeholders, thumbnail-window controls, forwarding, and strictly scoped frame overrides.
- [ ] **TASK-006** — Add `Pages/Products/ProductDetails.razor` and `.razor.cs`; mediator load, cancellation, option projection, gallery, prices, independent panels, enabled no-op actions, and load/error/not-found states.
- [ ] **TASK-007** — Add centralized route/helper and busy key; wire existing ProductCard OnSelect from ProductCatalogue to details route. Add Section 9 resources, page layout `_productdetails.scss`, SCSS imports, approved price-only tracking, and Breakpoint.Xs responsive layout.
- [ ] **TASK-008** — Preflight and re-fetch Figma nodes/assets; prepare exact 1x references and deterministic local visual data; perform browser render/inspect/correct loop for all targets, inspect full mobile viewport and breakpoint boundary, and pass visual gate. No missing-reference waiver.

**Completion gate:** Web builds; MudBlazor-only controls; typed resources and theme tokens; consumed contracts Stable; correct anonymous access and unpublished not-found behavior; approved price typography; Xs responsive review; visual evidence inspected and visual gate passes.

### Step 6 — Integration & pipeline

**Depends on:** Infrastructure and Web complete.

- [ ] **TASK-009** — During `$theshop-test`, add Application handler/validator/mapper/selection tests including unsafe-description text fallback and variant browsing without inventory gates; Infrastructure published-read and RLS tests; bUnit gallery/page tests including existing square-image callers; Playwright product-details journeys and Xs boundary checks. Use full feature ID for feature evidence and traits.
- [ ] **TASK-010** — Build solution, run scoped tests and SDD/design gates; update graph with `graphify update .` after code changes. Run `$theshop-verify 002_product-details`; Document only if needed; Ship follows verified evidence.

**Completion gate:** Every AC mapped and exercised; declared tests pass; current visual evidence passes integrity gate; no unreviewed UI evidence.

### Deviation procedure

- Accept deviations only when approved behavior, inward dependencies, and publication restrictions remain intact and repository conventions improve.
- Reject scope expansion, bag/favourite actions, permission widening, unrelated refactors, or silent contract changes.
- Contract change: stop dependent work, record affected contract and TASK ids here, re-freeze contracts, then resume.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: first available variant and details | TASK-001, TASK-002, TASK-003, TASK-004, TASK-006, TASK-009 |
| AC-2: primary-first previews and initial fallback | TASK-003, TASK-005, TASK-008, TASK-009 |
| AC-3: preview selects large image | TASK-005, TASK-006, TASK-009 |
| AC-4: variant price/image, retained previews | TASK-003, TASK-005, TASK-006, TASK-009 |
| AC-5: unlinked variant resets image | TASK-003, TASK-005, TASK-006, TASK-009 |
| AC-6: no primary uses first image | TASK-003, TASK-005, TASK-009 |
| AC-7: no images shows placeholder | TASK-005, TASK-006, TASK-007, TASK-009 |
| AC-8: collapsed independent panels | TASK-006, TASK-008, TASK-009 |
| AC-9: absent details hide sections | TASK-001, TASK-006, TASK-009 |
| AC-10: enabled no-op buttons | TASK-006, TASK-007, TASK-009 |
| AC-11: no variants uses product price/gallery | TASK-003, TASK-006, TASK-009 |
| AC-12: guest and signed-in parity | TASK-002, TASK-004, TASK-006, TASK-009 |
| AC-13: keyboard, accessible states, English | TASK-005, TASK-006, TASK-007, TASK-008, TASK-009 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

- `GetProductDetailsQueryValidator`: nonempty Id; expected missing/invalid product returns ProductErrorKeys.NotFound, existing `Product_NotFound` resource key.
- Handler checks publication independently of caller permissions; repository filters published parent. Existing validators do not change.
- RULE-1: mapper/selection order primary first, Position then Id; choose linked image or primary/first fallback.
- RULE-2: preview event accepts only Id from loaded gallery; invalid/stale Id resets to current linked/fallback selection.
- RULE-3: projection uses selected variant prices and currency; product prices only with zero variants. No fabricated zero price.
- RULE-4: variant change resets selected image from valid pin/fallback; all previews stay.
- RULE-5: DescriptionText blank or Specifications empty omits corresponding panel.
- RULE-6: purchase buttons enabled without business handlers.
- RULE-7: panel expansion independent and initially false.
- No variants: empty option controls; product price and product gallery.
- No primary/unlinked variant: select first image or primary as applicable.
- No images: placeholder; no preview/navigation controls.
- One image: selected preview; navigation unavailable.
- Missing brand: omit brand text. Missing optional sections: omit controls.
- Broken image URL: existing ShopImage placeholder in reserved frame; other product details remain usable.
- Legacy availability flags: prefer first flagged variant initially, or first configured variant when none flagged; do not restrict viewing or show stock/unavailable-product state.
- Unsafe legacy description: validate through existing ProductDescription.Create; unsupported/too-long markup becomes encoded DescriptionText fallback. Never expose unchecked HTML.
- Missing/unpublished product: localized not-found state. Unexpected technical failure: localized error with retry. Cancellation/navigation race: discard old result; no error toast.

### Domain exceptions

No new Domain exception. Preserve existing description and gallery validation on write; read path does not mutate aggregate.

### Result.Fail error keys and UI resources

Reuse ProductErrorKeys.NotFound and existing `Product_NotFound`. Application uses current key-constant pattern rather than referencing Web Strings. Web resolves runtime error with Localizer; compile-time labels use Strings accessors. Add these English resources only:

| Key | English text |
|---|---|
| ProductDetails_PageTitle | Product details |
| ProductDetails_AddToBag | Add to Bag |
| ProductDetails_Favourite | Favourite |
| ProductDetails_Description | Product description |
| ProductDetails_Specifications | Specifications |
| ProductDetails_LoadFailed | Product details could not be loaded. Try again. |
| ProductDetails_Retry | Try again |
| ProductDetails_PriceUnavailable | Price unavailable |
| ImageGallery_Placeholder | Image unavailable |
| ImageGallery_SelectImage | View image {0} of {1} |
| ImageGallery_Previous | Previous images |
| ImageGallery_Next | Next images |

Reuse `Product_ImageAlt` with product name and `CurrencyFormatter`. Persisted product names/options/specifications remain data, not resource keys. No additional languages, resource designer file, or locale switch.

## 10. Database Schema & RLS Policies

### Schema

No persistence changes; no SQL migration required. New read uses current parent/children and storage public URLs.

### RLS policies

Keep existing products published-read restriction and child published-owner SELECT policies from migrations 0022 and 0029. Retain existing admin access predicates. Repository adds explicit publication filter so privileged customer-page requests never return unpublished data. No service-role client or SECURITY DEFINER function added. TASK-009 proves guest/signed-in access and unpublished rejection against existing schema.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

---
**Status:** Resolved · **Spec:** `.specs/002_product-details/spec.md` · **Created:** 2026-10-01 · **Resolved:** 2026-10-01

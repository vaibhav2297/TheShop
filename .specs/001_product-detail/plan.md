# Implementation Plan — Product Detail

**Feature:** `001_product-detail`

Companion: `.specs/001_product-detail/spec.md`. Confirmed product behavior governs Figma differences below.

## 1. Objective

Add public product detail with complete product data, variant pricing, responsive gallery, and independent disclosures. Reuse current aggregate, storage, RLS, and native controls. Bag and favourite operations remain excluded; missing-selection attention remains local UI behavior.

## 2. Tech Stack

- **Domain:** existing C# `Product`, `ProductPricing`, `ProductDescription`, and children; no changes.
- **Application:** MediatR, FluentValidation pipeline, `Result<T>`, immutable records, existing static mapper convention.
- **Infrastructure:** existing Supabase/PostgREST repository, records, mappers, `IFileStorage`.
- **Web:** .NET 10 Blazor WebAssembly, native HTML, Shop controls, SCSS, small JS module for browser geometry/navigation.
- **Verification:** xUnit/NSubstitute, bUnit, existing PostgreSQL integration harness, Playwright and SDD visual capture. No new packages.

## 3. High-level Architecture

`ProductDetail` dispatches public `GetProductDetailQuery` through `IMediator`. Handler loads published aggregate through new `IProductRepository.GetPublishedDetailAsync`, maps public DTO, and returns `Result<ProductDetailDto>`. Repository reuses existing child hydration from `GetForEditAsync`; no admin query dispatch or admin DTO exposure. Subsequent choices use loaded DTO and pure Application selection resolver; no network request per selection.

```text
ProductDetail.razor.cs
    IMediator.Send(GetProductDetailQuery)
        ValidationBehavior
        GetProductDetailHandler
            IProductRepository.GetPublishedDetailAsync
                SupabaseProductRepository + existing records/RLS
            ProductDetailDtoMapper
    Result<ProductDetailDto>
    local option/image/disclosure state; browser geometry via JS
```

## 4. Data Model

### Domain entities & value objects

Reuse `Product` and ordered `Images`, `OptionTypes`, `Specifications`, `Variants`. `ProductPricing.Effective` already selects sale/original. `ProductVariant.PinnedImageId` already references one image in shared product gallery; this is first associated variant image. No separate variant gallery, new invariant, or exception.

### DTOs (Application to Web)

New records under `Features/Products/DTOs/`:

- `ProductDetailDto`: `Guid Id`, `string Name`, `string? BrandName`, `bool HasVariants`, `string Currency`, `decimal? OriginalPrice`, `decimal? SalePrice`, `decimal? CurrentPrice`, `decimal? MinVariantPrice`, `decimal? MaxVariantPrice`, `string? DescriptionHtml`, `Guid? PrimaryImageId`, and read-only `Images`, `OptionTypes`, `Specifications`, `Variants` collections.
- `ProductDetailVariantDto`: `Guid Id`, `string Label`, `decimal? OriginalPrice`, `decimal? SalePrice`, `decimal? CurrentPrice`, `bool IsAvailable`, `Guid? TargetImageId`, `IReadOnlyList<Guid> OptionValueIds`. Exclude SKU and concurrency tokens.
- Reuse existing `ProductImageDto` (public URL, position, primary), `ProductOptionTypeDto`/`ProductOptionValueDto`, `ProductSpecificationDto`. No Domain entities in Web.

`ProductDetailDtoMapper.ToDto(Product, IFileStorage)` orders images primary-first, remaining by `Position` then `Id`; no duplicates. Other children retain `Position` then `Id`. Target image resolves valid pin, else primary, else first image, else null. Compute range from every configured variant's effective price, including unavailable variants; no stock-dependent range change. Equal endpoints remain equal numbers; Web formats one amount. Blank description uses `Description.PlainText`; blank specification rows are omitted. Prices remain nullable for defensive legacy-data handling, never zero substitutes.

### Database tables (existing, read only)

| Table | Purpose | Key columns |
|---|---|---|
| `products`, `brands`, `categories` | Published product and names | `id`, `is_published`, pricing, description, foreign keys |
| `product_images` | Shared gallery | `product_id`, `position`, `is_primary`, `object_key` |
| `product_option_types`, `product_option_values` | Named option groups | parent IDs, `position`, names/values |
| `product_variants`, `product_variant_option_values` | Prices, pins, option combinations | `product_id`, `image_id`, pricing, availability, option IDs |
| `product_specifications` | Ordered name/value details | `product_id`, `position`, `name`, `value` |

### Indexes

Reuse current primary/foreign-key indexes and child ordering indexes. No table, index, RPC, storage policy, or migration change.

## 5. Core Design Decisions

1. **Decision:** New public query and published-only repository method. Explicit `is_published` filter also applies to staff sessions; handler rejects unpublished results defensively.
   - **Why:** Existing admin RLS grants broader reads; storefront must remain identical for every shopper (AC-2, AC-14; constitution 4, 7).
   - **Rejected:** Reusing permission-gated `GetProductForEditQuery` or exposing `AdminProductDto`; both carry admin contracts.
2. **Decision:** Mapper owns price range, content presence, gallery order and image targets. `ProductDetailVariantResolver.Resolve(ProductDetailDto, IReadOnlyCollection<Guid>)` returns exact matching variant only after one option value per group is selected; otherwise null.
   - **Why:** Business interpretation stays Application-owned; Web owns transient selections, formatting and browser state (RULE-1–5; constitution 10).
   - **Rejected:** Product-level fallback for variant pricing, choosing first variant, or fetching price/image after each chip click.
3. **Decision:** Reuse Medium `ShopChip` controlled toggles inside named `fieldset`/`legend` groups. Maintain at most one explicit value per group; activating selected chip keeps it selected. Partial selection retains aggregate price/current image and requires completion before Add to Bag stops attention feedback. All configured combinations remain inspectable; existing availability does not introduce purchase behavior. User confirmed grouped-selection behavior during Resolve, 2026-10-20.
   - **Why:** Matches inspected option groups without another input component. Selection is complete only when all groups resolve existing variant.
   - **Rejected:** Page-owned chip copies, auto-filling unchosen groups, or unrelated stock/checkout validation.
4. **Decision:** Add reusable `ShopSwipeCarousel<TItem>` under `Components/Common/`, as explicitly requested during Resolve, 2026-10-20. Product detail supplies ordered images through source/alt selectors; the carousel itself renders `ShopImage`. Keep one gallery DOM: CSS grid at widths >=1024px; horizontal scroll-snap below. Desktop hides images after fourth until expanded; narrower layouts expose all. No thumbnail strip.
   - **Why:** Shared swipe, keyboard, position and lifecycle behavior has one owner; product rules stay outside. This user-directed component replaces the earlier inline-only decision. Fixed membership/order survives responsive changes (RULE-2, RULE-12).
   - **Rejected:** Product-specific DTO dependencies in the shared component, duplicated galleries, image reordering/highlighting, autoplay, looping, or a new carousel package.
5. **Decision:** Desktop selects target after render; expand before measuring hidden target. Scroll only when target has no intersection with usable viewport; a partially visible target counts as visible. Use nearest alignment, account for shell obstruction, never focus image. Narrow layouts scroll target inside gallery without moving document; retain keyboard focus on option. User confirmed visibility and resize defaults during Resolve, 2026-10-20.
   - **Why:** Avoid unexpected page movement for visible images (RULE-4). Media-query switch preserves selected variant and expansion; narrow view aligns selected target, otherwise primary.
   - **Rejected:** Unconditional `scrollIntoView`, full-page jump on phone, or highlighting matching tile.
6. **Decision:** Add to Bag is `type="button"`; missing selection reveals block, focuses first unchosen group's first chip without clicking, restarts one 300ms horizontal shake. Primed visually hidden live region announces requirement each attempt. Reduced motion uses static outline/focus, no transform or smooth scrolling. Selection clears attention. Favourite remains enabled no-op.
   - **Why:** Implements RULE-8–10 without a fake bag command or visible error. This is an attention interaction, not duplicate command validation.
   - **Rejected:** Disabled actions, toast/error text, looping motion, persistence, optimistic counters or success messages.
7. **Decision:** Reuse `ShopExpander`, initially collapsed, with semantic `h2` via `TitleContent`; render description's validated HTML with inspected secondary subtitle-1 text and specifications as responsive `dl` grid.
   - **Why:** Preserves authored formatting and independent disclosure state (RULE-6–7). Revalidate persisted HTML with existing `ProductDescription.Create` before emitting markup; omit unsafe legacy markup rather than render arbitrary HTML.
   - **Rejected:** Raw HTML from unchecked DTO, stripping valid formatting, new disclosure component, or empty headings.
8. **Decision:** Add centralized GUID route and native catalogue link. Extend `ProductCard` with optional `Href` for content anchor; retain existing callback-only mode for other callers. Existing cart/favourite buttons stay separate.
   - **Why:** Detail needs discoverable entry and native link semantics (constitution 21, 35).
   - **Rejected:** Literal URLs in Razor, button-only navigation, or nested interactive controls.

## 6. Core Functional Flow

### Flow 1: Open product detail

1. `Routes.ProductDetailPattern = "/products/{id:guid}"`; `Routes.ProductDetail(Guid)` builds link. `Pages/Products/ProductDetail.razor.cs` declares route, no authorization requirement.
2. On parameter change cancel prior load, clear product/selection/gallery/disclosure/attention state and old breadcrumbs. Run `Mediator.Send(new GetProductDetailQuery(Id), ct)` inside `BusyState.RunAsync(BusyKeys.Products.Detail, ...)`.
3. `GetProductDetailQueryValidator`: nonempty ID, using `ProductErrorKeys.DetailUnavailable`. Handler loads published aggregate and maps DTO; null/unpublished returns same key. Repository exceptions follow existing `ExceptionHandlingBehavior`; cancellation propagates quietly.
4. Apply response only if current route/load generation still matches. Set Home / Products / product-name breadcrumb using `BreadcrumbTrail.Storefront().Add(Strings.Nav_Products, Routes.Products).Current(name)`.
5. `BusyFor` owns loading skeleton; success shows range or own price and primary-first gallery, no selected variant. Failure replaces details with unavailable view and Browse Products link. Runtime technical error uses current notification service, with no stale content.

### Flow 2: Browse images

Desktop Show More expands all images, becomes Show Less, announces expansion and transfers focus to first newly revealed tile wrapper (`tabindex="-1"`) without changing selection/price. Show Less collapses to first four, preserves toggle focus, choices and price; later variant selection may expand again. Tablet/phone `ShopSwipeCarousel` supports native swipe plus ArrowLeft/ArrowRight/Home/End when its viewport itself has focus. Shared observer updates bound active index, 160px by 4px track segment and resource-backed current-position text; manual changes never select variants. No fake navigation when one image. Render one page-owned `ShopImage` placeholder when gallery empty; individual source failures keep source-specific fallback.

### Flow 3: Change variant

Chip changes selected option ID for its group. Resolver returns selected variant only for complete combination. Complete selection replaces range with `CurrentPrice`; sale original may appear struck through as supplied. Queue resolved `TargetImageId` for post-render gallery movement. Manual browsing afterward changes only active slide. Incomplete selection keeps aggregate price and current gallery; missing-selection attention remains until complete selection.

### Flow 4: Read description or specifications

Mapper suppresses blank content. Two `ShopExpander` instances manage independent collapsed state. Route changes recreate/reset both. Rich text remains encoded except validated description markup; specification text stays encoded.

### Flow 5: Activate action controls

No cart/favourite command dispatch. Missing complete variant selection invokes attention routine only. Complete selection or variant-free product yields no Add to Bag side effect; Favourite always yields none. Dispose JS observers, media listeners, pending attention work, .NET reference and cancellation source on navigation.

### Shared swipe carousel contract

`ShopSwipeCarousel.razor`/`.razor.cs` derives from `ShopComponentBase`; no product, variant, pricing, repository or route knowledge. Minimal parameters: `IReadOnlyList<TItem> Items`, `Func<TItem, string?> ImageSource`, `Func<TItem, string> ImageAlt`, resource-backed `string PlaceholderLabel`, `ShopImagePreset ImagePreset` (default SquareContain), zero-based `int ActiveIndex`/`EventCallback<int> ActiveIndexChanged`, `bool Enabled` (default true), resource-backed `string Label`, and `Func<int, int, string> PositionLabel` receiving one-based position/count. `Class`, `Style` and unmatched attributes target the root; documented `ViewportClass` and `SlideClass` target layout slots. `Ready` reports browser initialization for capture readiness. Explicit component-owned accessibility/event attributes take precedence. Carousel composes `ShopImage` internally; source/alt selectors preserve caller-owned data, shared image preset/failure behavior, and resource-backed labels. No arbitrary-content template or product dependency.

Shared `wwwroot/js/shopSwipeCarousel.js` owns native horizontal scrolling, nearest-slide observation, programmatic index alignment, keyboard navigation, reduced-motion behavior and cleanup. It scrolls only its own viewport, preserves focus, emits only changed settled indexes and suppresses observer/parameter feedback loops. Clamp indexes after item-count changes; empty items render no slides/indicator, one item has no navigation/indicator. No slide cloning, autoplay, wrapping, extra arrow controls or gesture library. Ignore carousel key handling for focused descendants; preserve vertical touch scrolling and pinch zoom. Cancel superseded navigation and detach observers/listeners/.NET references on disposal.

Product detail binds current gallery index and sets `Enabled` from its existing desktop media-query boundary; disabled mode keeps the same items/DOM but suspends swipe navigation, scroll callbacks, viewport tab stop and indicator. Page SCSS owns responsive grid/full-bleed placement and first-four visibility through documented layout slots; shared `_swipe-carousel.scss` owns enabled scroll-snap, focus and indicator appearance. CSS breakpoint rules establish correct initial layout before JS starts; after render the media-query signal must agree before readiness is exposed. No component-private appearance overrides in page styles.

`productDetail.js` retains only page-specific media-query coordination, desktop visibility/scrolling, expansion focus and selection attention. Entering narrow mode enables carousel, then parent applies selected target index or primary; changing a complete variant applies its target index. Desktop variant navigation remains page-owned and never highlights/reorders images. Reset carousel via product-keyed instance on route change. Shared carousel owns its sole position announcement; page owns expansion/selection announcements. Label/position formatter use `Strings.ProductDetail_Gallery` and `Strings.ProductDetail_ImagePosition`. Existing phone/tablet visual targets cover this first caller; no unrelated caller migration or standalone demo route.

## 7. Development Plan

### Step 1 — Domain

No impact. Reuse current pricing, pin, publishability and description contracts. No new Domain method or Domain tests.

### Step 2 — Application

**Depends on:** resolved plan; current Domain API.

- [x] **TASK-001** — Add public detail DTOs and `ProductDetailDtoMapper`; calculate pricing, content presence, primary-first order and fallback targets. Reuse existing child DTOs.
- [x] **TASK-002** — Add `GetProductDetailQuery`, handler, validator, `ProductErrorKeys.DetailUnavailable` and `IProductRepository.GetPublishedDetailAsync(Guid, CancellationToken)`; registration follows current assembly scanning.
- [x] **TASK-003** — Add pure `ProductDetailVariantResolver` for complete grouped selections; return DTO variant or null without side effects.

**Completion gate:** Application builds; public contracts documented; every Section 9 expected outcome represented; no Web/provider references. Test authoring belongs to Test stage, with required coverage below.

### Step 3 — Application contract checkpoint

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `GetProductDetailQuery(Guid)` / `Result<ProductDetailDto>` | Application | Web | Stable — Application build/scope passed, 2026-10-20 |
| `GetPublishedDetailAsync(Guid, CancellationToken)` / `Task<Product?>` | Application | Infrastructure | Stable — Application build/scope passed, 2026-10-20 |
| Detail DTO fields and reused child DTO shapes | Application | Web | Stable — Application build/scope passed, 2026-10-20 |
| `ProductDetailVariantResolver.Resolve` / nullable variant DTO | Application | Web | Stable — Application build/scope passed, 2026-10-20 |

Freeze consumed contracts before Infrastructure/Web. No optional parameter or DTO drift afterward without deviation record.

### Step 4 — Infrastructure

**Depends on:** repository contract Stable.

- [x] **TASK-004** — Implement published-only detail load in `SupabaseProductRepository`; extract shared child hydration from current edit load into private helper. Keep admin row-version/permission behavior intact; reuse all existing records and mappers, cancellation, and storage URL resolution. Filter publication before loading children; no service-role client.

**Completion gate:** Infrastructure builds; public method satisfies frozen interface; unpublished/stale IDs return null; admin hydration unchanged. No schema/policy/dependency change.

### Step 5 — Web

**Depends on:** Application checkpoint Stable; TASK-004 complete.

**Visual scope:** required

**Figma references**

- **File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-61089&t=vKYAXY6E1vg7eC7N-11
- Inspected through Figma Desktop Bridge, 2026-10-20: Product Detail section `3108:61089`; desktop `3107:60529`, tablet `3108:63693`, phone `3108:66297`; expanded/many-images/no-image/no-variants/unavailable siblings listed in targets.
- Original plan invocation's `2629:4428` was Product Catalogue. User corrected link to Product Detail; catalogue remains only regression reference for changed card navigation.
- Re-fetch exact nodes at Execute Web start. Export references at 1x; current inspection screenshots were downscaled by MCP. No maintained design JSON.

**Component mapping and capability decisions**

| Inspected design source / variant | Existing code API | Decision |
|---|---|---|
| Button `170:2625`, Filled/Primary/Large; `170:2656`, Outlined/Primary/Large | `ShopButton`: `Variant`, `Color`, `Size`, `EndIcon`, `OnClick`, `Href` | Add to Bag filled; Favourite and Show More outlined; trusted existing bag/heart/chevron icons. No control CSS copies. |
| Chip `180:518` default, `2036:10249` selected, Medium | `ShopChip`: `Size`, `Selected`, `SelectedChanged`, `Disabled` | Controlled single choice per named group; existing pressed semantics and focus. |
| ShopImage `2944:14402`, SquareContain | `ShopImage`: `Src`, `Preset`, `Alt`, `PlaceholderLabel` | Reuse square contain and source-failure fallback. Empty gallery spans available gallery width. Product detail passes product name for empty/failed images; shared label uses Subtitle 1, matching inspected16px Medium placeholder and user direction,2026-10-21. |
| Expander `2141:6255`, Collapsed/Default | `ShopExpander`: `TitleContent`, `ChildContent`, `Expanded` | Reuse independent disclosure; semantic headings use existing heading typography. |
| Swipe indicator `3108:69788`, instances `3108:69791`/`3108:69807` | No matching carousel; existing `ShopImage` supplies slide content | New user-requested `ShopSwipeCarousel<TItem>` owns swipe, keyboard, index, track/segment and hidden position text. Carousel renders `ShopImage` internally; page owns desktop grid/expansion. Reference frames/indicator revalidated through MCP on 2026-10-20. |
| MainLayout / breadcrumb / footer | existing MainLayout, `BreadcrumbState`, `ShopBreadcrumbs`, shell components | Reuse shell untouched. |
| Product Tile3022:23195,Default/Hover | Existing `ProductCard.Href` / `OnSelect`, `ShopImage`, `ShopIconButton` | Named typography and outside border in shared partial. Native content link/button stretches across tile; existing Bag/Favourite actions remain separate above link. Preserve modified clicks, callback mode and component forwarding. |

**Responsive geometry:** current shared breakpoints: phone <768px, tablet 768–1023px, desktop >=1024px. Desktop 1440: 32px side gutters, 64px vertical padding; gallery 924px, buy box 420px, 32px separation; gallery two458px square tiles with8px gaps. Buy box on right; details below gallery. Tablet 834: 32px gutters, 48px vertical padding, 770px image; gallery, buy box, details stacked. Phone 390: 32px vertical padding, full-bleed 390px image, 16px gutters for text/details. Track 160x4, 16px below image; its segment is 1/image-count. Figma defaults show four images (40px segment), while this fixture has eight (20px segment); selected fifth-image states show the pink pinned artwork rather than the reference's blue initial image. These action/data differences are reviewed explicitly. Use flexible tracks, no fixed text heights. Reuse `h3` title, `subtitle-1` labels and `body-2` chip labels. Price is an unbound Figma node: Barlow Condensed SemiBold 600, 48px current / 24px original, AUTO line height, 0.25px tracking, 12px price gap. Apply named sizes/family with the typography owner’s semibold primitive; preserve named styles unchanged.

**Existing shell state:** no active server announcement is supplied; keep the optional bar absent rather than invent a shipping offer. Reference shell includes a 32px desktop/tablet or 48px phone announcement. Review its exact vertical offset separately from feature-relative geometry; do not mask image differences. Other shell elements remain compared. Phone long current breadcrumb needs the existing shared component's missing single-line ellipsis treatment; TASK-013 adds CSS only, retaining expanded ancestor disclosure.

**Measured details:** specifications use four desktop, three tablet and two phone columns. Buy box is a flat flex column: name, optional variants, pricing and actions. Parent gap is 42px desktop/tablet and 32px phone, with or without variants. Only the bordered variant block retains internal 32px padding; no child section padding or purchase wrapper simulates parent spacing. Native approved typography uses normal line height: current expanded Figma content measures458/478/662px; record native normal-line-height bounds per capture. Review wrapping and accumulated line metrics explicitly, preserving named tokens.

**Approved spec differences:** initial chips unselected and aggregate price; no Show Less after expansion; image-unavailable wording clear; no reviews/recommendations. Figma selected sale/original display remains valid after selection. Keyboard focus/attention and reduced-motion states are inferred accessible treatments, not measured Figma states. TASK-009 records each expected difference against its specific element/state; all other layout, typography, artwork and controls remain subject to comparison. No broad ignored regions or replaced references. Runtime Google Fonts requests failed intermittently during capture and produced fallback wrapping; TASK-014 self-hosts the same approved faces plus the exact price SemiBold face with their OFL licenses. No font family, scale or token redesign. Existing detail/catalogue targets and signed-in browser checks cover shared font loading. Do not redesign shared tokens to match page-specific overrides. Existing native controls and typography tokens govern control variants; no MudBlazor dependency or new palette is permitted. Figma reference access and all 21 distinct frame IDs revalidated through Desktop Bridge on 2026-10-20.

**Desktop buy-box scrolling (user request, 2026-10-21):** Native CSS sticky on the intrinsic-height grid item (`align-self: start`), contained by the product grid. The sticky top is `min(0, scrollport height - buy-box height)`: tall content scrolls until its bottom is visible, then stays at the scrollport bottom with 24px clearance below the action row; shorter content sticks below the shell. Desktop-only outer bottom padding uses existing `--shop-space-6` and is included in the measured box height. Inter-section spacing remains parent gap. ResizeObserver tracks actual box/scrollport sizes; media-query/window resize updates and disposal share the existing page JS lifecycle. No scroll handler, fixed overlay, duplicated buy box or independent inner scrollbar. Tablet/phone stay static. Verify forward/reverse scroll, expanded gallery, product/footer boundary, focus, variant image reveal and 1023/1024px transitions. This scroll state is specified by the user, not claimed as a measured Figma state.

**Product tile refinement (user request,2026-10-21):** Supplied [Product Tile component set](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3022-23195). Default2009:5162 and Hover3123:44757 inspected/exported. Brand subtitle-2 (14px/500), name body-1 (16px/400), current price h6 (20px/500), original subtitle-1 (16px/500), tracking0.25px,AUTO. Remove old negative name margin;8px content gaps. Hover uses1px Primary rectangle inset -8px,matching Figma inside stroke; no scale/shadow/motion. CSS stretched native anchor/button covers image, stock label, text and whitespace; local stacking keeps existing Bag/Favourite buttons above it, preserving callbacks and modified-click browser behavior. Full-tile keyboard focus uses existing focus tokens. Hover applies only with fine pointer/hover capability; loading skeleton remains decorative/noninteractive. Existing ProductCard is sole catalogue caller; skeleton inherits shared typography. Ratings remain excluded because DTO has no rating data.

**Latest refinement (user request,2026-10-21):** Desktop gallery row/column gaps8px, verified in live Figma grid properties; specification values body-1 Regular400. Existing loading skeleton retains its separately designed16px grid gaps. Keep tablet/phone swipe gap0.

**Four UI fixes (user request,2026-10-21):** Skeleton frames3116:43231/43768/44257 define four desktop squares, one narrow square, brand/name lines, two option groups, price/actions and two collapsed detail headings. Reuse static ShopSkeleton in page-owned layout; loading sidebar stays static. Description uses body-1 Regular400,16px,0.25px tracking,AUTO line height and text-primary; specification values use body-1 Regular400,16px per latest explicit user request (overrides current Figma Medium500 values). Show Less uses existing outlined button/up-chevron, preserving choices/price/focus. ProductRecord left-embeds product_images in existing catalogue read, preserving products without gallery rows; mapper selects primary then position/id, retaining legacy image_path fallback without full-aggregate hydration or per-product requests. Catalogue fixture has gallery-only primary image and null legacy path. Skeleton captures hold actual product requests pending in browser; no synthetic DOM/loading flag. Thumbnail targets compare existing media region including controls.

### Visual targets

| Surface | Route | Viewport | State | Reference |
|---|---|---|---|---|
| detail-desktop | /products/11111111-1111-4111-8111-111111111101 | 1440x900 | initial, 4 of 8 images, range, no selection | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3107-60529 |
| detail-tablet | /products/11111111-1111-4111-8111-111111111101 | 834x1194 | initial image and indicator | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-63693 |
| detail-phone | /products/11111111-1111-4111-8111-111111111101 | 390x844 | initial image and indicator | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-66297 |
| detail-expanded-desktop | /products/11111111-1111-4111-8111-111111111101 | 1440x900 | scroll to both expanded details | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-61093 |
| detail-expanded-tablet | /products/11111111-1111-4111-8111-111111111101 | 834x1194 | scroll to both expanded details | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-64213 |
| detail-expanded-phone | /products/11111111-1111-4111-8111-111111111101 | 390x844 | scroll to both expanded details | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-66354 |
| detail-many-desktop | /products/11111111-1111-4111-8111-111111111101 | 1440x900 | expanded gallery; scroll to fifth image, no Show Less | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-61697 |
| detail-many-tablet | /products/11111111-1111-4111-8111-111111111101 | 834x1194 | select fifth-image variant; updated indicator | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-64821 |
| detail-many-phone | /products/11111111-1111-4111-8111-111111111101 | 390x844 | swipe fifth image; updated indicator | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-66449 |
| detail-no-image-desktop | /products/11111111-1111-4111-8111-111111111103 | 1440x900 | image placeholder | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-62198 |
| detail-no-image-tablet | /products/11111111-1111-4111-8111-111111111103 | 834x1194 | image placeholder | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-65897 |
| detail-no-image-phone | /products/11111111-1111-4111-8111-111111111103 | 390x844 | image placeholder | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-66515 |
| detail-no-variants-desktop | /products/11111111-1111-4111-8111-111111111102 | 1440x900 | own sale price, no choices | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-62684 |
| detail-no-variants-tablet | /products/11111111-1111-4111-8111-111111111102 | 834x1194 | own sale price, no choices | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-65377 |
| detail-no-variants-phone | /products/11111111-1111-4111-8111-111111111102 | 390x844 | own sale price, no choices | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-66572 |
| detail-unavailable-desktop | /products/11111111-1111-4111-8111-111111111104 | 1440x900 | unpublished product, no stale data | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-63169 |
| detail-unavailable-tablet | /products/11111111-1111-4111-8111-111111111104 | 834x1194 | unpublished product, no stale data | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-65945 |
| detail-unavailable-phone | /products/11111111-1111-4111-8111-111111111104 | 390x844 | unpublished product, no stale data | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-66629 |
| detail-attention-phone | /products/11111111-1111-4111-8111-111111111101 | 390x844 | Add to Bag, focused choices, reduced-motion outline; inferred state | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-66297 |
| detail-entry-desktop | /products?price_min=34.57&price_max=34.57 | 1440x900 | catalogue card native link, unchanged geometry | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2263-5531 |
| detail-entry-tablet | /products?price_min=34.57&price_max=34.57 | 834x1194 | catalogue card native link, unchanged geometry | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3041-39939 |
| detail-entry-phone | /products?price_min=34.57&price_max=34.57 | 390x844 | catalogue card native link, unchanged geometry | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3041-41494 |
| detail-price-desktop | /products/11111111-1111-4111-8111-111111111101 | 1440x900 | selected 5mg and Rose Syrup; full current/original price crop | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3107-60529 |
| detail-price-tablet | /products/11111111-1111-4111-8111-111111111101 | 834x1194 | selected 5mg and Rose Syrup; full current/original price crop | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-63693 |
| detail-price-phone | /products/11111111-1111-4111-8111-111111111101 | 390x844 | selected 5mg and Rose Syrup; full current/original price crop | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3108-66297 |
| detail-skeleton-desktop | /products/11111111-1111-4111-8111-111111111101 | 1440x900 | actual request pending; static skeleton | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3116-43231 |
| detail-skeleton-tablet | /products/11111111-1111-4111-8111-111111111101 | 834x1194 | actual request pending; static skeleton | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3116-43768 |
| detail-skeleton-phone | /products/11111111-1111-4111-8111-111111111101 | 390x844 | actual request pending; static skeleton | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3116-44257 |
| detail-thumbnail-desktop | /products?price_min=34.57&price_max=34.57 | 1440x900 | gallery-only catalogue thumbnail; legacy path null | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2263-5531 |
| detail-thumbnail-tablet | /products?price_min=34.57&price_max=34.57 | 834x1194 | gallery-only catalogue thumbnail; legacy path null | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3041-39939 |
| detail-thumbnail-phone | /products?price_min=34.57&price_max=34.57 | 390x844 | gallery-only catalogue thumbnail; legacy path null | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3041-41494 |
| detail-tile-hover-desktop | /products?price_min=34.57&price_max=34.57 | 1440x900 | fine-pointer hover outline top-left64px; full tile bounds in journal | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3123-44757 |
| detail-tile-hover-tablet | /products?price_min=34.57&price_max=34.57 | 834x1194 | fine-pointer hover outline top-left64px; full tile bounds in journal | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3123-44757 |
| detail-tile-hover-phone | /products?price_min=34.57&price_max=34.57 | 390x844 | fine-pointer hover outline top-left64px; full tile bounds in journal | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3123-44757 |

**Deterministic data/readiness:** TASK-009 adds idempotent local-only fixture setup under `tests/TheShop.E2E.Tests/tools/`, explicitly applied to prepared local stack without reset. Fixed IDs above: suffix 101 has PUFFCO / “Peak Pro Vaporizer & Dual Smooth Puff”, 8 ordered gallery images, 5 nicotine values and 4 flavours matching Figma, 20 combinations, CAD effective range 345.29–545.99, a selected sale 345.29/original 545.99, pins to second/fifth image and a no-pin variant. Suffix 102 has same text/images, no variants, own 345.29 sale/545.99 original. Suffix 103 has no images. Suffix 104 unpublished. Reserve suffix 105 absent; additional fixtures cover equal prices, one image, blank details, failed URL and no-primary fallback through DTO fixtures when storage invariants prevent persistence. Seed description/specifications from inspected expanded frame; export exact image-fill assets through MCP into feature evidence, upload individual artwork to local product storage. Never use whole screenshots as artwork. Suffix 106 supplies the Figma catalogue card (PUFFCO / Canadian Smooth - Smokeless & Super Strong, original 45.57/sale 34.57, side artwork). Catalogue target price filter isolates this fixture without changing unrelated product rows; capture its native content anchor against the matching Figma card content crop. Ratings are excluded by the feature scope, not added to existing ProductCard. Suffixes 107–110 cover one image, equal variant prices, blank details and broken source. Other catalogue fixture/reference text and images match existing nodes separately.

Ready selectors: `[data-testid="product-detail-ready"]` after DTO applied and gallery observer initialized; `[data-testid="product-detail-unavailable"]` for failure; `[data-testid="product-detail-details"]` for disclosure scroll; `[data-testid="product-detail-gallery"][data-active-image="5"]` after fifth-image action. Add stable selectors for Add to Bag, each option value, Show More and disclosures. Catalogue readiness uses existing grid class plus loaded matching card, never `body`. Prepared local stack already contains older `FIXTURE-PD-1` visual data with the same unique product title; fixture setup preserves that row under a previous-fixture label. Scenario activation gives only its own current product the exact reference title, keeping all fixture IDs stable and satisfying the name uniqueness constraint. No non-fixture product changes.

Actions: base targets load with no selection; expanded targets click both disclosure toggles then scroll details; many-desktop clicks Show More then scrolls fifth tile, with separate behavioral check for automatic expansion via selection; many-tablet selects complete fifth-image combination; many-phone swipes manually (Playwright behavioral check), capture helper uses gallery keyboard End/Arrow keys to same image. Attention target scrolls actions, activates Add to Bag and waits for focused attention state; crop the buy box so the selection block's exterior static outline remains visible above/below the choices. Default/unavailable/image states compare viewport crops. Expanded disclosure targets crop the details element against Figma Details at (32,1236), (32,1707), or phone inner (16,1379); native measured heights are checked after font readiness against current designed458/478/662px, retaining all visible content. Many-desktop compares the fifth tile; narrow many targets compare the gallery/indicator, whose target artwork follows the documented action. Catalogue captures crop the content anchor using updated14px brand/16px name and8px gaps; omit out-of-scope rating. Native normal-line metrics produce102/102/123px content versus100/100/120px design; screenshot crop rounds fractional desktop bounds to103px. Hover targets compare an unscaled64px top-left corner of exported Hover component, proving1px border/8px outside offset; full-tile geometry and screenshots remain in browser journal, without claiming whole-component pixel parity from this regional crop. Compare all reference/actual pixels in these crops, with no ignored regions; dimensions must match. Capture helper forces reduced motion; normal-motion shake needs separate Playwright check. Also inspect 320px, 767/768px, 1023/1024px, resizing, keyboard focus and forced colors; no Figma parity claim for inferred widths/states.

- [x] **TASK-005** — Add `ProductDetail.razor`/`.razor.cs`, GUID route/helper, `BusyKeys.Products.Detail`, resources from Section 9, skeleton/failure rendering, cancellation/stale-response guard and breadcrumbs. Keep single-use content inline; gallery uses shared component from TASK-012.
- [x] **TASK-012** — Before TASK-006, add generic `Components/Common/ShopSwipeCarousel.razor`/`.razor.cs`, `wwwroot/js/shopSwipeCarousel.js` and `Styles/components/_swipe-carousel.scss`; register SCSS in `TheShop.scss`. Implement Section 6 contract, controlled index, native swipe/keyboard, accessible track/position, enabled/static lifecycle, reduced motion, count changes and disposal. Document public API/layout slots; no product dependencies or extra callers.
- [x] **TASK-006** — After TASK-012, compose `ShopSwipeCarousel<ProductImageDto>` with `ShopImage`; bind active index and responsive enabled state. Add page-owned variant/desktop expansion/action interactions, `wwwroot/js/productDetail.js` lifecycle, expansion announcements and one-shot/reduced-motion attention. Reuse frozen DTO/resolver; do not duplicate shared swipe/indicator logic.
- [x] **TASK-007** — Add `Styles/layouts/_product-detail.scss`, register in `TheShop.scss`; page geometry, documented carousel layout slots and local attention presentation only. Shared carousel partial owns swipe/indicator appearance; existing component partials retain control appearance. Consume existing tokens.
- [x] **TASK-008** — Extend optional `ProductCard.Href`, preserve callback-only behavior and separate actions, wire catalogue via `Routes.ProductDetail(product.Id)`; anchor typography/focus matches current card.
- [x] **TASK-013** — Add phone-only single-line current-item ellipsis in existing `_breadcrumbs.scss`; collapsed ancestor disclosure and expanded wrapping remain intact. Needed for the product title shown by Figma. Inspect detail/catalogue targets; Test stage covers existing long-trail expansion and current-item accessibility.
- [x] **TASK-014** — Host existing Barlow Condensed 600/700/800 and Space Grotesk 400/500/700 font files in `wwwroot/fonts/`, retain OFL licenses/provenance, register faces in `Styles/base/_fonts.scss` and preload key faces from `index.html`. Remove runtime CDN dependency; verify actual face loading and reject fallback-font captures.
- [x] **TASK-009** — Prepare local deterministic data/artwork and all planned browser evidence. Rebuild current source, capture, inspect reference/actual/overlay/difference and geometry, correct mismatches, write hash-bound review with component-reuse findings and element-specific approved spec differences; reject unexpected differences and broad exclusions; pass visual gate.

**Completion gate:** Web/solution build; routes and DI resolve; English resources, native dependencies and design rules pass; frozen contracts consumed; all visual targets reviewed against references plus approved spec differences; no unhandled browser errors. Pending test authoring/feature verification remains for explicit Test/Verify stages.

- [x] **TASK-015** — Apply user corrections: carousel owns ShopImage with generic selectors; exact Figma SemiBold price face/tracking; flat buy-box parent gap, retaining only variant-block internal padding. Code build passed. Refresh all visual evidence, including three dedicated selected-price crops, before restoring Implement Done.

- [x] **TASK-016** — Implement intrinsic-height desktop buy-box stickiness and measured scrollport offset in existing page SCSS/JS. Build and browser geometry inspection cover AC-25; Test stage adds formal regression coverage.

- [x] **TASK-017** — Correct supplied responsive skeleton, reversible desktop gallery toggle, gallery-first catalogue thumbnail mapping and body-1/primary description. Latest refinement:8px desktop image-grid gap and body-1 specification values. Build, scoped gates and current browser/visual evidence; formal regression coverage remains Test.

- [x] **TASK-018** — Align shared ProductCard typography and hover border to supplied component, stretch native link/selection button across tile while preserving separate Bag/Favourite actions. Current browser journal covers whole-tile hit targets, modified clicks, focus, no navigation from actions, hover geometry and touch. Formal regression coverage remains Test.

### Step 6 — Integration & pipeline

**Depends on:** Infrastructure and Web complete.

- [x] **TASK-010** — Run build/design/scope/integration checks, inspect public/authorized/privileged sessions and missing product behavior; update graph with `graphify update .` after code changes. Record Execute evidence and hand off to explicit Test stage.
- [ ] **TASK-011** — Test-stage outcome: add handler/validator/mapper/resolver tests in `TheShop.Application.Tests/Features/Products/`; repository detail/hydration coverage and existing RLS regression in `TheShop.Infrastructure.Tests/Persistence/`; bUnit page, generic carousel, breadcrumb disclosure regression and ProductCard link/callback tests in `TheShop.Web.Tests/`; Playwright detail journeys using existing `ShopBrowser`/persona patterns. Carousel coverage: non-product image items/selectors, index/callback synchronization without loops, empty/single/changed item counts, attribute forwarding, disabled desktop mode and disposal. Browser coverage: swipe/keyboard position, variant-driven navigation without focus/page jumps, reduced motion and 1023/1024px transitions without duplicate gallery content. Stamp feature `001_product-detail`, generate manifest, run affected checks and preserve all AC evidence. Resolve test-writing mechanics through canonical Test skill; do not author tests during Execute.

**Completion gate:** explicit `$theshop-test 001_product-detail`, then `$theshop-verify 001_product-detail`; Ship follows only on user invocation. No automatic next-skill execution.

### Deviation procedure

- Accept only behavior-preserving reuse/placement improvements within approved scope and layer boundaries; record reason and affected tasks.
- Reject requirement changes, new purchase behavior, authorization weakening, unrelated refactoring or silent contract changes.
- Contract change: stop dependent work, update checkpoint/affected tasks, re-freeze before continuing. Product changes return to Clarify. Keep task IDs stable after Resolve; append new IDs.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: complete initial data, unselected range, primary first | TASK-001, TASK-002, TASK-005, TASK-006, TASK-011, TASK-012 |
| AC-2: public guest/signed-in access | TASK-002, TASK-004, TASK-005, TASK-010, TASK-011 |
| AC-3: desktop first four and Show More | TASK-006, TASK-007, TASK-009, TASK-011, TASK-012 |
| AC-4: manual narrow gallery preserves selection/price | TASK-006, TASK-011, TASK-012 |
| AC-5: selected variant pricing/image and preserved order | TASK-001, TASK-003, TASK-006, TASK-011, TASK-012 |
| AC-6: variant image fallback | TASK-001, TASK-006, TASK-011, TASK-012 |
| AC-7: no variants, own price, no bag effect | TASK-001, TASK-005, TASK-006, TASK-011 |
| AC-8: independent collapsed sections | TASK-005, TASK-009, TASK-011 |
| AC-9: omit blank sections | TASK-001, TASK-005, TASK-011 |
| AC-10: enabled no-op actions | TASK-005, TASK-006, TASK-011 |
| AC-11: keyboard, names, focus, perceivable state | TASK-005, TASK-006, TASK-007, TASK-008, TASK-009, TASK-011, TASK-012, TASK-013 |
| AC-12: single image | TASK-006, TASK-007, TASK-011, TASK-012 |
| AC-13: missing primary/images and failed artwork | TASK-001, TASK-005, TASK-006, TASK-009, TASK-011, TASK-012 |
| AC-14: product unavailable without stale details | TASK-002, TASK-004, TASK-005, TASK-010, TASK-011 |
| AC-15: missing selection shake/focus/announcement | TASK-006, TASK-007, TASK-011 |
| AC-16: clear attention upon selection | TASK-003, TASK-006, TASK-011 |
| AC-17: initial narrow primary/indicator/all images | TASK-001, TASK-006, TASK-007, TASK-009, TASK-011, TASK-012 |
| AC-18: Show More / Show Less toggle | TASK-006, TASK-011 |
| AC-19: already-visible target preserves scroll | TASK-006, TASK-011, TASK-012 |
| AC-20: hidden target expands before scroll | TASK-006, TASK-011, TASK-012 |
| AC-21: no Show More with <=4 images | TASK-006, TASK-011, TASK-012 |
| AC-22: equal variant prices collapse to amount | TASK-001, TASK-005, TASK-011 |
| AC-23: sale/original current price selection | TASK-001, TASK-003, TASK-005, TASK-006, TASK-011 |
| AC-24: reduced-motion static focus/outline | TASK-006, TASK-007, TASK-009, TASK-011 |
| AC-25: desktop buy-box natural scroll then stick | TASK-016, TASK-011 |
| AC-26: responsive designed loading skeleton | TASK-017, TASK-011 |
| AC-27: gallery-only catalogue thumbnail and description style | TASK-017, TASK-011 |
| AC-28: tile typography, hover and whole-tile navigation | TASK-018, TASK-011 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

`GetProductDetailQueryValidator`: `Id.NotEmpty()` returns `ProductErrorKeys.DetailUnavailable`, key `ProductDetail_Unavailable`. Public query has no `RequiresPermission`; no sign-in redirect. Missing/unpublished handler result uses same key, without revealing whether product exists. Unexpected network/provider failures retain existing pipeline keys `Auth_Network`/`Auth_Unexpected`; no new exception pipeline.

### Domain exceptions

No new exceptions. Mapper catches only existing `ProductDescriptionUnsupportedContentException` / bounds exceptions while validating legacy description; omit unsafe content, keep other details usable. Never swallow repository failure into invented product data.

### Rules and edge-case ownership

| Spec coverage | Implementation / failure behavior |
|---|---|
| RULE-1; missing primary | Mapper stable primary-first/fallback; route load starts unselected. |
| RULE-2, RULE-12; <=4 or hidden target | CSS desktop first-four; one-shot expansion; narrow exposes all; no filtering or Show Less. |
| RULE-3; variant-free/equal/sale/unpriced legacy data | Mapper effective prices/range; selected DTO exact amount; `CurrencyFormatter`; null reads `Strings.NotAvailable`, never zero. |
| RULE-4; missing pin/hidden/already-visible target | Mapper pin-or-primary; post-render expansion/visibility check; no highlight/reorder or focus transfer on variant selection. |
| RULE-5, RULE-11; swipe/single image | Shared carousel observer updates bound position only; keyboard navigation bounded; sole image has no false navigation/indicator controls. Empty gallery placeholder remains page-owned. |
| RULE-6; whitespace/rich-text-empty/specifications-empty | Mapper trims plain text and specification rows; omit section/control. |
| RULE-7 | Independent ShopExpander instances reset only on product change. |
| RULE-8; selected/variant-free Bag/Favourite | Native enabled buttons, no persistence/command/navigation/success effects. |
| RULE-9; missing selection/repeated activation/reduced motion | Focus/reveal, 300ms repeatable shake or static outline, repeat live announcement; no visible error or auto-selection. |
| RULE-10 | Complete selection clears attention/outline; price and target update together. |
| No displayable images/failed URL | Existing ShopImage placeholder shows product name in Subtitle 1 for empty galleries and failed carousel images; other details usable. |
| Missing/unviewable product; route race | Published-only read, failed Result, cleared DTO/selection/breadcrumbs; ignore superseded responses. |

### Result.Fail error keys and UI strings

`ProductErrorKeys.DetailUnavailable` references new resource key below, following current Application constant-key convention without Web dependency. Static UI uses typed `Strings`; runtime errors use `Localizer[result.Error]`. Reuse `ProductDetail_PageTitle`, `ProductSpecifications_Heading`, `NotAvailable`, `Nav_Products`, `Loading`, and `Product_ImageAlt`.

| Key | English text |
|---|---|
| `ProductDetail_Unavailable` | Product unavailable |
| `ProductDetail_UnavailableDescription` | This product can't be found or is no longer available. |
| `ProductDetail_BrowseProducts` | Browse Products |
| `ProductDetail_AddToBag` | Add to Bag |
| `ProductDetail_Favourite` | Favourite |
| `ProductDetail_Description` | Product Description |
| `ProductDetail_ShowMore` | Show More |
| `ProductDetail_ShowLess` | Show Less |
| `ProductDetail_Gallery` | Product images |
| `ProductDetail_ImageUnavailable` | Image unavailable |
| `ProductDetail_ImagePosition` | Image {0} of {1} |
| `ProductDetail_AllImagesShown` | All {0} images shown. |
| `ProductDetail_SelectVariant` | Select a variant before adding to bag. |
| `ProductDetail_PriceRange` | {0}–{1} |

No visible error text for missing selection; `ProductDetail_SelectVariant` is assistive announcement only. All resources remain English `Strings.resx`; accessor generated on build.

## 10. Database Schema & RLS Policies

### Schema

No persistence changes; no migration SQL or remote database mutation required. Read existing schema only. Local fixture data is test setup, not production migration.

### RLS policies

Preserve `products_public_read` (`is_published = true`) from `0002_create_product_catalogue.sql`, child `*_public_read` policies from `0022_create_product_variants.sql`, and `product_specifications_public_read` from `0029_product_specifications.sql`. Existing `0023_products_admin_writes.sql` permits privileged unpublished reads; new repository method therefore filters `is_published = true` explicitly. Brand/category reads and storage URLs reuse current policies. Do not modify grants, permissions, policies or service-role usage. Verify guest/customer cannot read unpublished parent/children; staff storefront still rejects unpublished detail despite admin access. Detail is a load-time view, without live administrative updates or an atomic cross-table snapshot. TASK-004 uses existing reads/RLS; TASK-005 suppresses superseded route results. Refresh reloads current data. No transactional RPC or subscription added for this read-only scope.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

- ⚠️ Risk — ✅ Accepted: independent product/child reads can overlap administrative edits and briefly show mixed or outdated load-time data. Existing publication filters/RLS, handler guard and stale-route suppression remain required in TASK-004–005; refresh reloads data. No purchasing occurs here; atomic snapshot/live synchronization would expand this read-only scope. See Section 10.

---
**Status:** Resolved · **Spec:** `.specs/001_product-detail/spec.md` · **Created:** 2026-10-20 · **Resolved:** 2026-10-20

# Implementation Plan — Variant URLs and Galleries

**Feature:** `007_variant-urls-and-galleries`

Companion: `.specs/007_variant-urls-and-galleries/spec.md`. Spec confirmed; implementation not started.

## 1. Objective

Replace variant pins with ordered image assignments. Resolve stable product/variant slugs into existing product aggregates. Keep URL, options, SKU, price, availability, and gallery aligned. Preserve shared uploads, product primary, existing GUID links, and atomic product saves.

## 2. Tech Stack

- **Domain:** pure C# entities/value objects.
- **Application:** immutable records, MediatR, FluentValidation, existing `Result<T>` and resource-key constants.
- **Infrastructure:** existing Supabase repository/storage adapters; PostgreSQL transactions and RLS.
- **Web:** .NET 10 Blazor WebAssembly, native inputs, existing `Shop*` components, project SCSS, English `Strings.resx`.
- **Verification:** existing layer test projects, bUnit, PostgreSQL fixtures, Playwright journeys.

## 3. High-level Architecture

```text
ProductForm / VariantImageDialog: staged ordered selections
  -> CreateProductCommand / UpdateProductCommand
  -> validators -> handlers -> Product aggregate
  -> IProductRepository -> SupabaseProductRepository -> save_product(jsonb)
  -> atomic product, assignments, address allocation -> Result<AdminProductDto>

ProductDetail route parameters
  -> GetProductDetailQuery -> published product + saved address lookup
  -> ProductDetailDto + exact/default SelectedVariantId
  -> ProductDetailVariantResolver -> one visible gallery and purchase state
```

Reuse `Product`, `ProductVariant`, `ProductInputMapper`, `ProductDetailDtoMapper`, `ProductDetailVariantResolver`, existing upload cleanup, and optimistic row versions. Routes and busy state remain Web-owned. No new service boundary.

## 4. Data Model

### Domain

- Replace `ProductVariant.PinnedImageId` and `ProductVariantInput.PinnedImageId` with ordered `IReadOnlyList<Guid> ImageIds`. Empty means no cover. Cover always `ImageIds.FirstOrDefault()`; no independently stored cover.
- Add immutable `VariantImageAssignment` value object: ordered unique IDs; `AppendMissing` preserves existing order. Duplicate constructor input throws new `DuplicateVariantImageException` (`Product_VariantImageDuplicate`).
- Replace `Product.PinVariantImage` with `SetVariantImages(Guid variantId, IReadOnlyList<Guid> imageIds)`. Existing `ProductImageNotOwnedException` rejects foreign IDs. `ApplyVariantConfiguration` validates every supplied assignment and preserves retained variant IDs by option-value IDs.
- `SetGallery` and `RemoveImage` prune removed IDs from every variant; retain remaining order. Product primary stays independent. Omitted existing variant configuration retains assignments; newly generated combinations start empty.

### Application contracts

- `VariantInput` and admin variant DTO: replace pin with ordered `ImageIds`. Input IDs reference existing image IDs or `ProductGalleryEntry.ClientId` for staged uploads.
- `ProductAddressDto(string ProductSlug, IReadOnlyDictionary<Guid,string> VariantSlugs)` — new immutable record.
- Extend `IProductRepository` with `GetPublishedDetailBySlugAsync(string productSlug, CancellationToken)` returning `Product?`, and `GetAddressesAsync(Guid productId, CancellationToken)` returning `ProductAddressDto?`. Keep existing GUID lookup, save results, and concurrency contract.
- Extend `GetProductDetailQuery` to `(Guid? Id, string? ProductSlug, string? VariantSlug)`: exactly one parent identifier; variant slug requires product slug. No URL strings enter Application.
- `ProductDetailDto`: add `ProductSlug`, nullable `SelectedVariantId`. Variant DTO: add `Slug`, `Sku`, ordered `ImageIds`; remove `TargetImageId`. Shared image DTO collection remains image lookup data; Web exposes only selected IDs.
- Extend `ProductDetailVariantResolver` with initial selection resolution: explicit slug requires exact match; otherwise first available by existing order, then first. Existing exact-combination resolution remains.
- New `ApplyVariantImagesQuery` / handler / validator: pure preview, no persistence; accepts variant DTOs, explicit target IDs, ordered image IDs, replace/append mode, and IDs explicitly deselected from the edited variant. Handler uses Domain `VariantImageAssignment`. Returns updated immutable inputs. Web owns dialog editing state; Domain owns merge rules.

### Persistence

| Table | Purpose | Constraints / indexes |
|---|---|---|
| `product_variant_images` — new | Ordered assignments | PK `(variant_id,image_id)`; unique `(variant_id,position)`; composite same-product foreign keys; product lookup index |
| `product_routes` — new | Stable parent slug | Slug PK; unique nullable product ID; deleted target becomes null |
| `product_variant_routes` — new | Stable variant slug under parent | PK `(product_slug,slug)`; unique nullable variant ID; deleted target becomes null |
| `product_variants` — retained | Existing variant data | Keep `image_id` as compatibility projection of first assigned image |

Address records survive child replacement and deletion. Null targets reserve old addresses; never resolve as live content. No manual slug editing. No duplicate product records.

## 5. Core Design Decisions

1. **Ordered relationships:** one library, many variant assignments. Supports shared uploads and independent order (RULE-2–4, RULE-8–9). No duplicated storage objects or variant-level primary flag.
2. **Persist initial slugs:** ASCII lowercase, punctuation collapsed to hyphens, base capped at 80 characters; names yielding no ASCII letters/digits use `product`/`variant` fallback. Collisions append `-2`, `-3`, etc. Append `-product` to GUID-shaped product bases so legacy GUID routes remain unambiguous. Generate variant name from ordered option labels. Never regenerate on rename. Reserved deleted addresses prevent accidental reassignment (RULE-1, RULE-7).
3. **Extend existing transaction:** migration wraps current stock-free `save_product` from migration 0032. Snapshot assignments and address bindings before its delete/reinsert; restore retained bindings afterward. Keep core function private. Existing save version, SKU registry, specifications, and upload compensation remain. Avoid another independent gallery save transaction.
4. **Explicit assignments only:** no product-primary or first-library-image fallback. Empty gallery remains valid. Legacy valid pin seeds one assignment; unpinned remains empty.
5. **Navigation:** Web adds `Routes.ProductBySlug` (`/products/{productSlug}`), `Routes.ProductVariant` (`/products/{productSlug}/{variantSlug}`), and URL builders. Existing GUID route remains. Normalize parameters from current route shape before dispatch, clearing obsolete GUID/slug parameters when Blazor reuses component instances. Parent/GUID entry replaces history with selected variant URL; nonvariant GUID entry replaces with parent slug URL. Explicit selection pushes history; Back/Forward never pushes another entry.
6. **Atomic presentation:** resolve complete combination before navigation. Invalid combinations show unavailable-combination feedback without replacing current committed variant/URL. Route response commits one selection snapshot; stale request generations cannot commit. Reset gallery index and expansion on successful variant change; remove `revealImage` scroll behavior.
7. **Staged admin edits:** dialog **Apply** updates parent form; product **Save** persists. Cancel discards assignment/order edits. Uploads already added to pending product library remain reusable and persist only on product Save. The shared option value choice appends missing images and removes explicitly deselected images only for variants with that value; individual edit replaces only its assignments. Product save failure retains all editing choices. Existing two-checkbox Apply To UI stays; dialog tiles remain in product-library order when selected. Deselect and reselect moves an image to the end of the saved variant gallery.
8. **Native UI and existing permissions:** reuse components, resource keys, `products.create`/`products.edit`, and publication checks. No new permission or UI framework (constitution Rules 1–10, 14, 20–22, 31–35).

## 6. Core Functional Flow

### Flow 1 — Exact variant (Behavior 1)

Route parameters dispatch `GetProductDetailQuery` through `BusyKeys.Products.Detail`. Validator checks identifier shape. Handler retrieves published product and addresses; exact slug must belong to that product. Failure returns existing `ProductDetail_Unavailable`. Success commits selected options, SKU, prices, availability, and filtered ordered images together.

### Flow 2 — Switch variant (Behavior 2)

Option event calls Application resolver. Complete match updates route through centralized builder, without force-load. Same-product route changes reuse loaded DTO and resolve synchronously; fresh entry fetches. Key carousel by variant ID, reset index to zero, ignore old generation callbacks. Preserve scroll and focused option control. Existing page-navigation focus remains for new product/page navigation; adjust `App.razor` focus handling only if browser proof reveals same-product focus movement.

### Flow 3 — Parent / legacy entry (Behavior 3)

Handler resolves default in saved order. Web replaces current history entry with variant URL. All unavailable selects first, purchase disabled. No variants: retain original product gallery and parent URL. Never default after explicit variant lookup fails.

### Flow 4 — Manage gallery (Behavior 4)

Dialog copies current ordered selection. Select appends; deselect removes; reselect moves an image to the end of variant-gallery order. Dialog tiles remain in product-library order. First selected image receives derived primary badge. Upload uses existing `AddGalleryFiles` callback; new client ID enters pending library and edited selection only. Apply dispatches preview query, then updates form inputs. Product Save dispatches existing command; validator checks client IDs; handler uploads, maps client IDs to saved IDs, applies Domain invariants, saves once. Failure cleans newly uploaded server objects using existing compensation and leaves form previews intact.

### Flow 5 — Reuse images (Behavior 5)

Default scope is current variant. Bulk scope names the shared option value and targets only variants with that value. The variants card remembers the last successfully applied scope by option value ID for dialogs opened during the current edit session; Cancel does not change it. `ApplyVariantImagesQuery` removes IDs explicitly deselected from the edited variant, then appends missing selected IDs to chosen targets using Domain merge; unrelated variants remain identical. No independent cover selection.

### Flow 6 — Restore selection (Behavior 6)

Refresh/shared link runs exact query. Back/Forward resolves route selection without new history entry. Cancellation plus monotonically increasing request generation guards asynchronous loads. Gallery callbacks capture variant generation.

### Flow 7 — Missing content (Behavior 7)

Empty selection renders neutral `ShopImage` placeholder. Broken image uses existing image failure state within assigned slot. Unknown/deleted/wrong-parent/unpublished route renders unavailable. Published unavailable variant keeps its identity/gallery with disabled purchase state. Library deletion prunes every assignment; variants remain.

## 7. Development Plan

### Step 1 — Domain

**Depends on:** resolved plan.

- [ ] **TASK-001** — Implement ordered assignment value object, duplicate exception, aggregate assignment/removal behavior, retained IDs; replace pin APIs. Update `ProductTests` / `ProductVariantTests`; cover append, ownership, removal, empty, and independent primary.

**Completion gate:** Domain build/tests pass; no outer-layer references; report public API.

### Step 2 — Application

**Depends on:** Domain API.

- [ ] **TASK-002** — Extend inputs/admin DTOs, preview query, command validators, mapper and create/update handlers. Validate all staged/existing IDs before uploads; map every client ID; preserve failure cleanup and row versions. Replace pin tests with ordered-gallery cases.
- [ ] **TASK-003** — Add address contract/repository methods; extend detail query, mapper, resolver, and DTOs. Tests cover exact/default/unavailable/nonexistent combinations and immutable coherent selection.

**Completion gate:** Application builds/tests pass; all validators use pipeline; key constants recorded for Web resources. No repository or Domain entity enters Web contracts.

### Step 3 — Application contract checkpoint

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| Ordered `VariantInput`, admin DTO, create/update command results | Application | Infrastructure, Web | Pending TASK-002; freeze as Stable before Step 4 |
| `ApplyVariantImagesQuery` and result | Application | Web | Pending TASK-002; freeze as Stable before Step 4 |
| `IProductRepository`, `ProductAddressDto` | Application | Infrastructure | Pending TASK-003; freeze as Stable before Step 4 |
| Detail query, DTOs, selection resolver | Application | Web | Pending TASK-003; freeze as Stable before Step 4 |

### Step 4 — Infrastructure

**Depends on:** all Application contracts Stable.

- [ ] **TASK-004** — Add `supabase/migrations/0039_variant_urls_and_galleries.sql` from Section 10. Update migration fixture lists. Prove populated migration, pin backfill, stable rename/save/delete behavior, collisions, same-product constraints, per-mode permissions, and transaction rollback in PostgreSQL tests.
- [ ] **TASK-005** — Add route/assignment records under `Persistence/Records`; extend `ProductChildMapper`, `SupabaseProductRepository` hydration/payload and error translation. Batch assignments/addresses; avoid per-variant fetches. New payload always supplies ordered `image_ids`. Test published lookup, foreign images, stale versions, uploads, and retained product primary.

**Required save mitigation (TASK-004/005):** seed multiple ordered galleries and saved routes. Price-only saves and label renames must retain variant IDs, routes, and image order. Force failure after core child replacement; assert complete rollback of prices, routes, assignments, and row version. Race two saves using one expected version; exactly one succeeds, loser returns modified-elsewhere without partial changes. Verify deletion reserves old URL, legacy payload preserves surviving assignments, and concurrent slug collisions allocate distinct addresses.

**Completion gate:** disposable database migration and required save-mitigation tests pass; RLS roles tested; wrapper/helpers cannot be called without correct permission; repository satisfies frozen contracts. Failed/skipped mitigation tests block completion. No live deployment during Execute.

### Step 5 — Web

**Depends on:** Infrastructure complete; frozen Application contracts.

**Figma reference:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2629-5381&t=2CVwBLSG7twvF37T-11

Inspected through Figma Desktop Bridge. Re-fetch at implementation time.

| Nodes | Intent / implementation mapping |
|---|---|
| `2687:15441`, `3041:54463`, `3041:56013` | Create-product desktop/tablet/phone; existing `ProductForm`, `ProductVariantsCard`, shared image library |
| `2946:14712`, `3041:59037`, `3041:59112` | Image-selection dialog responsive states; `ShopDialog` with `MaxWidth.Small` |
| `3013:18569`, `3041:59187`, `3041:59250` | Empty library; distinguish from nonempty library with zero selected images |
| Source `2946:14556` | Dialog header/content/actions; existing `ShopButton` / `ShopIconButton` |
| Sources `3013:18166`, `3013:18160`, `3013:18184` | Selected/default/add tiles; `ShopImageTile.Selected`, derived `Primary`, `Draggable`, `OnKeyDown`; existing `ShopFileDropzone` |
| Source `2944:14402` | Existing `ShopImage`, `SquareContain`; reuse product storage images, no exported sample-photo assets |

Design decisions: preserve existing Apply To checkboxes. The bulk checkbox names the first shared option value (for example, “All 2 variants with Flavour = Mango”) and applies only to variants sharing that value, without a second target list. Dialog tiles stay in product-library order. Deselecting and reselecting moves an image to the end of the saved variant gallery; first selected image is its cover. Use **Apply** for staged dialog action. Existing Figma text confirms uploads enter product library. No storefront prototype behavior was evidenced in this Admin section; preserve existing storefront layout and implement spec interactions.

- [ ] **TASK-006** — Extend `VariantImageDialog`, `ProductVariantsCard`, `ProductForm` and result model for ordered selections, derived cover, shared option value bulk assignment, uploads, remove/reorder, staged Apply/Cancel. Update `_product-form.scss`; bUnit covers retry, cancel, independent primary, and bulk append.
- [ ] **TASK-007** — Add slug routes/builders to `Routes`; adapt `ProductDetail.razor`/code-behind and existing gallery. Filter every gallery surface from one selected-image list; render variant SKU. Preserve native navigation, history, focus and scroll; remove pin/reveal logic. Extend `_product-detail.scss` only where required. bUnit covers route resets, empty/broken images, invalid combinations and stale loads.
- [ ] **TASK-008** — Add Section 9 English resources and accessible names. Reuse `BusyKeys.Products.Detail`, `SaveProduct`, `LoadProduct`, and existing per-dialog image-selection key. Verify dialog focus restoration, selected-image order, live selected-variant announcement, and carousel position. No new general-purpose UI primitive.

**Completion gate:** Web build, bUnit, scope/design checks pass. States covered: loading, empty library, no assignment, broken image, unauthorized, unavailable, validation, save conflict/failure, success. Responsive checks at 1440, 834, 390, and 320 CSS pixels; existing 768/1024 breakpoints. No hardcoded strings/tokens or legacy UI assets.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4–5 complete.

- [ ] **TASK-009** — Extend existing product detail/admin Playwright journeys and page objects. Seed distinct image bytes for two variants plus unavailable, empty-gallery, nonvariant, unpublished, and renamed records. Assert image membership/order, URL/SKU/price alignment, Back/Forward, refresh, scroll/focus stability, touch, upload/bulk/delete/retry, unauthorized RPC, and rapid selection. Reuse `product-detail-ready`; add named dialog-selection readiness only if existing selectors cannot express readiness. Never use fixed sleeps or placeholder-only images as proof.
- [ ] **TASK-010** — Build solution; run focused layer tests and applicable SDD/design gates. Run local E2E harness with fixtures; missing environment/skipped browser tests remain incomplete evidence. Record migration/app rollout and app rollback results. Run `graphify update .` after code changes. Prepare handoff to `$theshop-test 007_variant-urls-and-galleries`, then `$theshop-verify 007_variant-urls-and-galleries`; do not auto-claim those stages complete.

**Required navigation mitigation (TASK-007/009):** control asynchronous completion order; select Red then Blue, finish stale Red load/callback last, and assert Blue URL/options/SKU/price/gallery remain aligned. Capture scroll position and focused control before switching; assert preservation after render. Repeat history navigation and keyboard/touch cases at desktop/mobile sizes. Use actual browser assertions for focus/scroll; bUnit alone is insufficient. Failed/skipped checks block integration completion and release handoff.

### Deviation procedure

- Accept only behavior-preserving changes aligned with existing conventions and layer boundaries.
- Reject scope expansion, weakened authorization/integrity, or changed approved behavior.
- Contract change: stop dependent work, record affected TASK IDs and contract changes here, re-freeze, then resume.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: exact variant identity/details | TASK-003, TASK-005, TASK-007, TASK-009 |
| AC-2: coherent switch without reload/scroll | TASK-003, TASK-007, TASK-009 |
| AC-3: every gallery surface scoped/order | TASK-007, TASK-009 |
| AC-4: assign/order/derived cover | TASK-001, TASK-002, TASK-006, TASK-009 |
| AC-5: bulk add/remove/no duplicates | TASK-001, TASK-002, TASK-006, TASK-009 |
| AC-6: local removal/shared upload | TASK-001, TASK-006, TASK-009 |
| AC-7: next cover/empty | TASK-001, TASK-006, TASK-007 |
| AC-8: parent default/all unavailable | TASK-003, TASK-007, TASK-009 |
| AC-9: history/refresh/share | TASK-007, TASK-009 |
| AC-10: empty/broken fallback | TASK-007, TASK-009 |
| AC-11: GUID/nonvariant compatibility | TASK-003, TASK-007, TASK-009 |
| AC-12: existing pin transition | TASK-004, TASK-005 |
| AC-13: invalid/restricted route | TASK-003, TASK-004, TASK-005, TASK-009 |
| AC-14: unavailable direct variant | TASK-003, TASK-007, TASK-009 |
| AC-15: authorization | TASK-002, TASK-004, TASK-009 |
| AC-16: ownership/duplicates | TASK-001, TASK-002, TASK-004, TASK-005 |
| AC-17: stable slugs/collisions | TASK-004, TASK-005, TASK-009 |
| AC-18: keyboard/touch/accessibility | TASK-006, TASK-007, TASK-008, TASK-009 |
| AC-19: latest selection wins | TASK-007, TASK-009 |
| AC-20: shared image deletion | TASK-001, TASK-004, TASK-006, TASK-009 |
| AC-21: failed save retains choices/state | TASK-002, TASK-005, TASK-006, TASK-009 |
| AC-22: nonexistent combination | TASK-003, TASK-007, TASK-009 |
| AC-23: reorder changes only variant cover | TASK-001, TASK-006, TASK-009 |
| AC-24: upload/library/primary independence | TASK-002, TASK-006, TASK-009 |

## 9. Validation & Error Handling Strategy

| Rule / edge | Enforcement / result |
|---|---|
| RULE-1, RULE-7; collisions/rename/deletion/wrong parent | Database unique route keys and reserved tombstones; lookup requires live target under parent. Exact miss returns `ProductDetail_Unavailable`. Retain IDs when labels change. |
| RULE-2; foreign/unknown staged image | Create/update validators check ordered IDs against submitted library/client IDs. Loaded aggregate verifies ownership; composite database FKs enforce same parent. Existing `Product_VariantImageNotInGallery`. |
| RULE-3; duplicate/order/cover | Validators reject duplicate list IDs; Domain exception `DuplicateVariantImageException`; PK/unique position constraints. Derive cover exclusively from first position. |
| RULE-4; remove cover/final image | Domain removal preserves remainder; no validation error for empty. Shared library unaffected by assignment removal. |
| RULE-5; unpublished/explicit unavailable/all unavailable/no variants | Repository enforces publication even for admin credentials. Resolver distinguishes explicit miss from default. Unavailable variant retains details; purchase disabled. Nonvariant uses product images. |
| RULE-6; unauthorized update | Existing MediatR permission behavior precedes validation; RPC checks create/edit by mode. RLS limits reads; new tables have no client writes. Existing authorization error presentation. |
| RULE-8; bulk add/remove | Domain `Remove` retains other assignments and `AppendMissing` skips existing IDs; only selected target variants modified. Preview query validates targets and image IDs. |
| RULE-9; shared image deletion | Domain pruning plus cascading image FK; normalize order on next save. Cover uses lowest remaining position immediately. No variant delete cascade from image. |
| Empty/broken image | Placeholder/failure component; never fetch unrelated fallback. Empty list passes save validation. |
| Failed upload/save/concurrent edit | Existing `Product_CreateFailed`, `Product_UpdateFailed`, `Product_ModifiedElsewhere` paths; preserve form state, compensate new uploads only. Database rollback keeps prior state. No raw SQL text shown. |
| Rapid route/load/carousel events | Cancellation, request generation, variant-keyed carousel; stale completion ignored before state/URL writes. |
| Nonexistent combination | Resolver returns no completed variant; selected choices identified unavailable, existing committed details remain clearly associated with committed variant. No navigation/purchase for attempted invalid combination. |
| Malformed query or assignment payload | FluentValidation field failures through `ValidationErrors`; RPC also rejects malformed arrays, duplicate/foreign IDs, invalid mode, missing variant IDs. |

Reuse existing `ProductErrorKeys` pattern; add constants matching resource names. Domain keys follow existing exception convention. No Application reference to Web. Web accesses generated `Strings` members; runtime errors use `Localizer[result.Error]`.

| New resource key | English text |
|---|---|
| `Product_VariantImageDuplicate` | Each image can appear only once in a variant gallery. |
| `Product_VariantImageSelectionInvalid` | Select images and variants from this product. |
| `ProductVariantImages_Apply` | Apply |
| `VariantImage_ApplyToAllSharing` | All {0} variants with {1} |
| `ProductVariantImages_LibraryHint` | Uploads are added to the product image library. |
| `ProductDetail_CombinationUnavailable` | This option combination is unavailable. |
| `ProductDetail_SelectedVariant` | Selected variant: {0} |

Reuse existing Save/Cancel, SKU, primary, unavailable, image-error, position and scope labels where equivalent. Do not add duplicate keys.

## 10. Database Schema & RLS Policies

Planned migration `0039_variant_urls_and_galleries.sql`. Run after 0038; transaction includes backfill. This is executable migration text, not applied during planning. Existing 0032 RPC remains private core to preserve stock-free saves, specifications, and SKU behavior.

```sql
BEGIN;

ALTER TABLE public.product_images
  ADD CONSTRAINT product_images_id_product_unique UNIQUE (id, product_id);
ALTER TABLE public.product_variants
  ADD CONSTRAINT product_variants_id_product_unique UNIQUE (id, product_id);

CREATE TABLE public.product_variant_images (
  product_id uuid NOT NULL REFERENCES public.products(id) ON DELETE CASCADE,
  variant_id uuid NOT NULL,
  image_id uuid NOT NULL,
  position integer NOT NULL CHECK (position >= 0),
  PRIMARY KEY (variant_id, image_id),
  UNIQUE (variant_id, position),
  FOREIGN KEY (variant_id, product_id)
    REFERENCES public.product_variants(id, product_id) ON DELETE CASCADE,
  FOREIGN KEY (image_id, product_id)
    REFERENCES public.product_images(id, product_id) ON DELETE CASCADE
);
CREATE INDEX product_variant_images_product_idx
  ON public.product_variant_images(product_id);
CREATE INDEX product_variant_images_image_idx
  ON public.product_variant_images(image_id);

CREATE TABLE public.product_routes (
  slug text PRIMARY KEY CHECK (slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
  product_id uuid UNIQUE REFERENCES public.products(id) ON DELETE SET NULL
);
CREATE TABLE public.product_variant_routes (
  product_slug text NOT NULL REFERENCES public.product_routes(slug),
  slug text NOT NULL CHECK (slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
  variant_id uuid UNIQUE REFERENCES public.product_variants(id) ON DELETE SET NULL,
  PRIMARY KEY (product_slug, slug)
);

ALTER TABLE public.product_variant_images ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.product_routes ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.product_variant_routes ENABLE ROW LEVEL SECURITY;

CREATE POLICY variant_images_read ON public.product_variant_images FOR SELECT
USING (EXISTS (SELECT 1 FROM public.products p
  WHERE p.id = product_id AND p.is_published)
  OR (SELECT public.authorize('products.view')));
CREATE POLICY product_routes_read ON public.product_routes FOR SELECT
USING (product_id IS NOT NULL AND EXISTS (SELECT 1 FROM public.products p
  WHERE p.id = product_id AND
    (p.is_published OR (SELECT public.authorize('products.view')))));
CREATE POLICY variant_routes_read ON public.product_variant_routes FOR SELECT
USING (variant_id IS NOT NULL AND EXISTS (
  SELECT 1 FROM public.product_variants v
  JOIN public.products p ON p.id = v.product_id
  JOIN public.product_routes r ON r.product_id = p.id
  WHERE v.id = variant_id AND r.slug = product_slug AND
    (p.is_published OR (SELECT public.authorize('products.view')))));

REVOKE ALL ON public.product_variant_images, public.product_routes,
  public.product_variant_routes FROM PUBLIC, anon, authenticated;
GRANT SELECT ON public.product_variant_images, public.product_routes,
  public.product_variant_routes TO anon, authenticated;

CREATE FUNCTION public.product_slug_base_007(label text, fallback text)
RETURNS text LANGUAGE sql IMMUTABLE SET search_path = public AS $$
  SELECT COALESCE(NULLIF(trim(both '-' FROM left(
    regexp_replace(lower(COALESCE(label, '')), '[^a-z0-9]+', '-', 'g'), 80)), ''), fallback);
$$;

CREATE FUNCTION public.ensure_product_routes_007(target uuid)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
  parent_slug text;
  base text;
  candidate text;
  suffix integer;
  row_count integer;
  item record;
BEGIN
  SELECT r.slug INTO parent_slug FROM public.product_routes r WHERE r.product_id = target;
  IF parent_slug IS NULL THEN
    SELECT public.product_slug_base_007(p.name, 'product') INTO base
      FROM public.products p WHERE p.id = target;
    IF base IS NULL THEN RAISE EXCEPTION 'product not found'; END IF;
    IF base ~ '^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$' THEN
      base := base || '-product';
    END IF;
    suffix := 1;
    LOOP
      candidate := base || CASE WHEN suffix = 1 THEN '' ELSE '-' || suffix::text END;
      INSERT INTO public.product_routes(slug, product_id)
        VALUES (candidate, target) ON CONFLICT DO NOTHING;
      GET DIAGNOSTICS row_count = ROW_COUNT;
      EXIT WHEN row_count = 1;
      suffix := suffix + 1;
    END LOOP;
    parent_slug := candidate;
  END IF;
  FOR item IN
    SELECT v.id, string_agg(ov.value, '-' ORDER BY ot.position, ov.position, ov.id) AS label
    FROM public.product_variants v
    LEFT JOIN public.product_variant_option_values l ON l.variant_id = v.id
    LEFT JOIN public.product_option_values ov ON ov.id = l.option_value_id
    LEFT JOIN public.product_option_types ot ON ot.id = ov.option_type_id
    WHERE v.product_id = target AND NOT EXISTS (
      SELECT 1 FROM public.product_variant_routes r WHERE r.variant_id = v.id)
    GROUP BY v.id, v.position ORDER BY v.position, v.id
  LOOP
    base := public.product_slug_base_007(item.label, 'variant');
    suffix := 1;
    LOOP
      candidate := base || CASE WHEN suffix = 1 THEN '' ELSE '-' || suffix::text END;
      INSERT INTO public.product_variant_routes(product_slug, slug, variant_id)
        VALUES (parent_slug, candidate, item.id) ON CONFLICT DO NOTHING;
      GET DIAGNOSTICS row_count = ROW_COUNT;
      EXIT WHEN row_count = 1;
      suffix := suffix + 1;
    END LOOP;
  END LOOP;
END;
$$;

INSERT INTO public.product_variant_images(product_id, variant_id, image_id, position)
SELECT v.product_id, v.id, i.id, 0
FROM public.product_variants v
JOIN public.product_images i ON i.id = v.image_id AND i.product_id = v.product_id;

DO $$
DECLARE item record;
BEGIN
  FOR item IN SELECT id FROM public.products ORDER BY id LOOP
    PERFORM public.ensure_product_routes_007(item.id);
  END LOOP;
END;
$$;

ALTER FUNCTION public.save_product(jsonb) RENAME TO save_product_core_007;
REVOKE ALL ON FUNCTION public.save_product_core_007(jsonb) FROM PUBLIC, anon, authenticated;

CREATE FUNCTION public.save_product(payload jsonb)
RETURNS TABLE(id uuid, updated_at timestamptz)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
  target uuid := (payload->'product'->>'id')::uuid;
  mode text := payload->>'mode';
  old_routes jsonb;
  old_images jsonb;
  item jsonb;
  images jsonb;
  variant uuid;
  saved_version timestamptz;
BEGIN
  IF mode IS NULL OR mode NOT IN ('create', 'update') THEN
    RAISE EXCEPTION 'invalid product save mode' USING ERRCODE = '22023';
  END IF;
  IF (mode = 'create' AND NOT COALESCE(public.authorize('products.create'), false))
    OR (mode = 'update' AND NOT COALESCE(public.authorize('products.edit'), false)) THEN
    RAISE EXCEPTION 'access denied' USING ERRCODE = '42501';
  END IF;
  -- Serialize existing-product snapshots with its optimistic save check.
  PERFORM 1 FROM public.products p WHERE p.id = target FOR UPDATE;
  SELECT COALESCE(jsonb_agg(jsonb_build_object(
    'product_slug', r.product_slug, 'slug', r.slug, 'variant_id', r.variant_id)), '[]'::jsonb)
  INTO old_routes FROM public.product_variant_routes r
  JOIN public.product_variants v ON v.id = r.variant_id WHERE v.product_id = target;
  SELECT COALESCE(jsonb_object_agg(q.variant_id::text, q.images), '{}'::jsonb)
  INTO old_images FROM (
    SELECT v.id AS variant_id, COALESCE(jsonb_agg(a.image_id ORDER BY a.position)
      FILTER (WHERE a.image_id IS NOT NULL), '[]'::jsonb) AS images
    FROM public.product_variants v
    LEFT JOIN public.product_variant_images a ON a.variant_id = v.id
    WHERE v.product_id = target GROUP BY v.id
  ) q;
  -- New clients send explicit IDs; core must not silently mint uncorrelated IDs.
  FOR item IN SELECT value FROM jsonb_array_elements(COALESCE(payload->'variants', '[]'::jsonb)) LOOP
    IF NULLIF(item->>'id', '') IS NULL THEN
      RAISE EXCEPTION 'variant id required' USING ERRCODE = '22023';
    END IF;
  END LOOP;
  SELECT c.updated_at INTO saved_version FROM public.save_product_core_007(payload) c;
  FOR item IN SELECT value FROM jsonb_array_elements(old_routes) LOOP
    UPDATE public.product_variant_routes r SET variant_id = (item->>'variant_id')::uuid
    WHERE r.product_slug = item->>'product_slug' AND r.slug = item->>'slug'
      AND EXISTS (SELECT 1 FROM public.product_variants v
        WHERE v.id = (item->>'variant_id')::uuid AND v.product_id = target);
  END LOOP;
  PERFORM public.ensure_product_routes_007(target);
  FOR item IN SELECT value FROM jsonb_array_elements(COALESCE(payload->'variants', '[]'::jsonb)) LOOP
    variant := (item->>'id')::uuid;
    IF item ? 'image_ids' THEN
      images := item->'image_ids';
      IF jsonb_typeof(images) IS DISTINCT FROM 'array' THEN
        RAISE EXCEPTION 'invalid variant image selection' USING ERRCODE = '22023';
      END IF;
    ELSE
      -- Old app saves preserve existing assignments; newly introduced legacy pins seed one image.
      images := COALESCE(old_images->(variant::text),
        CASE WHEN NULLIF(item->>'image_id', '') IS NULL THEN '[]'::jsonb
          ELSE jsonb_build_array(item->>'image_id') END);
      SELECT COALESCE(jsonb_agg(e.value ORDER BY e.ordinality), '[]'::jsonb) INTO images
      FROM jsonb_array_elements_text(images) WITH ORDINALITY e(value, ordinality)
      JOIN public.product_images i ON i.id = e.value::uuid AND i.product_id = target;
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements_text(images) e(value)
      WHERE e.value IS NULL OR NOT EXISTS (SELECT 1 FROM public.product_images i
        WHERE i.id = e.value::uuid AND i.product_id = target)) THEN
      RAISE EXCEPTION 'variant image not in gallery' USING ERRCODE = '23503';
    END IF;
    IF (SELECT count(*) <> count(DISTINCT e.value::uuid)
        FROM jsonb_array_elements_text(images) e(value)) THEN
      RAISE EXCEPTION 'duplicate variant image' USING ERRCODE = '23505';
    END IF;
    INSERT INTO public.product_variant_images(product_id, variant_id, image_id, position)
    SELECT target, variant, e.value::uuid, (e.ordinality - 1)::integer
      FROM jsonb_array_elements_text(images) WITH ORDINALITY e(value, ordinality);
    UPDATE public.product_variants v
      SET image_id = (images->>0)::uuid WHERE v.id = variant AND v.product_id = target;
  END LOOP;
  SELECT p.updated_at INTO saved_version FROM public.products p WHERE p.id = target;
  RETURN QUERY SELECT target, saved_version;
END;
$$;

REVOKE ALL ON FUNCTION public.product_slug_base_007(text, text),
  public.ensure_product_routes_007(uuid), public.save_product(jsonb)
  FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.save_product(jsonb) TO authenticated;
COMMIT;
```

**Error mapping:** named FK/duplicate failures and wrapper messages map to Section 9 keys. Existing SKU uniqueness mapping remains constraint-specific; do not classify every `23505` as duplicate images. `40001` stays modified-elsewhere. Authorization remains `42501`.

**Rollout:** migrate first, then deploy new app. New app always sends `image_ids`; empty clears assignments. Legacy payload omission preserves surviving assignments. Old app cannot edit full galleries; rollback keeps expanded schema/wrapper and existing data. Do not drop registry/assignment tables during application rollback. Test rollback save against migration fixture. No storage-object migration/deletion.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

---
**Status:** Resolved · **Spec:** `.specs/007_variant-urls-and-galleries/spec.md` · **Created:** 2026-11-17 · **Resolved:** 2026-11-17

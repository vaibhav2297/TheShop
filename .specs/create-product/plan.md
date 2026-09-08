# Implementation Plan — Create Product

> Companion to `.specs/create-product/spec.md`. This plan is technical (HOW); the spec is
> non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

Promote `products` from a read-only catalogue table into a managed admin aggregate: a paginated
admin list plus add/edit forms that persist product details, an ordered image gallery, per-product
option types, and one auto-generated variant per option-value combination — each variant carrying
its own SKU, pricing, stock, availability, and a single pinned gallery image. Invariants
(gallery primary, option/value uniqueness, variant-set = cartesian product, pin ∈ gallery,
publish completeness) live on the `Product` aggregate; writes are gated by `products.create` /
`products.edit` at the MediatR pipeline **and** by Supabase RLS. The feature also retires
`products.flavour` / `products.nicotine_strength_mg` (spec RULE-19) and, with them, the customer
catalogue's Flavour and Nicotine filter groups — `get_catalogue_filters()` instead publishes the
generic option types products actually carry, which the product-catalogue feature will later build
dynamic filter controls from.

## 2. Tech Stack

- **Domain:** C# (no external deps) — `Product` aggregate, `Sku`/`Money`/`ProductPricing` value objects.
- **Application:** MediatR, FluentValidation, `Result<T>`, hand-written DTO mappers, `IFileStorage` + `StorageArea.ProductImages`.
- **Infrastructure:** `supabase-csharp` (Postgrest + Storage), plpgsql RPCs for atomic aggregate writes.
- **Web:** MudBlazor, `BusyState`/`BusyKeys`/`Routes`, bUnit for component tests.
- **Persistence:** Supabase (PostgreSQL + RLS + Storage bucket `product-images`).

## 3. High-level Architecture

The product form is a single aggregate write: one page assembles one command carrying details,
gallery uploads, option types, and variant rows; the handler uploads images, rebuilds the
aggregate, and persists it in one transactional RPC so a half-saved product is impossible
(spec Section 5, "save fails … never partly saved").

```
Staff clicks "Create" in AddProduct.razor
   ↓
IMediator.Send(CreateProductCommand(details, sku, images[], optionTypes[], variants[]))
   ↓  AuthorizationBehavior ← [RequiresPermission("products.create")]
   ↓  ValidationBehavior    ← CreateProductCommandValidator
CreateProductHandler (Application)
   ├── IProductRepository.FindConflictsAsync(name, skus, excludeProductId)   // RULE-2 + RULE-8
   ├── IFileStorage.UploadAsync(StorageArea.ProductImages, …)                // per new image
   └── Product.Create(...) → .SetGallery(...) → .SetOptionTypes(...)         // invariants here
       → .ApplyVariantConfiguration(...) → .EnsurePublishable()              // RULE-14
   ↓
SupabaseProductRepository.AddAsync → rpc save_product(jsonb)  (RLS-gated, one transaction)
   ↓  on failure → IFileStorage.DeleteAsync for every image uploaded this request (RULE-18)
Result<AdminProductDto> → AddProduct.razor → Snackbar + NavigateTo(Routes.Admin.ManageProducts)
```

## 4. Data Model

### Domain entities & value objects

- **`Product`** (aggregate root, modified) — owns `Images`, `OptionTypes`, `Variants`.
  Methods: `Create`, `Rehydrate`, `UpdateDetails`, `SetSku`, `SetPricing`, `SetStock`, `SetGallery`,
  `SetPrimaryImage`, `RemoveImage`, `SetOptionTypes`, `ApplyVariantConfiguration`,
  `PinVariantImage(variantId, imageId, PinScope)`, `EnsurePublishable()`, `MinVariantPrice`,
  `HasSellableVariant`.
  Invariants: name required/≤150 and description ≤2000 (RULE-1); a `Sku` on every product
  (Decision 11); exactly one primary whenever any image exists (RULE-7); variant set is exactly the
  cartesian product of option values (RULE-10); surviving variants keep their configuration across
  an option change (RULE-12); every pin resolves to an image this product owns (RULE-13); publish
  completeness (RULE-14). `Pricing`/`StockQuantity` are **nullable** so an Unpublished draft can be
  saved incomplete (RULE-15). `Flavour` / `NicotineStrengthMg` are **removed** (RULE-19).
  Throws `ProductNameRequiredException`, `ProductNameTooLongException`,
  `ProductDescriptionTooLongException`, `ProductImageNotOwnedException`,
  `ProductNotPublishableException`.
- **`ProductImage`** — `Id`, `ObjectKey`, `Position`, `IsPrimary`. Behaviour: `MoveTo`, `MakePrimary`.
- **`ProductOptionType`** — `Id`, `Name`, `Position`, `Values`. Invariants: name required, ≥1 value,
  name unique within the product and value unique within the type, both on `lower(btrim(x))`
  (RULE-9). Throws `OptionTypeNameRequiredException`, `OptionTypeValueRequiredException`,
  `DuplicateOptionNameException`, `DuplicateOptionValueException`.
- **`ProductOptionValue`** — `Id`, `Value`, `Position`.
- **`ProductVariant`** — `Id`, `Sku`, `Pricing?`, `StockQuantity?`, `IsAvailable`, `PinnedImageId?`,
  `OptionValueIds`. `Pricing`/`StockQuantity` nullable for the same draft reason.
- **`Sku`** (value object) — `Create(string)` trims and rejects empty; `Normalized => lower(trim)`
  is the comparison key; `Suggest(string productName)` and `Suggest(Sku productSku, values)` build
  the uppercased slug forms of Decision 11. Throws `SkuRequiredException`.
- **`PinScope`** (enum) — `ThisVariantOnly` | `AllSharingOptionValue` (FR-16; Figma node `2680:14523`).

### DTOs (Application → Web)

- **`ProductListItemDto`** — `Id, Name, Sku, PrimaryImageUrl, BrandName, CategoryName, PriceLabel, IsPublished`.
- **`AdminProductDto`** — full edit payload: details + `Sku` + `IReadOnlyList<ProductImageDto>` +
  `IReadOnlyList<ProductOptionTypeDto>` + `IReadOnlyList<ProductVariantDto>` + `RowVersion`.
- **`ProductImageDto`** — `Id, Url, Position, IsPrimary`.
- **`ProductOptionTypeDto`** — `Id, Name, Position, IReadOnlyList<ProductOptionValueDto>`.
- **`ProductVariantDto`** — `Id, Sku, OriginalPrice?, SalePrice?, StockQuantity?, IsAvailable, PinnedImageId?, OptionValueIds, Label`.
- **`ProductImageUpload`** — `byte[] Content, string FileName, string ContentType` (new uploads).
- **`ProductGalleryEntry`** — either an existing `ImageId` or a `ProductImageUpload`, plus `Position` and `IsPrimary`.
- **`ProductSummaryDto`** (modified) — `Flavour` / `NicotineStrengthMg` **removed**;
  `MinVariantPrice?` and `HasSellableVariant` added.
- **`CatalogueOptionTypeDto`** — `Name, IReadOnlyList<string> Values`; the generic replacement for the
  retired flavour/nicotine facets (Decision 3).

### Database tables (new or modified)

| Table | Purpose | Key columns |
|---|---|---|
| `product_images` (new) | The product-owned gallery (FR-10, FR-11) | `id`, `product_id`, `object_key`, `position`, `is_primary` |
| `product_option_types` (new) | Per-product option type (FR-12) | `id`, `product_id`, `name`, `position` |
| `product_option_values` (new) | Values of an option type | `id`, `option_type_id`, `value`, `position` |
| `product_variants` (new) | One row per option-value combination (FR-13, FR-14) | `id`, `product_id`, `sku`, `original_price`, `sale_price`, `stock_quantity`, `is_available`, `image_id`, `position` |
| `product_variant_option_values` (new) | Which values compose a variant | `variant_id`, `option_value_id` |
| `product_sku_registry` (new) | The one shared SKU namespace across products and variants (Decision 11) | `sku_normalized` (PK), `product_id`, `variant_id` |
| `products` (modified) | Draft-tolerant, variant-aware, flavour-free, SKU-bearing | `sku` **NOT NULL**; `original_price`/`stock_quantity` → NULLable; **drop** `flavour`, `nicotine_strength_mg`; **add** `min_variant_price`, `has_sellable_variant`, `updated_at` |

### Indexes

- `ux_product_images_primary ON product_images(product_id) WHERE is_primary` — enforces RULE-7's "exactly one primary".
- `idx_product_images_product ON product_images(product_id, position)` — serves gallery load in saved order.
- `ux_products_name_normalized ON products(lower(btrim(name)))` — store-wide name uniqueness (RULE-2, Decision 7).
- `ux_product_option_types_name ON product_option_types(product_id, lower(btrim(name)))` — RULE-9.
- `ux_product_option_values_value ON product_option_values(option_type_id, lower(btrim(value)))` — RULE-9.
- `product_sku_registry` primary key on `sku_normalized` — the single store-wide SKU constraint (RULE-8, Decision 11).
- `idx_product_variants_product ON product_variants(product_id, position)` — serves the variant table's load.
- `idx_products_created_at ON products(created_at DESC)` — already present (0002); serves FR-2's newest-first list.

## 5. Core Design Decisions

1. **Decision:** `Product` stays the single aggregate root; images, option types, and variants are
   owned children with no repository of their own, and the whole aggregate is written in one
   `save_product(jsonb)` RPC.
   - **Why:** every rule that matters spans children — "exactly one primary" (RULE-7), "variants =
     cartesian product" (RULE-10), "pin ∈ this product's gallery" (RULE-13), publish completeness
     (RULE-14). Splitting them into sibling aggregates would push those invariants into a handler,
     violating constitution Rule 6. One RPC also delivers the spec's "never partly saved".
   - **Rejected:** separate `IProductImageRepository` / `IProductVariantRepository` with per-child
     commands — four round trips, no transaction boundary, invariants enforced nowhere.

2. **Decision:** A variant carries **at most one** `image_id`, a FK to `product_images` with
   `ON DELETE SET NULL`; the "apply to every variant sharing this option value" scope is a **UI
   fan-out**, resolved in the Web layer into N per-variant pins before the command is built.
   - **Why:** RULE-13 makes the pin a starting position, not a filter, so the stored truth is one
     nullable pointer per variant. `ON DELETE SET NULL` gives RULE-13's "removing a pinned image
     leaves those variants unpinned" for free — no application code, no trigger. Keeping the scope
     in the UI means editing one variant afterwards changes only that variant (spec Section 5 edge
     case), which a shared pin row could not express.
   - **Rejected:** a `variant_images` many-to-many (multiple pins per variant) — contradicts
     RULE-13; and pinning at the option-value level — cannot express AC-10a's "then pin a different
     image to Mango / 50mg alone".

3. **Decision:** `products.flavour` and `products.nicotine_strength_mg` are dropped, and the
   catalogue's Flavour and Nicotine **filter groups go with them**. `get_catalogue_filters()` stops
   returning `flavours` / `nicotine_strengths` and instead returns a generic
   `option_types: [{name, values[]}]` payload aggregated from published products' option types;
   `ProductFilterDefinitions` loses its two hardcoded entries and the catalogue renders category,
   brand, and price only until the product-catalogue feature builds dynamic controls.
   - **Why:** RULE-19 forbids the dedicated columns, and the spec's Section 4 cross-feature note
     assigns the filter *controls* to the product-catalogue feature while making this feature
     responsible for *supplying* the information. Publishing the generic `option_types` payload is
     exactly that supply, and it keeps AC-18's second clause verifiable without this feature
     building UI it does not own. **Disposition: the interim loss of flavour filtering is a
     knowingly accepted risk** (Section 11).
   - **Rejected:** a trigger-maintained `products.option_facets JSONB` read model preserving the two
     filter keys — carries a GIN index and a denormalization trigger purely to keep two filter
     groups alive that the next feature will replace anyway.

4. **Decision:** Publish completeness is a **domain** check (`Product.EnsurePublishable()` throwing
   `ProductNotPublishableException` carrying the list of missing parts), not a validator rule.
   - **Why:** RULE-14 depends on aggregate state that FluentValidation cannot see coherently (does
     this product have option types? then every variant needs a price). RULE-15 makes the *same*
     fields optional when Unpublished, so the rule is conditional on aggregate state — exactly
     constitution Rule 6's definition of domain behaviour.
   - **Rejected:** `When(x => x.IsPublished, …)` chains in the validator — duplicates the
     option-type branch in two layers and cannot flag the individual variant rows AC-20 requires.

5. **Decision:** Draft tolerance is expressed by making `products.original_price`,
   `products.stock_quantity`, `product_variants.original_price`, and
   `product_variants.stock_quantity` nullable, while `name`, `sku`, `category_id`, and `brand_id`
   stay `NOT NULL`.
   - **Why:** RULE-15 names exactly four values required at every save — name, SKU, category, brand
     — and RULE-3's outcome is unconditional ("the save is rejected"), unlike RULE-4's which is
     scoped to products with no option types. Everything else is optional until publish (RULE-14).
   - **Rejected:** sentinel `0` prices — indistinguishable from a legitimately free product and
     would silently satisfy RULE-14.

6. **Decision:** Every product carries its own SKU, and product SKUs and variant SKUs occupy **one
   shared store-wide namespace** enforced by a `product_sku_registry` table whose primary key is
   `lower(btrim(sku))`. The `save_product` RPC maintains registry rows in the same transaction as
   the aggregate write.
   - **Why:** the user's chosen model is one namespace, so no product SKU may collide with any other
     product's or with any variant's. A single-column primary key over a registry is the only way to
     express uniqueness spanning two tables atomically; `ON DELETE CASCADE` from both parents keeps
     it self-cleaning.
   - **Rejected:** one unique index per table plus a cross-check trigger — two constraints and a
     race window between them; and a check constraint with a subquery — not allowed in Postgres.

7. **Decision:** Product names are unique store-wide on `lower(btrim(name))` (RULE-2), enforced by
   `ux_products_name_normalized`. That is what makes the name-derived SKU suggestion collision-free,
   so collision handling needs no special case: **any** SKU already in the registry is refused with
   `Product_SkuAlreadyExists`, however it got onto the form.
   - **Why:** the SKU suggestion is a function of the name, so unique names give unique suggestions.
     One uniform refusal rule replaces a suggested-vs-typed split that would otherwise have been
     needed to keep the old "duplicate names always save" AC-22 alive. Slugification is lossy
     ("Elf Bar, BC5000" and "Elf Bar BC5000" both slug to `ELF-BAR-BC5000`), so the refusal path
     still exists as the backstop — the staff member edits the SKU and saves again (spec Section 5).
   - **Rejected:** silently disambiguating a colliding suggestion (`-2`, `-3`) — unnecessary once
     names are unique, and it hides a rewrite from the staff member.

8. **Decision:** Variant configuration is **inline** on the add/edit product page — a collapsible
   "Variants" card sitting between General Info and the Create/Cancel actions, matching Figma
   `2687:14713` (empty) and `2687:15441` (populated). There is no separate configure-variants route.
   - **Why:** the user restructured the design to put variant configuration on the product page; the
     current frames are single pages whose Variants card carries the running count in its header and
     the option/variant editor in its content slot. One page means one form state and one
     unsaved-changes guard.
   - **Rejected:** the earlier separate `/admin/products/{id}/variants` route (superseded by the new
     Figma frames); and a full-screen dialog — same state-splitting cost as a route with none of a
     route's addressability.

9. **Decision:** Form state (details, gallery bytes, option types, variant rows) is page-local to
   `AddProduct` / `EditProduct`; there is no draft store and nothing is uploaded or persisted before
   the staff member saves.
   - **Why:** Decision 8 removes the navigation that would have needed a shared store. Because
     `ShopImageUpload` reads files into `ShopUploadedImage` byte arrays client-side and only the
     save command uploads them, RULE-18's "uploaded to a product that was then abandoned without
     saving" is satisfied by construction — an abandoned form never wrote to storage, so AC-31
     needs no cleanup command.
   - **Rejected:** a scoped `ProductDraftState` store (needed only by the superseded route split);
     and auto-creating an Unpublished product on first edit — leaks half-products into the list.

10. **Decision:** The `product-images` bucket's Storage RLS policies are rewritten from the
    obsolete `auth.jwt() ->> 'role' = 'admin'` predicate (migration 0004) to
    `(SELECT public.authorize('products.{action}'))`.
    - **Why:** the RBAC feature replaced role-claim checks with `authorize()`
      (`rules/architecture-admin.md`, Common mistakes). Left as-is, **every** gallery upload would
      be refused for staff who legitimately hold `products.create`, because no token carries a
      `role: admin` claim any more. This is a prerequisite for FR-10, not an optional cleanup.
    - **Rejected:** leaving 0004 alone and uploading with a service key — puts a privileged key in
      a WASM client.

11. **Decision:** `products.updated_at` is the optimistic-concurrency token, surfaced to the Web as
    an opaque `RowVersion` string the client never parses. `save_product` guards on
    `WHERE updated_at = :row_version`; zero rows affected means someone else saved first.
    - **Why:** the spec's "changed by another staff member" edge case needs a token but names no
      mechanism, and `updated_at` is already the row's natural change marker.
    - **Rejected:** a separate `version INTEGER` column — an extra column for the same guarantee, as
      long as nothing else writes `updated_at` behind the aggregate's back.

## 6. Core Functional Flow

### Flow 1: Browse the product list (spec Behavior 1)

1. `ManageProducts.razor.cs` (`[AuthorizePermission("products.view")]`) initialises a
   `Paginator<ProductListItemDto>` with `PageSize = 10`, laid out like `ManageCategories` /
   `ManageBrands` minus search, filter, sort, and bulk selection (all out of scope).
2. `BusyState.RunAsync(BusyKeys.Products.ManageList, …)` → `Mediator.Send(new GetAdminProductsPageQuery(request))`.
3. `GetAdminProductsPageQueryValidator` rejects `Page < 1` → `Result.Fail(ProductErrorKeys.PageInvalid)`.
4. `GetAdminProductsPageHandler` → `IProductRepository.GetAdminPageAsync(request, ct)`.
5. `SupabaseProductRepository` selects `products` with embedded category/brand and primary image,
   `.Order("created_at", Descending)`, ranged by `PostgrestPaginationExtensions`. RLS's
   `products_admin_read` policy is what makes Unpublished rows visible (RULE-17).
6. Page renders `MudTable` + `ShopPagination`; the add control and each edit link sit inside
   `AuthorizeView Policy="@PolicyNames.Permission(...)"` so they are absent, not disabled (AC-3).

### Flow 2: Add a simple product with no variants (spec Behavior 2)

1. `AddProduct.razor` collects details; the SKU field pre-fills from the name via `Sku.Suggest` and
   stops re-deriving once the staff member edits it. `ProductGalleryEditor` collects
   `ShopUploadedImage`s client-side.
2. `Mediator.Send(new CreateProductCommand(...))` inside `BusyState.RunAsync(BusyKeys.Products.SaveProduct, …)`.
3. `AuthorizationBehavior` checks `products.create` → `Result.Fail(RbacErrorKeys.AccessDenied)` if absent.
4. `CreateProductCommandValidator` checks lengths, SKU presence, price/sale relation, stock
   integrality, image type/size, and one-primary.
5. `CreateProductHandler`: `FindConflictsAsync` → `Result.Fail(Product_NameAlreadyExists)` or
   `Product_SkuAlreadyExists` naming the offending field/rows → upload each image →
   `Product.Create(...)` + `SetGallery(...)` → `EnsurePublishable()` when `IsPublished` →
   `repository.AddAsync`.
6. `DomainException` → `Result.Fail(ex.MessageKey)`; any post-upload failure → best-effort
   `IFileStorage.DeleteAsync` for every key uploaded this request (RULE-18), then
   `Result.Fail(ProductErrorKeys.CreateFailed)`.
7. `Snackbar.Add(string.Format(Strings.Product_CreatedPublished, name))` → `Nav.NavigateTo(Routes.Admin.ManageProducts)`.

### Flow 3: Build the image gallery (spec Behavior 3)

1. `ProductGalleryEditor` wraps `ShopImageUpload` (`Multiple="true"`, no count cap, 2 MB /
   PNG-JPG-WebP) and adds reorder + "make primary" + remove controls.
2. Rejected files raise the component's `InvalidTypeError` / `MaxFileSizeError` inline; the rest of
   the form is untouched (AC-8).
3. Removing the primary promotes the next entry client-side; the server re-asserts it in
   `Product.RemoveImage` so a direct command cannot leave a gallery primary-less (RULE-7).
4. Gallery order and primary flag travel as `ProductGalleryEntry.Position` / `.IsPrimary`; the RPC
   deletes rows absent from the payload, and their storage objects are deleted by the handler.

### Flow 4: Define option types and get variants (spec Behavior 4)

1. The inline Variants card (Figma `2687:15451`) shows the empty state — "No Variants Configured"
   plus the "Add Variant" button — until the first option type exists.
2. Every option edit recomputes the cartesian product in page state for immediate feedback,
   preserving each surviving row by its option-value-id set (RULE-12) and generating
   `{ProductSku}-{Value}-{Value}` SKU suggestions (FR-15, Decision 7).
3. The running count renders in the card header (Figma `I2687:14723;2137:6238` — "3 Variants") and
   is announced via `AnnouncementState` (spec accessibility constraint).
4. `Product.ApplyVariantConfiguration` re-derives the same set server-side and rejects any row whose
   combination is not in the product (RULE-10) — the client computation is convenience, not truth.

### Flow 5: Configure each variant, including the image pin (spec Behavior 5, AC-10a)

1. The pin cell opens `VariantImageDialog` via `ShopDialog` (Figma `2680:14477`), showing the
   gallery and the "Apply To" choice: *This Variant Only* / *All N Variants with {Option} = {Value}*.
2. On confirm, the page fans the choice out to every affected variant row — each row stores its own
   `PinnedImageId` (Decision 2).
3. On save, `Product.PinVariantImage` re-validates each pin against the gallery, throwing
   `ProductImageNotOwnedException` → `Result.Fail(ProductErrorKeys.VariantImageNotInGallery)`.

### Flow 6: Change the options after variants are configured (spec Behavior 6)

1. Removing an option type or value opens `ShopConfirmDialog` naming how many **configured**
   variants (SKU/price/stock entered) will be discarded (RULE-11).
2. Cancel → no state change. Confirm → those rows are dropped; the gallery is untouched (AC-13).
3. Removing the last option type restores the product-level price and stock fields (AC-14).

### Flow 7: Edit an existing product (spec Behavior 7)

1. `EditProduct.razor` (`[AuthorizePermission("products.edit")]`, route `Routes.Admin.EditProductPattern`)
   sends `GetProductForEditQuery(id)`; a miss → `Result.Fail(ProductErrorKeys.NotFound)` renders the
   not-found panel with a link back to the list (AC-34).
2. `AuthorizingView` covers the load so no field appears empty-but-editable (spec Section 5).
3. Save sends `UpdateProductCommand` carrying the loaded `RowVersion`; the RPC's
   `WHERE updated_at = :row_version` guard returns zero rows on a concurrent edit →
   `Result.Fail(ProductErrorKeys.ModifiedElsewhere)`, and the page reloads current state rather than
   overwriting (Decision 11).

### Flow 8: Publish an incomplete product (spec Behavior 9)

1. `EnsurePublishable()` collects every missing part into `ProductNotPublishableException.Missing`.
2. The handler returns `Result.Fail(ProductErrorKeys.NotPublishable)` plus the structured missing
   list on the DTO; the page flags each field and variant row in place and leaves the status
   unchanged (AC-20).

## 7. Development Plan

### Step 1 — Domain (`shop-domain-implementer`)

**Depends on:** resolved plan.

- [ ] **TASK-001** — `Sku` value object in `Domain/ValueObjects/` (trim, non-empty, `Normalized`, `Suggest` overloads per Decision 7) + `SkuRequiredException`.
- [ ] **TASK-002** — `ProductImage` entity in `Domain/Entities/` (`ObjectKey`, `Position`, `IsPrimary`, `MoveTo`, `MakePrimary`).
- [ ] **TASK-003** — `ProductOptionType` + `ProductOptionValue` entities with RULE-9 uniqueness/emptiness invariants; `OptionTypeNameRequiredException`, `OptionTypeValueRequiredException`, `DuplicateOptionNameException`, `DuplicateOptionValueException`.
- [ ] **TASK-004** — `ProductVariant` entity (nullable `Pricing`/`StockQuantity`, `IsAvailable`, `PinnedImageId`, `OptionValueIds`) + `PinScope` enum in `Domain/Enums/`.
- [ ] **TASK-005** — Rework `Product`: required `Sku`, nullable `Pricing`/`StockQuantity`, remove `Flavour`/`NicotineStrengthMg`, add `Images`/`OptionTypes`/`Variants` + `MinVariantPrice`/`HasSellableVariant`; methods `UpdateDetails`, `SetSku`, `SetGallery`, `SetPrimaryImage`, `RemoveImage`, `SetOptionTypes`, `ApplyVariantConfiguration` (RULE-10 + RULE-12 preservation by option-value-id set), `PinVariantImage` (RULE-13).
- [ ] **TASK-006** — `EnsurePublishable()` + `ProductNotPublishableException` carrying the structured missing-parts list (RULE-14); `ProductNameRequiredException`, `ProductNameTooLongException`, `ProductDescriptionTooLongException`, `ProductImageNotOwnedException`.
- [ ] **TASK-007** — Domain unit tests: `ProductTests`, `ProductVariantTests`, `ProductOptionTypeTests`, `SkuTests` — one test per invariant above.

**Completion gate:** Domain builds · every invariant in Section 4 covered by a test · no outer-layer
type leaked into Domain · public API reported for the Application step.

### Step 2 — Application (`shop-application-implementer`)

**Depends on:** Step 1's reported Domain API.

- [ ] **TASK-008** — `ProductErrorKeys` in `Features/Products/` with every key from Section 9.
- [ ] **TASK-009** — DTOs under `Features/Products/DTOs/`: `ProductListItemDto`, `AdminProductDto`, `ProductImageDto`, `ProductOptionTypeDto`, `ProductOptionValueDto`, `ProductVariantDto`, `ProductImageUpload`, `ProductGalleryEntry`, `OptionTypeInput`, `VariantInput`, `CatalogueOptionTypeDto`; modify `ProductSummaryDto` (drop `Flavour`/`NicotineStrengthMg`, add `MinVariantPrice`/`HasSellableVariant`); mappers in `Features/Products/Mappers/`.
- [ ] **TASK-010** — Extend `IProductRepository`: `GetAdminPageAsync(PaginationRequest, ct)`, `GetForEditAsync(Guid, ct)`, `AddAsync(Product, ct)`, `UpdateAsync(Product, string rowVersion, ct)`, `FindConflictsAsync(string name, IReadOnlyCollection<string> normalizedSkus, Guid? excludeProductId, ct)` returning the conflicting name and/or SKUs (RULE-2, RULE-8).
- [ ] **TASK-011** — `CreateProductCommand` (`[RequiresPermission("products.create")]`) + handler + validator in `Features/Products/Commands/CreateProduct/`, including the RULE-18 upload-compensation path.
- [ ] **TASK-012** — `UpdateProductCommand` (`[RequiresPermission("products.edit")]`) + handler + validator in `Commands/UpdateProduct/`: diffs the gallery, deletes storage objects for removed images, and honours `RowVersion` for the concurrent-edit outcome.
- [ ] **TASK-013** — `GetAdminProductsPageQuery` + handler + validator in `Queries/GetAdminProductsPage/` (`[RequiresPermission("products.view")]`).
- [ ] **TASK-014** — `GetProductForEditQuery` + handler in `Queries/GetProductForEdit/` (`[RequiresPermission("products.view")]`).
- [ ] **TASK-015** — New keys in `Strings.resx` (Section 9) mirrored in `Strings.fr.resx`; retire `Filter_Flavour` / `Filter_NicotineStrength` once TASK-022 confirms them unreferenced.
- [ ] **TASK-016** — Application unit tests: handler tests for TASK-011 through TASK-014, validator tests, mapper tests, and a `FindConflictsAsync` contract test covering a duplicate name (AC-22) and a duplicate SKU on the product and on a variant row (AC-27).

**Completion gate:** command/query folders follow the per-command convention · every admin request
carries `[RequiresPermission]` · handler translates every Section 9 outcome · all contracts consumed
by Infra/Web compile.

### Step 3 — Contract freeze

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `IProductRepository` (extended) | Application | Infrastructure | Stable |
| `CreateProductCommand` / `UpdateProductCommand` / `Result<AdminProductDto>` | Application | Web | Stable |
| `GetAdminProductsPageQuery` / `Result<PagedResult<ProductListItemDto>>` | Application | Web | Stable |
| `GetProductForEditQuery` / `Result<AdminProductDto>` | Application | Web | Stable |
| `ProductGalleryEntry` / `OptionTypeInput` / `VariantInput` shapes | Application | Web | Stable |
| `ProductSummaryDto` / `CatalogueOptionTypeDto` | Application | Infrastructure, Web | Stable |
| `StorageArea.ProductImages` + `IFileStorage` | Application | Infrastructure | Stable (unchanged) |

### Step 4 — Infrastructure (`shop-infra-implementer`) — runs in parallel with Step 5

**Depends on:** contract freeze (`IProductRepository` stable).

- [ ] **TASK-017** — Migration `0022_create_product_variants.sql`: the six new tables (including `product_sku_registry`), indexes, and RLS policies from Section 10, applied via Supabase MCP.
- [ ] **TASK-018** — Migration `0023_products_admin_writes.sql`: `products` write policies + admin read policy; add `ux_products_name_normalized` (RULE-2); add `sku` (back-filled from the name slug, then `NOT NULL`) and register the seeded values; nullable `original_price`/`stock_quantity`; drop `flavour`/`nicotine_strength_mg`; add `min_variant_price`/`has_sellable_variant`/`updated_at` with their refresh trigger; redefine `get_catalogue_filters()` per Decision 3; **rewrite the `product-images` bucket policies off `auth.jwt() ->> 'role'` onto `authorize()`** (Decision 10).
- [ ] **TASK-019** — Migration `0024_save_product_rpc.sql`: `save_product(payload JSONB)` — one transactional upsert of product + gallery + option types + values + variants + pins + registry rows, sequencing **all deletes before all inserts** so a SKU moving between rows cannot self-collide; `SECURITY DEFINER` with an `authorize('products.create'|'products.edit')` guard and the `updated_at` concurrency check.
- [ ] **TASK-020** — Records in `Persistence/Records/` (`ProductImageRecord`, `ProductOptionTypeRecord`, `ProductOptionValueRecord`, `ProductVariantRecord`, `SaveProductResultRecord`) + mappers in `Persistence/Mappers/`; extend `ProductRecord` and `ProductMapper` for the new/removed columns.
- [ ] **TASK-021** — Implement the new `IProductRepository` members on `SupabaseProductRepository` (admin page with embedded joins, edit load, `save_product` invocation, `ResolveSkuConflictsAsync` against `product_sku_registry`), translating unique-violation `23505` on the registry to `ProductErrorKeys.SkuAlreadyExists` and zero-row concurrency to `ProductErrorKeys.ModifiedElsewhere`.
- [ ] **TASK-022** — Retire the flavour/nicotine catalogue read path (Decision 3): remove those entries from `ProductFilterDefinitions` and `ProductFilterKeys`, drop the fields from `CatalogueFiltersRecord` and add `option_types`, update `ProductDtoMapper` and `CatalogueQueryState`.
- [ ] **TASK-023** — Infrastructure schema tests (`SupabaseProductAdminSchemaTests`, extending `SupabaseProductRepositorySchemaTests`): RLS denial for a permission-less caller, shared-namespace SKU uniqueness, a SKU swapped between two variant rows in one save, primary-image uniqueness, `ON DELETE SET NULL` unpinning, and the `min_variant_price` / `has_sellable_variant` trigger.

**Completion gate:** migrations apply cleanly · RLS policies match Section 10 verbatim · repository
satisfies the frozen interface · the existing catalogue tests pass against the retired filter groups ·
no Infrastructure type leaks inward.

### Step 5 — Web (`shop-ui-implementer`) — runs in parallel with Step 4

**Depends on:** contract freeze (command/DTO shapes stable).

**Figma references**

- **File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop
- **Nodes:**
  - `2687:14713` — **Create Products – Empty**: the authoritative add-product page in its no-variants state; body is a General Info card, a Variants card showing "No Variants Configured" plus an "Add Variant" button, and the Create / Cancel actions.
  - `2687:15441` — **Create Products**: the same page populated — General Info filled and the Variants card expanded to the option editor and per-variant table.
  - `2687:14722` / `2687:15450` — the **General Info** card instance holding name, description, category, brand, SKU, price, sale price, stock, and status.
  - `2687:14723` / `2687:15451` — the **Variants** card instance; its header carries the running count ("3 Variants", node `I2687:14723;2137:6238`) and a collapse control, its content slot the empty state or the variant table.
  - `2687:14724` / `2687:15452` — the form's **Actions** row (Create, Cancel).
  - `2680:14477` — **Image Selection Dialog**: the pin picker — header (`2680:14478`), content (`2680:14481`) showing the variant label, the selectable gallery strip (`2680:14489`), and the "Apply To" choice (`2680:14523`: *All N Variants with Color = Red* / *This Variant Only*), and the actions row (`2680:14496`).
  - *No node for the admin product list* — TASK-026 follows the existing `ManageCategories` / `ManageBrands` table layout, minus search, filter, sort, and bulk selection.
  - Superseded, do **not** implement: `2629:4429`, `2660:9877`, `2668:13315` (the earlier separate configure-variants route — see Decision 8).

- [ ] **TASK-024** — `ShopDialog` in `Components/Common/` (header title + close, `ChildContent`, `Actions` render fragments; inherits `MudComponentBase`, forwards `Class`/`Style` via `CssBuilder`/`StyleBuilder`); refactor `ShopConfirmDialog` to compose it (user direction, constitution Rule 25).
- [ ] **TASK-025** — `Routes.Admin.AddProduct` / `EditProductPattern` / `EditProduct(Guid)`; `BusyKeys.Products.{ManageList, LoadProduct, SaveProduct}`.
- [ ] **TASK-026** — `ManageProducts.razor(.cs)` — replace the harness shell with the real paginated list (`MudTable` + `ShopPagination`, 10/page, newest first, no search/filter/sort/bulk), permission-gated add and edit affordances, empty and loading states.
- [ ] **TASK-027** — `AddProduct.razor(.cs)` and `EditProduct.razor(.cs)` matching nodes `2687:14713` / `2687:15441` / `2687:14722` / `2687:14724`: details fields, the name-derived SKU suggestion, category/brand selects restricted to Active (showing an inactive current assignment), status control, price-range note when variants exist, and per-field validation.
- [ ] **TASK-028** — `ProductGalleryEditor` in `Components/Products/`: wraps `ShopImageUpload`, adds reorder, make-primary, and remove with the primary-promotion rule (FR-11, RULE-7).
- [ ] **TASK-029** — `ProductVariantsCard` in `Components/Products/` matching node `2687:14723` / `2687:15451`: the collapsible card with its running count, the empty state, the option-type editor (add/rename/remove with the RULE-11 confirmation via `ShopConfirmDialog`), and the generated variant table with SKU suggestions — rendered with `MudTable` `Virtualize` so an uncapped variant count stays responsive.
- [ ] **TASK-030** — `VariantImageDialog` on `ShopDialog`, matching node `2680:14477` including the "Apply To" scope, fanning the choice out per Decision 2.
- [ ] **TASK-031** — Unsaved-changes guard on both forms (`NavigationLock` + `ShopConfirmDialog`); no storage cleanup is needed because nothing is uploaded before save (Decision 9).
- [ ] **TASK-032** — Resource strings from Section 9 wired through `Strings.{Key}` / `Localizer[result.Error]`; ARIA labels, `AnnouncementState` announcements for upload progress and variant-list changes, focus handling on every dialog.
- [ ] **TASK-033** — bUnit component tests for TASK-024 and TASK-026 through TASK-031.

**Completion gate:** matches the Figma nodes above · MudBlazor-only, no hardcoded strings or design
tokens (constitution Rules 2–5, 11, 14–16) · consumes only frozen contracts · a user without
`products.view` / `products.create` / `products.edit` sees `AccessDeniedView`, and the controls they
lack are absent rather than disabled.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5 complete.

- [ ] **TASK-034** — Cross-layer verification: solution builds, DI resolves, migrations applied, the customer catalogue still renders and filters on category/brand/price after the flavour/nicotine retirement, and the create → configure variants → publish → edit round trip works end to end.
- [ ] **TASK-035** — Run `/theshop.test create-product` → `/theshop.e2e create-product` → `/theshop.review create-product` → `/theshop.document`.

### Deviation procedure

- **Accept** a deviation only if it preserves approved behavior and layer boundaries, aligns better
  with existing project conventions, and doesn't weaken authorization/integrity or expand scope.
- **Reject** it if it changes a requirement, adds business behavior, violates dependency direction,
  silently alters a frozen contract, or smuggles in unrelated refactoring.
- **Contract change:** stop dependent work → record the change here (update the freeze table and
  affected TASK ids) → resume only after the contract is re-frozen.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: list shows 10 newest, published + unpublished, no search/filter/sort | TASK-013, TASK-018 (admin read policy), TASK-021, TASK-026 |
| AC-2: next page shows the next 10 in the same order | TASK-013, TASK-021, TASK-026 |
| AC-3: view-only staff see no add/edit affordance; direct link denied | TASK-011, TASK-012 (`[RequiresPermission]`), TASK-026, TASK-027 |
| AC-4: create a published product end to end | TASK-005, TASK-011, TASK-019, TASK-021, TASK-027, TASK-028 |
| AC-5: add form opens empty and Unpublished | TASK-025, TASK-027 |
| AC-6: edit form pre-filled with everything saved | TASK-014, TASK-020, TASK-021, TASK-027, TASK-029 |
| AC-7: reorder, re-primary, remove; removed image no longer stored | TASK-002, TASK-005, TASK-012, TASK-028 |
| AC-8: bad type/size refused; no image-count cap | TASK-011, TASK-028 |
| AC-9: two option types produce exactly four variants; product stock field withdrawn | TASK-003, TASK-005, TASK-027, TASK-029 |
| AC-10: per-variant price/stock/sale/pin/unavailable render on their rows | TASK-004, TASK-005, TASK-029, TASK-030 |
| AC-10a: pin with option-value scope, then re-pin one row alone | TASK-004, TASK-005 (`PinVariantImage`), TASK-024, TASK-030 |
| AC-11: suggested product and variant SKUs accepted or overwritten, both persist | TASK-001, TASK-019, TASK-027, TASK-029 |
| AC-12: adding a value preserves every existing variant | TASK-005 (RULE-12 preservation), TASK-029 |
| AC-13: removing a value confirms, discards only its variants, keeps the gallery | TASK-005, TASK-019, TASK-029 |
| AC-14: removing the last option type restores product price/stock | TASK-005, TASK-027, TASK-029 |
| AC-15: very large variant counts generated and shown, never refused | TASK-005, TASK-017 (no cap), TASK-029 (virtualized table) |
| AC-16: no-variant product shows its own price and stock | TASK-005, TASK-018 (trigger no-op), TASK-022 |
| AC-17: form states the customer sees a range from the lowest variant price | TASK-018 (`min_variant_price`), TASK-027 |
| AC-18: no flavour/nicotine field; option values are what the catalogue offers | TASK-005, TASK-009 (`CatalogueOptionTypeDto`), TASK-018 (`get_catalogue_filters`), TASK-022, TASK-027 |
| AC-19: draft with name + SKU + category + brand saves and reopens intact | TASK-005, TASK-011, TASK-014, TASK-018 (nullable columns), TASK-027 |
| AC-20: publishing an incomplete product is refused with the missing list | TASK-006, TASK-011, TASK-027, TASK-029 |
| AC-21: name blank/too long, description too long | TASK-006, TASK-011, TASK-027 |
| AC-22: duplicate product name is refused | TASK-010, TASK-011, TASK-016, TASK-018 (`ux_products_name_normalized`) |
| AC-23: only Active categories/brands offered; both required | TASK-011, TASK-027 |
| AC-24: deactivated assigned category still saveable | TASK-012, TASK-027 |
| AC-25: sale price must be below price, product or variant | TASK-004, TASK-011, TASK-017 (CHECK), TASK-027, TASK-029 |
| AC-26: stock must be a whole number ≥ 0 | TASK-011, TASK-017 (CHECK), TASK-027, TASK-029 |
| AC-27: duplicate product or variant SKU flagged in place; own SKUs raise no conflict | TASK-001, TASK-010, TASK-017 (`product_sku_registry`), TASK-021, TASK-027, TASK-029 |
| AC-28: duplicate/empty option names and values refused; per-product scope | TASK-003, TASK-011, TASK-017, TASK-029 |
| AC-29: all-unavailable/zero-stock published product shows out of stock | TASK-018 (`has_sellable_variant`), TASK-022 |
| AC-30: unpublishing hides from customers, keeps admin row and images | TASK-005, TASK-012, TASK-018 (`products_public_read`), TASK-026 |
| AC-31: leave-with-unsaved-changes warns; images discarded on leave | TASK-031 (nothing uploaded before save — Decision 9) |
| AC-32: a failed save preserves everything and never partly saves | TASK-011, TASK-012, TASK-019 (single transaction), TASK-027 |
| AC-33: no `products.view` → access denied, including direct link | TASK-013, TASK-018 (RLS), TASK-026 |
| AC-34: stale edit link → not-found with a way back; rename-proof id URL | TASK-014, TASK-025, TASK-027 |
| AC-35: empty list state, add option only with the permission | TASK-013, TASK-026 |
| AC-36: every string follows the active EN/FR language | TASK-015, TASK-032 |
| AC-37: full keyboard/screen-reader operability across list, forms, gallery, variants, dialogs | TASK-024, TASK-028, TASK-029, TASK-030, TASK-032 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

- `CreateProductCommandValidator` / `UpdateProductCommandValidator`:
  - `Name` not empty, trimmed length ≤ 150 (RULE-1) → `Product_NameRequired` / `Product_NameTooLong`
  - `Description` trimmed length ≤ 2000 (RULE-1) → `Product_DescriptionTooLong`
  - `Sku` not empty (RULE-8, Decision 6) → `Product_SkuRequired`
  - `CategoryId` / `BrandId` not empty (RULE-3) → `Product_CategoryRequired` / `Product_BrandRequired`
  - `OriginalPrice` when present > 0 and ≤ 2 decimals (RULE-4) → `Product_PriceInvalid`
  - `SalePrice` when present > 0 and < `OriginalPrice`, on the product and on every variant (RULE-5) → `Product_SalePriceTooHigh`
  - `StockQuantity` when present ≥ 0 and integral, product and variant (RULE-6) → `Product_StockInvalid`
  - Each image `ContentType ∈ {image/png, image/jpeg, image/webp}` and ≤ 2 MB (RULE-7) → `Product_ImageInvalidType` / `Product_ImageTooLarge`
  - Exactly one gallery entry flagged primary whenever any entry exists (RULE-7) → `Product_PrimaryImageRequired`
  - Every variant `Sku` non-blank and distinct within the request against each other **and** the product SKU, case/space-insensitive (RULE-8, Decision 6) → `Product_SkuRequired` / `Product_SkuDuplicatedInRequest`
  - Each option type has a name and ≥ 1 value; names unique per product, values unique per type (RULE-9) → `Product_OptionNameRequired` / `Product_OptionValueRequired` / `Product_OptionNameDuplicated` / `Product_OptionValueDuplicated`
- `GetAdminProductsPageQueryValidator`: `Page ≥ 1`, `PageSize = 10` → `Product_PageInvalid`

### Domain exceptions

- `ProductNameRequiredException` / `ProductNameTooLongException` / `ProductDescriptionTooLongException` — `MessageKey = "Product_NameRequired"` / `"Product_NameTooLong"` / `"Product_DescriptionTooLong"`.
- `SkuRequiredException` — `"Product_SkuRequired"`.
- `OptionTypeNameRequiredException` / `OptionTypeValueRequiredException` / `DuplicateOptionNameException` / `DuplicateOptionValueException` — the four `Product_Option*` keys above.
- `ProductImageNotOwnedException` — thrown when a pin or primary flag names an image outside the product's gallery (RULE-13). `"Product_VariantImageNotInGallery"`.
- `ProductNotPublishableException` — thrown by `EnsurePublishable()` (RULE-14); carries `IReadOnlyList<string> Missing`. `"Product_NotPublishable"`.

### Result.Fail error keys (new entries in `Strings.resx`)

| Key | English text |
|---|---|
| `Product_NameRequired` | "A product name is required." |
| `Product_NameTooLong` | "A product name cannot exceed 150 characters." |
| `Product_DescriptionTooLong` | "A description cannot exceed 2000 characters." |
| `Product_SkuRequired` | "A SKU is required." |
| `Product_SkuDuplicatedInRequest` | "The SKU {0} is used more than once on this product." |
| `Product_SkuAlreadyExists` | "The SKU {0} is already in use." |
| `Product_NameAlreadyExists` | "A product named {0} already exists." |
| `Product_CategoryRequired` | "Select a category for this product." |
| `Product_BrandRequired` | "Select a brand for this product." |
| `Product_PriceInvalid` | "Enter a price greater than zero, with at most two decimal places." |
| `Product_SalePriceTooHigh` | "The sale price must be lower than the price." |
| `Product_StockInvalid` | "Stock must be a whole number of zero or more." |
| `Product_ImageInvalidType` | "Images must be PNG, JPG, or WebP." |
| `Product_ImageTooLarge` | "Each image must be 2 MB or smaller." |
| `Product_PrimaryImageRequired` | "Choose which image is the product's primary image." |
| `Product_OptionNameRequired` | "Every option type needs a name." |
| `Product_OptionValueRequired` | "Every option type needs at least one value." |
| `Product_OptionNameDuplicated` | "This product already has an option type named {0}." |
| `Product_OptionValueDuplicated` | "This option type already has the value {0}." |
| `Product_VariantImageNotInGallery` | "A variant can only be pinned to one of this product's own images." |
| `Product_NotPublishable` | "This product cannot be published yet — the following are missing: {0}." |
| `Product_ModifiedElsewhere` | "This product has changed since you opened it. Review the current version before saving." |
| `Product_NotFound` | "That product could not be found." |
| `Product_CreateFailed` | "The product was not saved. Please try again." |
| `Product_UpdateFailed` | "The changes were not saved. Please try again." |
| `Product_PageInvalid` | "That page of products does not exist." |
| `Product_Created` | "{0} was saved as Unpublished and is not visible to customers." |
| `Product_CreatedPublished` | "{0} was published." |
| `Product_Updated` | "{0} was updated." |

All keys mirrored in `Strings.fr.resx` (`[TODO]` placeholder acceptable for the first pass — the
review step's French-completeness gate catches stragglers). UI-only labels (page titles, column
headings, dialog text, the "Apply To" options, empty states) are added alongside under the
`ManageProducts_*` / `AddProduct_*` / `EditProduct_*` / `ProductVariants_*` / `VariantImage_*`
prefixes. `Filter_Flavour` and `Filter_NicotineStrength` are retired with their filter groups.

## 10. Database Schema & RLS Policies

### Schema

```sql
-- ---------------------------------------------------------------- 0022
CREATE TABLE product_images (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id  UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    object_key  TEXT NOT NULL,
    position    INTEGER NOT NULL CHECK (position >= 0),
    is_primary  BOOLEAN NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_product_images_primary ON product_images(product_id) WHERE is_primary;
CREATE INDEX idx_product_images_product ON product_images(product_id, position);

CREATE TABLE product_option_types (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id  UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    name        TEXT NOT NULL CHECK (btrim(name) <> ''),
    position    INTEGER NOT NULL CHECK (position >= 0)
);
CREATE UNIQUE INDEX ux_product_option_types_name
    ON product_option_types(product_id, lower(btrim(name)));

CREATE TABLE product_option_values (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    option_type_id  UUID NOT NULL REFERENCES product_option_types(id) ON DELETE CASCADE,
    value           TEXT NOT NULL CHECK (btrim(value) <> ''),
    position        INTEGER NOT NULL CHECK (position >= 0)
);
CREATE UNIQUE INDEX ux_product_option_values_value
    ON product_option_values(option_type_id, lower(btrim(value)));

CREATE TABLE product_variants (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id      UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    sku             TEXT NOT NULL CHECK (btrim(sku) <> ''),
    original_price  NUMERIC(10,2) CHECK (original_price IS NULL OR original_price > 0),
    sale_price      NUMERIC(10,2) CHECK (sale_price IS NULL OR sale_price > 0),
    stock_quantity  INTEGER CHECK (stock_quantity IS NULL OR stock_quantity >= 0),
    is_available    BOOLEAN NOT NULL DEFAULT TRUE,
    -- RULE-13: removing a pinned gallery image unpins its variants automatically.
    image_id        UUID REFERENCES product_images(id) ON DELETE SET NULL,
    position        INTEGER NOT NULL CHECK (position >= 0),
    CONSTRAINT variant_sale_below_original
        CHECK (sale_price IS NULL OR original_price IS NULL OR sale_price < original_price)
);
CREATE INDEX idx_product_variants_product ON product_variants(product_id, position);

CREATE TABLE product_variant_option_values (
    variant_id      UUID NOT NULL REFERENCES product_variants(id) ON DELETE CASCADE,
    option_value_id UUID NOT NULL REFERENCES product_option_values(id) ON DELETE CASCADE,
    PRIMARY KEY (variant_id, option_value_id)
);

-- Decision 6: ONE store-wide SKU namespace spanning products and variants (RULE-8). A single
-- primary key is the only way to express uniqueness across two tables atomically; cascades from
-- both parents keep it self-cleaning.
CREATE TABLE product_sku_registry (
    sku_normalized  TEXT PRIMARY KEY,
    product_id      UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    variant_id      UUID REFERENCES product_variants(id) ON DELETE CASCADE
);
CREATE INDEX idx_product_sku_registry_product ON product_sku_registry(product_id);

-- ---------------------------------------------------------------- 0023
-- RULE-2 (Decision 7): unique names are what keep name-derived SKU suggestions from colliding.
-- The 0002 seed names are already distinct, so this index applies without a de-duplication pass.
CREATE UNIQUE INDEX ux_products_name_normalized ON products (lower(btrim(name)));

ALTER TABLE products ADD COLUMN IF NOT EXISTS sku TEXT;
UPDATE products SET sku = upper(regexp_replace(btrim(name), '[^A-Za-z0-9]+', '-', 'g'))
    WHERE sku IS NULL;                                            -- back-fill the 0002 seed rows
ALTER TABLE products ALTER COLUMN sku SET NOT NULL;
INSERT INTO product_sku_registry (sku_normalized, product_id)
    SELECT lower(btrim(sku)), id FROM products ON CONFLICT DO NOTHING;

ALTER TABLE products ALTER COLUMN original_price DROP NOT NULL;   -- RULE-15 drafts
ALTER TABLE products ALTER COLUMN stock_quantity DROP NOT NULL;
ALTER TABLE products DROP COLUMN IF EXISTS flavour;               -- RULE-19
ALTER TABLE products DROP COLUMN IF EXISTS nicotine_strength_mg;  -- RULE-19
ALTER TABLE products
    ADD COLUMN IF NOT EXISTS min_variant_price     NUMERIC(10,2),
    ADD COLUMN IF NOT EXISTS has_sellable_variant  BOOLEAN     NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS updated_at            TIMESTAMPTZ NOT NULL DEFAULT now();
```

`public.refresh_product_read_model(p_product_id UUID)` sets `min_variant_price` to
`min(COALESCE(sale_price, original_price))` over the product's variants and `has_sellable_variant`
to `EXISTS (… is_available AND stock_quantity > 0)` (RULE-16 / AC-29). It is called from an
`AFTER INSERT OR UPDATE OR DELETE` statement trigger on `product_variants`.

`get_catalogue_filters()` is redefined per Decision 3: the `flavours` and `nicotine_strengths` keys
are removed and replaced by

```sql
'option_types', COALESCE((
    SELECT jsonb_agg(jsonb_build_object('name', t.name, 'values', t.values) ORDER BY t.name)
    FROM (
        SELECT ot.name, jsonb_agg(DISTINCT ov.value) AS values
        FROM public.product_option_types ot
        JOIN public.product_option_values ov ON ov.option_type_id = ot.id
        JOIN public.products p ON p.id = ot.product_id
        WHERE p.is_published
        GROUP BY ot.name
    ) t
), '[]'::jsonb)
```

### RLS policies (the only real security boundary — per `rules/architecture-admin.md`)

```sql
ALTER TABLE product_images                ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_option_types          ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_option_values         ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_variants              ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_variant_option_values ENABLE ROW LEVEL SECURITY;
ALTER TABLE product_sku_registry          ENABLE ROW LEVEL SECURITY;

-- Customer surface reads the children of PUBLISHED products only, mirroring products_public_read.
CREATE POLICY "product_images_public_read" ON product_images
    FOR SELECT USING (
        EXISTS (SELECT 1 FROM products p WHERE p.id = product_id AND p.is_published)
        OR (SELECT public.authorize('products.view'))
    );
-- Same predicate shape for product_option_types / product_option_values / product_variants /
-- product_variant_option_values, resolving the owning product through their own FK chain.
-- product_sku_registry is admin-only: SELECT requires products.view, with no public clause.

-- Writes: permission-gated, per action, on every child table.
CREATE POLICY "product_images_admin_insert" ON product_images
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create'))
                        OR (SELECT public.authorize('products.edit')));
CREATE POLICY "product_images_admin_update" ON product_images
    FOR UPDATE USING ((SELECT public.authorize('products.edit')))
    WITH CHECK ((SELECT public.authorize('products.edit')));
CREATE POLICY "product_images_admin_delete" ON product_images
    FOR DELETE USING ((SELECT public.authorize('products.edit'))
                   OR (SELECT public.authorize('products.create')));
-- Repeated verbatim for the five remaining child tables.

-- products itself: gains an admin read (RULE-17 — staff see Unpublished) and its first writes.
CREATE POLICY "products_admin_read" ON products
    FOR SELECT USING ((SELECT public.authorize('products.view')));
CREATE POLICY "products_admin_insert" ON products
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create')));
CREATE POLICY "products_admin_update" ON products
    FOR UPDATE USING ((SELECT public.authorize('products.edit')))
    WITH CHECK ((SELECT public.authorize('products.edit')));
-- No DELETE policy: spec Section 1 puts product deletion out of scope.

-- Decision 10 — replace migration 0004's obsolete role-claim predicates.
DROP POLICY IF EXISTS "product_images_admin_read"   ON storage.objects;
DROP POLICY IF EXISTS "product_images_admin_insert" ON storage.objects;
DROP POLICY IF EXISTS "product_images_admin_update" ON storage.objects;
DROP POLICY IF EXISTS "product_images_admin_delete" ON storage.objects;

CREATE POLICY "product_images_read" ON storage.objects
    FOR SELECT USING (bucket_id = 'product-images' AND (SELECT public.authorize('products.view')));
CREATE POLICY "product_images_insert" ON storage.objects
    FOR INSERT WITH CHECK (bucket_id = 'product-images' AND (SELECT public.authorize('products.create')));
CREATE POLICY "product_images_update" ON storage.objects
    FOR UPDATE USING (bucket_id = 'product-images' AND (SELECT public.authorize('products.edit')))
    WITH CHECK (bucket_id = 'product-images' AND (SELECT public.authorize('products.edit')));
CREATE POLICY "product_images_delete" ON storage.objects
    FOR DELETE USING (bucket_id = 'product-images'
                      AND ((SELECT public.authorize('products.edit'))
                        OR (SELECT public.authorize('products.create'))));
```

`save_product(payload JSONB)` is `SECURITY DEFINER` and therefore re-asserts the gate itself:
`IF NOT (public.authorize('products.create') OR public.authorize('products.edit')) THEN RAISE
EXCEPTION … USING ERRCODE = 'insufficient_privilege'; END IF;`, with `REVOKE ALL … FROM PUBLIC, anon`
and `GRANT EXECUTE … TO authenticated`, following `delete_categories` in migration 0020. Within the
transaction it sequences **all registry and variant deletes before any insert** so a SKU moving
between rows never self-collides on `product_sku_registry`'s primary key.

## 11. Open Questions, Risks & Assumptions

- **⚠️ Risk — ✅ Accepted:** dropping `products.flavour` / `nicotine_strength_mg` removes the
  customer catalogue's Flavour and Nicotine filter groups, and no replacement control ships in this
  feature — customers filter on category, brand, and price only until the product-catalogue feature
  builds dynamic option-type filters. This feature supplies the data via `get_catalogue_filters()`'s
  new `option_types` payload (Decision 3, TASK-018, TASK-022), per the spec's Section 4 cross-feature
  note.

- **⚠️ Risk — ✅ Accepted:** unique product names (RULE-2, Decision 7) mean the catalogue can never
  carry two products with the same name — a real constraint on merchandising, e.g. the same device
  listed separately per retail bundle would need distinguishing names. Accepted deliberately: it is
  what makes the name-derived SKU suggestion collision-free without a disambiguation mechanism.
  Slugification remains lossy, so a rare suggested-SKU collision between two differently-punctuated
  names is still possible; it lands on the ordinary "SKU already in use" refusal (TASK-021) and the
  staff member edits the SKU.

---
**Status:** Resolved · **Spec:** `.specs/create-product/spec.md` · **Created:** 2026-08-13 · **Resolved:** 2026-08-13

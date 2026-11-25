# Implementation Plan — Manage Product

> Companion to `.specs/manage-product/spec.md`. Plan is technical (HOW); spec is
> non-technical (WHAT/WHY). Read spec first.

## 1. Objective

Grow the existing `/admin/products` list from a plain paged table into the full manage-product
surface: name search, status/brand/category/price filters, six sort orders, variant price range
plus variant count per row, page-scoped multi-selection, single and bulk Activate/Deactivate, and
single and bulk permanent Delete with reference protection. Reads move from a PostgREST table
query to one `admin_products_page` RPC, because "at least one variant price inside the bounds"
(RULE-10) and "lowest variant price across the full variant set" (RULE-11) are per-variant
predicates PostgREST cannot express on the `products` row. Deletion mirrors the confirmed
Brands/Categories policy through a new `delete_products` RPC with partial success. Page follows the
manage-brands pattern end to end: `QueryStatePageBase` deep linking, `ShopFilterPanel`,
`ShopSortSelect`, `ShopBulkActionBar`, `ShopConfirmDialog`, `ShopPagination`.

## 2. Tech Stack

- **Domain:** C# — two use-case enums only, no new invariants.
- **Application:** MediatR, FluentValidation, `Result<T>`, `PagedResult<T>`, hand-written DTO mappers.
- **Infrastructure:** `supabase-csharp` (PostgREST + RPC), `IFileStorage` for image disposal.
- **Web:** MudBlazor (`MudTable` MultiSelection, `MudChip`, `MudTextField`, `MudSkeleton`), existing
  `Shop*` components, bUnit for component tests.
- **Persistence:** Supabase (PostgreSQL + RLS), project `TheShop-Dev` (`uwfltzgecsepvlaplktv`).

## 3. High-level Architecture

Every listing criterion travels as one immutable criteria record from URL to SQL. Mutations reuse
the manage-brands shape: one command covers the single-row and bulk case, a one-element list being
the single-row case.

```
Staff types in search / picks filter / picks sort / clicks page in ManageProducts.razor
   ↓
ProductQueryState pushed to URL → QueryStatePageBase.ApplyStateAsync
   ↓
IMediator.Send(GetAdminProductsPageQuery)          [RequiresPermission products.view]
   ↓
GetAdminProductsPageHandler → AdminProductCriteria (normalized, clamped)
   ↓
IProductRepository.GetAdminPageAsync(criteria, ct)
   ↓
SupabaseProductRepository → RPC admin_products_page(...)   (SECURITY DEFINER, authorize'd)
   ↓
PagedResult<ProductListItemDto> → Paginator → MudTable rows

Bulk Delete:  DeleteProductsCommand → RPC delete_products(uuid[]) → ProductDeletionOutcomeDto
              → handler disposes deleted products' exclusive image objects via IFileStorage
Activate:     SetProductStatusCommand → load variants → Product.EnsurePublishable()
              → IProductRepository.SetPublishedAsync(eligible ids, true)
```

## 4. Data Model

### Domain entities & value objects

No entity or invariant changes. Existing `Product.SetPublished(bool)` and
`Product.EnsurePublishable()` (throws `ProductNotPublishableException`) carry the activation rule.
Two use-case enums are added:

- **`ProductStatusFilter`** (`Domain/Enums/`) — `Active = 1`, `Inactive = 2`. Absence means no
  status narrowing, matching `BrandStatusFilter`.
- **`AdminProductSortOption`** (`Domain/Enums/`) — `NameAToZ = 0`, `NameZToA = 1`, `NewestFirst = 2`,
  `OldestFirst = 3`, `LowestVariantPriceAsc = 4`, `LowestVariantPriceDesc = 5`. Separate from
  `ProductSortOption` (storefront) — admin sorts on lowest variant price, storefront on
  `display_price`.

### DTOs (Application → Web)

- **`ProductListItemDto`** (reworked; admin list is its only consumer) — `Id`, `Name`, `Sku`,
  `PrimaryImageUrl`, `BrandName`, `CategoryName`, `MinPrice`, `MaxPrice`, `VariantCount`,
  `Currency`, `IsPublished`. `MinPrice`/`MaxPrice` are the FR-17 variant range (equal values render
  as one amount), or the product's own effective price when `VariantCount` is 0 (Decision 13); both
  `null` when nothing is priced. Retired fields: `EffectivePrice`, `MinVariantPrice`, `HasVariants`.
- **`AdminProductCriteria`** — `Search`, `Status`, `BrandIds`, `CategoryIds`, `PriceMin`, `PriceMax`,
  `Sort`, `Pagination`.
- **`AdminProductFiltersDto`** — `Brands`, `Categories` (`FilterOptionDto` lists), `PriceRange`
  (`RangeFilterDto`).
- **`ProductStatusChangeDto`** — `ChangedCount`, `NotPublishable` (`IReadOnlyList<BlockedProductDto>`).
- **`ProductDeletionOutcomeDto`** — `DeletedCount`, `Blocked` (`IReadOnlyList<ReferencedProductDto>`).
- **`BlockedProductDto`** — `Id`, `Name`.
- **`ReferencedProductDto`** — `Id`, `Name`, `ReferenceCount`.

### Database tables (new or modified)

| Table | Purpose | Key columns |
|---|---|---|
| `products` (unchanged schema) | listing source | `id`, `name`, `sku`, `brand_id`, `category_id`, `currency`, `is_published`, `created_at`, `updated_at`, `original_price`, `sale_price` |
| `product_variants` (unchanged schema) | FR-17 price range, count | `product_id`, `original_price`, `sale_price` |
| `product_images` (unchanged schema) | row thumbnail, image disposal | `product_id`, `object_key`, `is_primary` |

No table is created or altered. Three RPCs and one RLS policy are added (Section 10).

### Indexes

- `idx_product_variants_product (product_id, position)` — exists; serves the variant rollup and the
  price `EXISTS` predicate.
- `idx_products_created_at (created_at DESC)` — new; serves Newest/Oldest sorts.
- `ux_products_name_normalized (lower(btrim(name)))` — exists; serves name sorts, not `ILIKE %term%`
  (see Section 11).

## 5. Core Design Decisions

1. **Decision:** Read the admin list through a `SECURITY DEFINER` RPC `admin_products_page(...)`
   instead of a PostgREST table query.
   - **Why:** RULE-10 needs `EXISTS (variant price BETWEEN bounds)` and RULE-11 needs
     `min(variant price)` over the product's *full* variant set while a price filter is applied.
     Neither is expressible over `products` columns; `display_price` (migration 0027) collapses to a
     single number and would wrongly admit a 10/30 product under a 15–25 filter (AC-26). One RPC also
     returns range, count, brand/category names, primary image key, and `count(*) OVER ()` in one
     round-trip.
   - **Rejected:** keeping PostgREST + client-side post-filtering — breaks pagination totals and
     RULE-5 (filters apply before pagination). Rejected `SECURITY INVOKER`: the function aggregates
     `products` and `product_variants` together, and DEFINER plus an explicit
     `authorize('products.view')` guard matches the `brand_product_counts` / `delete_brands`
     precedent (migration 0015).

2. **Decision:** One `SetProductStatusCommand(ids, isActive)` and one
   `DeleteProductsCommand(ids)` cover both the single-row and bulk case.
   - **Why:** Same surface as `SetBrandStatusCommand` / `DeleteBrandsCommand`; a one-element list is
     the single-row case, so FR-9/FR-11 and FR-13 share one authorization gate and one outcome shape.
   - **Rejected:** separate single/bulk commands — duplicate permission attributes and duplicate
     failure translation for no behavioral gain.

3. **Decision:** Activation loads each product's variants and calls `Product.EnsurePublishable()`;
   products that fail are skipped, reported in `ProductStatusChangeDto.NotPublishable`, and left
   Inactive. Deactivation skips the check.
   - **Why:** FR-10/RULE-7 restore catalogue eligibility "under existing catalogue rules", and
     Create/Update already refuse to publish an unpriced product (`ProductNotPublishableException`).
     Without this the listing is a backdoor to a priceless catalogue tile. Rule 6 keeps the check on
     the entity, not in SQL.
   - **Rejected:** unconditional bulk `UPDATE` — cheaper, but publishes drafts. Rejected duplicating
     the publishability predicate in SQL — restates a domain invariant outside Domain.

4. **Decision:** Deactivation and eligible activation persist through one PostgREST `UPDATE` filtered
   by `id IN (...) AND is_published <> target`, setting `is_published` and `updated_at = now()`.
   - **Why:** Returned row count *is* the FR-9 changed count (a product already in the target status
     is a no-op), in one round-trip instead of manage-brands' N. Bumping `updated_at` makes a stale
     open edit form fail with `Product_ModifiedElsewhere` instead of silently reverting the status.
   - **Rejected:** the manage-brands per-id read-modify-write loop — N round-trips for a rule that
     needs none.

5. **Decision:** `delete_products(product_ids uuid[])` is one `SECURITY DEFINER` RPC that counts
   references and deletes in one statement, returning per-product `deleted` / `reference_count` /
   `image_keys`.
   - **Why:** RULE-3/RULE-4 partial success with no TOCTOU window — same shape as `delete_brands`
     (migration 0015). Returning image keys lets the handler dispose exclusively owned storage
     objects (RULE-8) after the rows are gone.
   - **Rejected:** client-side "check then delete" — a reference can appear between the two calls.

6. **Decision:** Reference counting lives in one SQL expression, `product_reference_count(p_id)`,
   inlined in `delete_products`. Today it evaluates to `0`: no table references `products` except
   the product's own children (`product_images`, `product_option_types`, `product_variants`,
   `product_sku_registry`), which are `ON DELETE CASCADE` owned parts, not references. Order tables
   do not exist yet (verified against `TheShop-Dev`).
   - **Why:** FR-12 must be structurally present now and correct the day `order_items` ships — one
     expression to extend. Owned children must never block their own parent's deletion.
   - **Rejected:** deferring FR-12 entirely — the UI, DTOs, and messages would have to be rebuilt.
     See the Section 11 risk about proving AC-15/16/17.

7. **Decision:** Filter options come from a new `get_admin_product_filters()` RPC returning the
   brands and categories that actually own a product (regardless of their Active flag) plus the
   min/max variant price bounds.
   - **Why:** A product may belong to an Inactive brand; sourcing options from
     `GetActiveBrandsQuery` would make that product unfilterable. Price bounds must span variant
     prices, which `get_catalogue_filters()` (published-only, `display_price`) does not.
   - **Rejected:** reusing `GetActiveBrandsQuery` / `GetActiveCategoriesQuery` — wrong option set and
     two extra round-trips. Rejected extending `get_catalogue_filters()` — it is the public
     storefront's contract, published-products-only by design.

8. **Decision:** Page state (search, status, brands, categories, price bounds, sort, page) is
   deep-linked via a `ProductQueryState : IUrlQueryState<ProductQueryState>` and
   `QueryStatePageBase<ProductQueryState>`; selection is page-owned and cleared in `ApplyStateAsync`.
   - **Why:** Satisfies FR-6 mechanically — every criteria change is a URL push that resets `Page = 1`
     and re-enters the one apply path, which clears selection. Matches `BrandQueryState` and
     `CatalogueQueryState`; multi-value filters reuse `CatalogueQueryState.ToggleFilter`'s merge shape
     so rapid toggles compound instead of racing.
   - **Rejected:** page-local fields — Back/Forward and shared links break, and FR-6's resets become
     hand-maintained.

9. **Decision:** Sort orders are declared once in `AdminProductSortCatalogue : SortCatalogue<AdminProductSortOption>`
   reusing `SortSlugs` (`name-asc`, `name-desc`, `newest`, `oldest`, `price-asc`, `price-desc`), default
   `NameAToZ` (FR-5).
   - **Why:** Picker, URL slug, and fallback follow from one declaration. `SortSlugs` is an
     append-only URL contract, so the admin list spells the same concepts the same way.
   - **Rejected:** new admin-only slugs — same concept, two tokens.

10. **Decision:** The RPC passes the sort as its slug string and validates it against a fixed
    `CASE`; an unknown slug falls back to `name-asc`.
    - **Why:** One vocabulary from URL to SQL, no enum-int coupling across the boundary.
    - **Rejected:** ordinal ints — a reordered enum silently changes stored links' meaning.

11. **Decision:** Row layout keeps the existing SKU column and adds the variant count as a
    `Typo.caption` line beneath the product name (FR-1: no separate count column).
    - **Why:** Spec enumerates required row content and forbids a count *column*; it does not forbid
      the SKU column the row already has, and SKU is how staff disambiguate similar names.
    - **Rejected:** dropping SKU — loses shipped capability with no requirement behind it. Rejected
      moving SKU into the name caption — the caption carries the variant count.

12. **Decision:** After any deletion the page reloads, then, when the current page is beyond the last
    page, navigates to `min(Page, TotalPages)` (or the empty state when no results remain).
    - **Why:** AC-22 last-page recovery. `Paginator.TotalPages` already carries the post-delete truth.
    - **Rejected:** always returning to page 1 — discards the staff member's position.

13. **Decision:** A product with no variants is priced by its own effective price,
    `COALESCE(sale_price, original_price)`. That single amount is both ends of its displayed range,
    is what the price filter matches, and is what both price sorts order on; its variant-count
    caption is omitted.
    - **Why:** FR-17's wording assumes every product has variants, but `Product.HasVariants` is false
      for a product configured without option types. Treating such a product as unpriced drops it out
      of every price filter and sorts it as NULL — the exact failure migration 0027 was written to fix
      for the storefront.
    - **Rejected:** excluding zero-variant products from price filtering and sorting — hides real
      products behind a filter they satisfy. Rejected display-only pricing (shown but never matched)
      — the range cell would then contradict the filter that excluded the row.

## 6. Core Functional Flow

### Flow 1: Find products (spec Behavior 1)

1. `ManageProducts.razor` — search `MudTextField` (`DebounceInterval="300"`), `ShopFilterPanel`
   (status single-select, brand/category multi-select, price range), `ShopSortSelect`,
   `ShopPagination`.
2. Every change builds a `ProductQueryState` and calls `PushStateAsync(state with { Page = 1 })`
   (page change keeps `Page`).
3. `ApplyStateAsync` clears `_selectedItems` and `_referencedIds`, snapshots criteria, then
   `BusyState.RunAsync(BusyKeys.Products.ManageList, () => _products.GoToAsync(state.Page, ct))`.
4. Fetch delegate sends `GetAdminProductsPageQuery(Search, Status, BrandIds, CategoryIds, PriceMin,
   PriceMax, Sort, Pagination)`.
5. `GetAdminProductsPageQueryValidator` runs first: page ≥ 1, `PriceMin <= PriceMax`, non-negative
   bounds. On failure → `Result.Fail(nameof(ProductErrorKeys.PriceRangeInvalid))` (or `PageInvalid`).
6. Handler normalizes pagination to `PageSize = 10`, trims/blank-collapses `Search` to `null`, and
   calls `IProductRepository.GetAdminPageAsync(criteria, ct)`.
7. Repository calls `admin_products_page`; each row maps to `ProductListItemDto` with the primary
   image key resolved through `IFileStorage.GetPublicUrl(StorageArea.ProductImages, key)`.
8. Page renders rows; price cell shows one amount when `MinPrice == MaxPrice`, else
   `Strings.ManageProducts_PriceRange`; caption shows `ManageProducts_VariantCountOne` or
   `ManageProducts_VariantCountMany`.

### Flow 2: Add or edit (spec Behavior 2)

1. Add button (`AuthorizeView` on `products.create`) links to `Routes.Admin.AddProduct`; row edit
   icon (`products.edit`) links to `Routes.Admin.EditProduct(id)`. Both already exist.
2. No new Application work — existing Add/Edit flows own their behavior. Returning to the list
   re-enters `ApplyStateAsync`, so saved changes appear in the next matching results (FR-8).

### Flow 3: Change availability (spec Behavior 3)

1. Row status `MudChip` or bulk bar button calls `OnStatusToggledAsync` / `BulkActivateAsync` /
   `BulkDeactivateAsync`.
2. Deactivation opens `ShopConfirmDialog` naming the product or the selected count and stating
   customers will no longer see it. Cancel returns without a request (FR-9, RULE-7).
3. `Mediator.Send(new SetProductStatusCommand(ids, isActive))`;
   `[RequiresPermission("products.edit")]` gates it.
4. Activating: handler calls `IProductRepository.GetManyWithVariantsAsync(ids, ct)`, then per product
   `SetPublished(true)` + `EnsurePublishable()`. `ProductNotPublishableException` → product excluded
   and added to `NotPublishable`. Deactivating: no check.
5. `IProductRepository.SetPublishedAsync(eligibleIds, isActive, ct)` returns the changed count.
6. Failure → `Result.Fail(nameof(ProductErrorKeys.StatusChangeFailed))`; RPC/PostgREST "access denied"
   → `RbacErrorKeys.AccessDenied`.
7. Page shows `ManageProducts_ActivatedSuccess` / `ManageProducts_DeactivatedSuccess` with the changed
   count, plus `ManageProducts_ActivateSkipped` (Warning) when `NotPublishable` is non-empty, then
   reloads the page (FR-16, AC-23).

### Flow 4: Delete products (spec Behavior 4)

1. Row trash icon (`products.delete`) or bulk Delete opens `ShopConfirmDialog` naming the product, or
   the selected count, and stating permanence (RULE-2). Cancel or dismiss changes nothing and keeps
   the selection (AC-14).
2. `Mediator.Send(new DeleteProductsCommand(ids))`; `[RequiresPermission("products.delete")]`.
3. Handler calls `IProductRepository.DeleteManyAsync(ids, ct)` → `delete_products` RPC → outcome plus
   the deleted products' exclusive image keys.
4. Handler best-effort deletes each key via `IFileStorage.DeleteAsync(StorageArea.ProductImages, ...)`;
   a storage failure never fails the request (rows are already gone) — mirrors `DeleteBrandsHandler`.
5. Failure → `Result.Fail(nameof(ProductErrorKeys.DeleteFailed))`; "access denied" → `RbacErrorKeys.AccessDenied`.
6. Page reports: all deleted → `ManageProducts_BulkDeletedSuccess`; none deleted →
   `Product_BulkDeleteAllBlocked`; mixed → `Product_BulkDeletePartial` with both counts. Referenced
   products stay selected, are marked with a `Product_InUse` caption carrying the reference count, and
   the message offers deactivation (FR-12, FR-13, AC-15..17).
7. Reload, then apply the Decision-12 last-page recovery (AC-22).

## 7. Development Plan

### Step 1 — Domain (`shop-domain-implementer`)

**Depends on:** resolved plan.

- [ ] **TASK-001** — Add `ProductStatusFilter` enum (`Active = 1`, `Inactive = 2`) in
  `TheShop.Domain/Entities/../Enums/ProductStatusFilter.cs`, documented as "absence = no narrowing"
  like `BrandStatusFilter`.
- [ ] **TASK-002** — Add `AdminProductSortOption` enum (`NameAToZ`, `NameZToA`, `NewestFirst`,
  `OldestFirst`, `LowestVariantPriceAsc`, `LowestVariantPriceDesc`) in `TheShop.Domain/Enums/`.

No entity, value object, or exception changes: `Product.SetPublished` and
`Product.EnsurePublishable` already carry every rule this feature needs.

**Completion gate:** Domain builds · no outer-layer type in Domain · both enum names and members
reported literally to Application.

### Step 2 — Application (`shop-application-implementer`)

**Depends on:** Step 1's reported Domain API.

- [ ] **TASK-003** — Rework `Features/Products/DTOs/ProductListItemDto.cs` to
  `(Guid Id, string Name, string Sku, string? PrimaryImageUrl, string BrandName, string CategoryName,
  decimal? MinPrice, decimal? MaxPrice, int VariantCount, string Currency, bool IsPublished)`; add
  `AdminProductCriteria`, `AdminProductFiltersDto`, `ProductStatusChangeDto`, `BlockedProductDto`,
  `ProductDeletionOutcomeDto`, `ReferencedProductDto` under `Features/Products/DTOs/`. Update
  `AdminProductDtoMapper` to the new shape.
- [ ] **TASK-004** — Rework `Queries/GetAdminProductsPage/`: query record gains `Search`, `Status`,
  `BrandIds`, `CategoryIds`, `PriceMin`, `PriceMax`, `Sort`; validator adds the Section 9 rules;
  handler normalizes to `PageSize = 10`, blank-collapses `Search`, builds `AdminProductCriteria`.
  Keep `[RequiresPermission("products.view")]`.
- [ ] **TASK-005** — Add `Queries/GetAdminProductFilters/` (query + handler), `[RequiresPermission("products.view")]`,
  returning `Result<AdminProductFiltersDto>` from `IProductRepository.GetAdminFiltersAsync`.
- [ ] **TASK-006** — Add `Commands/SetProductStatus/` (command + handler + validator),
  `[RequiresPermission("products.edit")]`, implementing Flow 3 steps 4–6.
- [ ] **TASK-007** — Add `Commands/DeleteProducts/` (command + handler + validator),
  `[RequiresPermission("products.delete")]`, implementing Flow 4 steps 3–5 including best-effort
  image disposal via `IFileStorage`.
- [ ] **TASK-008** — Extend `Common/Interfaces/IProductRepository.cs`:
  `Task<PagedResult<ProductListItemDto>> GetAdminPageAsync(AdminProductCriteria criteria, CancellationToken ct)`
  (replaces the `PaginationRequest` overload),
  `Task<AdminProductFiltersDto> GetAdminFiltersAsync(CancellationToken ct)`,
  `Task<IReadOnlyList<Product>> GetManyWithVariantsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)`,
  `Task<int> SetPublishedAsync(IReadOnlyList<Guid> ids, bool isPublished, CancellationToken ct)`,
  `Task<(ProductDeletionOutcomeDto Outcome, IReadOnlyList<string> DeletedImageKeys)> DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct)`.
- [ ] **TASK-009** — Add the Section 9 keys to `ProductErrorKeys` and mirror every new key in
  `Strings.resx` + `Strings.fr.resx` (`[TODO]` French placeholder acceptable this pass).
- [ ] **TASK-010** — Application unit tests (`GetAdminProductsPageHandlerTests`,
  `SetProductStatusHandlerTests`, `DeleteProductsHandlerTests`, validator tests), owned by
  `shop-test-writer` before Implement completion. `DeleteProductsHandlerTests` must cover a stubbed
  reference-blocked outcome (partial, all-blocked, none-blocked) so the FR-12/RULE-3 path is proven
  at handler level while no referencing table exists. Rewrite the existing `AdminProductDtoMapper`
  tests against the reshaped `ProductListItemDto`.

**Completion gate:** feature-folder convention held · every Section 9 outcome translated by a
handler or validator · new contracts compile · no Web or Infrastructure type referenced · the literal
`ProductListItemDto` shape reported to Web and to `shop-test-writer`, so the tests it breaks are
rewritten once against the final shape.

### Step 3 — Contract freeze

Freeze before Infrastructure and Web start. A frozen contract changes only through the deviation
procedure below.

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `ProductListItemDto` shape | Application | Web, Infrastructure | Stable |
| `AdminProductCriteria` | Application | Infrastructure | Stable |
| `AdminProductFiltersDto` | Application | Web, Infrastructure | Stable |
| `GetAdminProductsPageQuery` / `GetAdminProductFiltersQuery` | Application | Web | Stable |
| `SetProductStatusCommand` / `ProductStatusChangeDto` | Application | Web | Stable |
| `DeleteProductsCommand` / `ProductDeletionOutcomeDto` | Application | Web | Stable |
| `IProductRepository` (5 members from TASK-008) | Application | Infrastructure | Stable |
| `ProductStatusFilter`, `AdminProductSortOption` | Domain | Application, Infrastructure, Web | Stable |

### Step 4 — Infrastructure (`shop-infra-implementer`) — runs in parallel with Step 5

**Depends on:** contract freeze (`IProductRepository` stable).

- [ ] **TASK-011** — Apply migration `supabase/migrations/0028_manage_products.sql` (Section 10
  verbatim) through the Supabase MCP against `TheShop-Dev`: `admin_products_page`,
  `get_admin_product_filters`, `delete_products`, the `products_admin_delete` RLS policy, the
  `idx_products_created_at` index, and the GRANT/REVOKE block.
- [ ] **TASK-012** — Add `Persistence/Records/AdminProductRowRecord.cs`,
  `AdminProductFiltersRecord.cs`, `DeleteProductsResultRecord.cs`; add the record→DTO mapping to
  `Persistence/Mappers/ProductMapper.cs` (or a new `AdminProductMapper.cs`), resolving image URLs
  through `IFileStorage.GetPublicUrl(StorageArea.ProductImages, key)`.
- [ ] **TASK-013** — Reimplement `SupabaseProductRepository.GetAdminPageAsync` over the
  `admin_products_page` RPC and add `GetAdminFiltersAsync` over `get_admin_product_filters`.
  `total_count` is read from the first returned row; a no-match response returns no rows and
  therefore no total, so map an empty response explicitly to
  `PagedResult<ProductListItemDto>.Empty(pagination)` and cover that branch with a test.
- [ ] **TASK-014** — Add `GetManyWithVariantsAsync` (products + their variants, rehydrated through
  the existing `ProductVariantRecord.ToDomain` path), `SetPublishedAsync` (single PostgREST update,
  `id IN (...)` AND `is_published <> target`, setting `is_published` and `updated_at`, returning the
  updated row count), and `DeleteManyAsync` over `delete_products`.
- [ ] **TASK-015** — Failure translation: "access denied" ⇒ `RbacErrorKeys.AccessDenied`;
  any other exception ⇒ `Product_DeleteFailed` / `Product_StatusChangeFailed` per Section 9. Preserve
  the existing `OperationCanceledException` rethrow pattern.

**Completion gate:** migration applied cleanly and re-runnable · RLS policy matches Section 10
verbatim · repository satisfies the frozen interface · no Infrastructure type leaks inward ·
`get_advisors` reports no new security finding.

### Step 5 — Web (`shop-ui-implementer`) — runs in parallel with Step 4

**Depends on:** contract freeze (query/command/DTO shapes stable).

**Figma references** *(re-fetched by `shop-ui-implementer` at implementation time)*

- **File:** `https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop`
- **Nodes:**
  - `2629:4502` — "Manage Products" page frame, selection state: announcement bar, appbar,
    breadcrumb, title + Add button, listing section, bulk selection bar, footer.
  - `2629:4507` — page header row: title block plus the Add-new-product button.
  - `2629:4511` — listing section: `2629:4512` Product Section (table `2671:13936` + pagination
    `2629:4640`) beside `2629:4641` Filters.
  - `2629:4641` — filter panel: `2629:4642` Status Filter, `2668:13818` Price Filter, `2668:13835`
    Brand Filter, `2668:13851` Category Filter.
  - `2671:13936` — product table: row layout carrying image, name with the variant-count caption,
    SKU, brand, category, price range, status chip, row actions.
  - `2629:4653` — bulk selection bar: selected count, Activate / Deactivate / Delete, close.
  - `2629:4487` — "Manage Products - Empty": the no-products state. Title row `2629:4493` (heading +
    Add button) stays; `2629:4496` Product Section holds only `2629:4497`. No filter panel, table,
    pagination, or bulk bar renders in this state.
  - `2629:4497` — empty-state copy: "No Products Found" (`2629:4498`) over "Add your first products
    to organize products and make your catalogue easier to manage." (`2629:4499`).

- [ ] **TASK-016** — Add `Common/Sorting/AdminProductSortCatalogue.cs` (default `NameAToZ`, six
  options over existing `SortSlugs`) and `Pages/Admin/ProductQueryState.cs`
  (`Search`, `Status`, `Filters`, `PriceMin`, `PriceMax`, `Sort`, `Page`) with `ToggleFilter`, following
  `CatalogueQueryState` + `BrandQueryState`.
- [ ] **TASK-017** — Rewrite `Pages/Admin/ManageProducts.razor.cs` onto
  `QueryStatePageBase<ProductQueryState>`: filter/search/sort/page handlers, selection state,
  `_referencedIds`, row-pending indicators, confirmation dialogs, status and delete flows, outcome
  messages, and Decision-12 last-page recovery. Keep `[Route(Routes.Admin.ManageProducts)]` and
  `[AuthorizePermission("products.view")]`.
- [ ] **TASK-018** — Rewrite `Pages/Admin/ManageProducts.razor`: `ShopFilterPanel` (status
  single-select + brand/category multi-select + price range, `RangeFormatter` via
  `CurrencyFormatter`), search field, `ShopSortSelect`, `MudTable` with `MultiSelection`, price-range
  cell, variant-count caption, status chip gated on `products.edit`, row Edit/Delete gated on their
  permissions, `ShopBulkActionBar`, `ShopPagination`, and the loading / empty / no-match states.
  The empty state (node `2629:4487`) renders the title row and the empty copy only — no filter panel,
  table, pagination, or bulk bar. Every control keyboard-operable with an accessible name (AC-20).
- [ ] **TASK-019** — Add `BusyKeys.Products.ProductStatus` and `BusyKeys.Products.DeleteProducts`;
  add the UI resource keys to `Strings.resx` + `Strings.fr.resx`:
  `ManageProducts_SearchPlaceholder`, `ManageProducts_PriceRange`, `ManageProducts_VariantCountOne`,
  `ManageProducts_VariantCountMany`, `ManageProducts_NoMatchTitle`, `ManageProducts_NoMatchDescription`,
  `ManageProducts_DeleteAria`, `ManageProducts_BulkSetActive`, `ManageProducts_BulkSetInactive`,
  `ManageProducts_BulkDelete`, `ManageProducts_DeactivateConfirmTitle`,
  `ManageProducts_DeactivateConfirmBody`, `ManageProducts_BulkDeactivateConfirmTitle`,
  `ManageProducts_BulkDeactivateConfirmBody`, `ManageProducts_DeleteConfirmTitle`,
  `ManageProducts_DeleteConfirmBody`, `ManageProducts_BulkDeleteConfirmTitle`,
  `ManageProducts_BulkDeleteConfirmBody`, `ManageProducts_ActivatedSuccess`,
  `ManageProducts_DeactivatedSuccess`, `ManageProducts_ActivateSkipped`,
  `ManageProducts_DeletedSuccess`, `ManageProducts_BulkDeletedSuccess`,
  `Sort_LowestVariantPriceAsc`, `Sort_LowestVariantPriceDesc`. Reuse existing `Filter_*` and
  `Sort_NameAZ` / `Sort_NameZA` / `Sort_Newest` / `Sort_Oldest`. Reset the existing
  `ManageProducts_EmptyTitle` / `ManageProducts_EmptyDescription` values to the node `2629:4497` copy.
- [ ] **TASK-020** — bUnit component tests (`ManageProductsTests`), owned by `shop-test-writer`
  before Implement completion. Rewrite the existing `ManageProductsTests` against the literal
  `ProductListItemDto` shape reported by the Step 2 gate. Cover the reference-blocked delete paths
  (AC-15/16/17) with a stubbed blocked outcome: warning message, kept-selected rows, in-use captions,
  reference counts, deactivation alternative.

**Completion gate:** matches the Figma nodes above · MudBlazor only, `MudText` for all text, no
hardcoded string or design token (constitution rules 2–5, 11, 16) · consumes only frozen contracts ·
controls the user lacks permission for are absent, not disabled · `check-design-rules.ps1` passes on
every changed file.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5 complete.

- [ ] **TASK-021** — Cross-layer verification: solution builds, DI resolves, migration applied,
  formatter run (`.sdd/scripts/format-changes.ps1`), design rules re-checked, and the four flows in
  Section 6 exercised against `TheShop-Dev`.
- [ ] **TASK-022** — Test specialists write and run the required unit/component tests before Implement
  completion; record actual counts, member coverage, and remaining AC proof. Follow with
  `/theshop-test manage-product`, `/theshop-e2e manage-product`, and `/theshop-review manage-product`.
  Document stays a separate manual invocation.

### Deviation procedure

- **Accept** a deviation that preserves approved behavior and layer boundaries, matches existing
  project conventions better, and neither weakens authorization/integrity nor expands scope.
- **Reject** anything that changes a requirement, adds business behavior, violates dependency
  direction, silently alters a frozen contract, or carries unrelated refactoring.
- **Contract change:** stop dependent work → record it here (update the freeze table and affected
  TASK ids) → resume only after the contract is re-frozen.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: first 10 of 11, both statuses, name ascending | TASK-004, TASK-011, TASK-013, TASK-016, TASK-018 |
| AC-2: page change retains search/filters/sort | TASK-016, TASK-017, TASK-018 |
| AC-3: trimmed, case-insensitive name search | TASK-004, TASK-011, TASK-017 |
| AC-4: search + status + 2 brands + 2 categories + price bounds combine | TASK-004, TASK-011, TASK-013, TASK-016, TASK-018 |
| AC-5: every FR-5 sort order, restrictions retained | TASK-002, TASK-011, TASK-016, TASK-018 |
| AC-6: criteria change resets to page 1 and clears selection | TASK-016, TASK-017 |
| AC-7: select-all covers current page only | TASK-017, TASK-018 |
| AC-8: Add flow reachable, result appears in later results | TASK-017, TASK-018 |
| AC-9: Edit flow opens the chosen product, saved changes appear | TASK-017, TASK-018 |
| AC-10: activate one or many without confirmation, changed count | TASK-006, TASK-014, TASK-017 |
| AC-11: deactivate confirmation names product/count; cancel preserves | TASK-006, TASK-017, TASK-018 |
| AC-12: deactivated product leaves catalogue, details/history intact | TASK-006, TASK-014, TASK-021 |
| AC-13: single delete confirmation + exclusive image cleanup | TASK-007, TASK-011, TASK-014, TASK-017 |
| AC-14: cancel/dismiss deletes nothing, selection intact | TASK-017, TASK-018 |
| AC-15: referenced product survives, count + deactivation offered | TASK-007, TASK-011, TASK-017 |
| AC-16: 5 selected, 2 referenced → 3 deleted, 2 stay selected and named | TASK-007, TASK-011, TASK-017, TASK-018 |
| AC-17: all selected referenced → none deleted, all reported | TASK-007, TASK-011, TASK-017 |
| AC-18: missing permission hides control and refuses direct action | TASK-004, TASK-006, TASK-007, TASK-011, TASK-018 |
| AC-19: English/French text, locale CAD formatting | TASK-009, TASK-018, TASK-019 |
| AC-20: keyboard operation, focus indicator, accessible names, dialog focus | TASK-018 |
| AC-21: loading / empty / no-match states, clearing restores results | TASK-017, TASK-018 |
| AC-22: last-page recovery after deletion | TASK-017 |
| AC-23: missing product, failure, partial bulk failure reported truthfully | TASK-006, TASK-007, TASK-015, TASK-017 |
| AC-24: variants 10 and 30 with sale to 25 → CAD 10–25 | TASK-011, TASK-013, TASK-018 |
| AC-25: equal prices collapse to one amount; count with singular/plural | TASK-011, TASK-018, TASK-019 |
| AC-26: filter 15–25 excludes 10/30; 10–10 and 30–30 include | TASK-011, TASK-013 |
| AC-27: filter 15–25 keeps full 10–30 range and count 3 | TASK-011, TASK-013, TASK-018 |
| AC-28: price sorts use lowest variant price under a filter | TASK-002, TASK-011, TASK-016 |
| AC-29: inactive/out-of-stock variants count, deleted variant excluded | TASK-011, TASK-013 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

- `GetAdminProductsPageQueryValidator` (extended):
  - `Pagination.Page >= 1` → `ProductErrorKeys.PageInvalid`
  - `PriceMin >= 0`, `PriceMax >= 0`, `PriceMin <= PriceMax` when both supplied (RULE-10) →
    `ProductErrorKeys.PriceRangeInvalid`
  - `Search` needs no rule — blank or whitespace collapses to `null` in the handler (FR-3)
- `SetProductStatusCommandValidator`: `ProductIds` not empty, no empty GUID, no duplicates →
  `ProductErrorKeys.ProductIdsRequired`
- `DeleteProductsCommandValidator`: same three rules → `ProductErrorKeys.ProductIdsRequired`

### Domain exceptions

- `ProductNotPublishableException` (existing) — thrown by `Product.EnsurePublishable()` during
  activation. Caught per product in `SetProductStatusHandler`: the product is skipped, not failed,
  and reported through `ProductStatusChangeDto.NotPublishable` (Decision 3).

### Result.Fail error keys (new entries in `Strings.resx`)

| Key | English text |
|---|---|
| `Product_PriceRangeInvalid` | "Enter a valid price range." |
| `Product_ProductIdsRequired` | "Select at least one product." |
| `Product_StatusChangeFailed` | "The status could not be updated. Try again." |
| `Product_DeleteFailed` | "The product could not be deleted. Try again." |
| `Product_InUse` | "{0} is used by {1} other record(s) and was not deleted. Deactivate it instead." |
| `Product_BulkDeleteAllBlocked` | "No products were deleted — every selected product is still in use. Deactivate them instead." |
| `Product_BulkDeletePartial` | "{0} product(s) deleted, {1} still in use and kept." |
| `Product_ActivateNotPublishable` | "{0} product(s) were skipped because they are missing a price." |

`Product_NotFound` (existing) covers the missing-product edge case. `RbacErrorKeys.AccessDenied`
(existing) covers refused direct attempts (FR-14, AC-18). Every new key is mirrored in
`Strings.fr.resx`; the review step's French-completeness gate catches stragglers.

## 10. Database Schema & RLS Policies

### Schema

No table changes. One index and three functions:

```sql
-- ============================================================================
-- 0028_manage_products
--
-- Manage-product admin listing (.specs/manage-product/plan.md §10): a paged,
-- searched, filtered, sorted read over products + their variant price rollup,
-- an admin filter-option source, and atomic partial-success deletion.
-- ============================================================================

CREATE INDEX IF NOT EXISTS idx_products_created_at ON products (created_at DESC);

-- One page of the manage-product list. SECURITY DEFINER: the aggregation spans
-- products and product_variants, and unpublished products must be visible to
-- products.view holders regardless of per-table policy evaluation order.
-- RULE-9/10/11: the rollup covers EVERY variant row (inactive and unavailable
-- included; a deleted variant has no row), the price filter matches an ACTUAL
-- variant price, and both price sorts use the unfiltered lowest price.
-- A product with no variants falls back to its own effective price as a single
-- implicit price (plan §5 Decision 13).
CREATE OR REPLACE FUNCTION public.admin_products_page(
    p_search       TEXT,
    p_status       TEXT,          -- 'active' | 'inactive' | NULL for no narrowing
    p_brand_ids    UUID[],
    p_category_ids UUID[],
    p_price_min    NUMERIC,
    p_price_max    NUMERIC,
    p_sort         TEXT,          -- name-asc | name-desc | newest | oldest | price-asc | price-desc
    p_limit        INT,
    p_offset       INT)
RETURNS TABLE (
    id                UUID,
    name              TEXT,
    sku               TEXT,
    brand_id          UUID,
    brand_name        TEXT,
    category_id       UUID,
    category_name     TEXT,
    currency          TEXT,
    is_published      BOOLEAN,
    primary_image_key TEXT,
    variant_count     INT,
    min_price         NUMERIC,
    max_price         NUMERIC,
    total_count       BIGINT)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_search TEXT := NULLIF(btrim(COALESCE(p_search, '')), '');
BEGIN
    IF NOT public.authorize('products.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    WITH variant_price AS (
        SELECT v.product_id, COALESCE(v.sale_price, v.original_price) AS price
        FROM product_variants v
    ),
    rollup AS (
        SELECT p.id AS product_id,
               (SELECT count(*)::int FROM product_variants v WHERE v.product_id = p.id) AS variant_count,
               COALESCE((SELECT min(vp.price) FROM variant_price vp WHERE vp.product_id = p.id),
                        COALESCE(p.sale_price, p.original_price)) AS min_price,
               COALESCE((SELECT max(vp.price) FROM variant_price vp WHERE vp.product_id = p.id),
                        COALESCE(p.sale_price, p.original_price)) AS max_price
        FROM products p
    ),
    matched AS (
        SELECT p.id, p.name, p.sku, p.brand_id, b.name AS brand_name,
               p.category_id, c.name AS category_name, p.currency, p.is_published,
               (SELECT i.object_key FROM product_images i
                 WHERE i.product_id = p.id AND i.is_primary LIMIT 1) AS primary_image_key,
               r.variant_count, r.min_price, r.max_price, p.created_at,
               count(*) OVER () AS total_count
        FROM products p
        JOIN brands b     ON b.id = p.brand_id
        JOIN categories c ON c.id = p.category_id
        JOIN rollup r     ON r.product_id = p.id
        WHERE (v_search IS NULL OR p.name ILIKE '%' || v_search || '%')
          AND (p_status IS NULL OR p.is_published = (p_status = 'active'))
          AND (p_brand_ids IS NULL OR p.brand_id = ANY(p_brand_ids))
          AND (p_category_ids IS NULL OR p.category_id = ANY(p_category_ids))
          AND (
                (p_price_min IS NULL AND p_price_max IS NULL)
                OR EXISTS (
                    SELECT 1 FROM variant_price vp
                    WHERE vp.product_id = p.id
                      AND vp.price IS NOT NULL
                      AND (p_price_min IS NULL OR vp.price >= p_price_min)
                      AND (p_price_max IS NULL OR vp.price <= p_price_max))
                OR (r.variant_count = 0
                    AND COALESCE(p.sale_price, p.original_price) IS NOT NULL
                    AND (p_price_min IS NULL OR COALESCE(p.sale_price, p.original_price) >= p_price_min)
                    AND (p_price_max IS NULL OR COALESCE(p.sale_price, p.original_price) <= p_price_max))
              )
    )
    SELECT m.id, m.name, m.sku, m.brand_id, m.brand_name, m.category_id, m.category_name,
           m.currency, m.is_published, m.primary_image_key,
           m.variant_count, m.min_price, m.max_price, m.total_count
    FROM matched m
    ORDER BY
        CASE WHEN p_sort = 'name-desc'  THEN m.name END DESC,
        CASE WHEN p_sort = 'newest'     THEN m.created_at END DESC,
        CASE WHEN p_sort = 'oldest'     THEN m.created_at END ASC,
        CASE WHEN p_sort = 'price-desc' THEN m.min_price END DESC,
        CASE WHEN p_sort = 'price-asc'  THEN m.min_price END ASC,
        CASE WHEN p_sort NOT IN ('name-desc','newest','oldest','price-asc','price-desc')
             THEN m.name END ASC,
        m.id                                  -- deterministic tie-break across pages
    LIMIT p_limit OFFSET p_offset;
END;
$$;

-- Filter options for the admin list: brands and categories that actually own a
-- product (Active or not — an Inactive brand's products must stay filterable),
-- plus the variant-aware price bounds the range control needs.
CREATE OR REPLACE FUNCTION public.get_admin_product_filters()
RETURNS JSONB
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    v_result JSONB;
BEGIN
    IF NOT public.authorize('products.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    SELECT jsonb_build_object(
        'brands', COALESCE((
            SELECT jsonb_agg(jsonb_build_object('id', b.id, 'name', b.name) ORDER BY b.name)
            FROM brands b WHERE EXISTS (SELECT 1 FROM products p WHERE p.brand_id = b.id)), '[]'::jsonb),
        'categories', COALESCE((
            SELECT jsonb_agg(jsonb_build_object('id', c.id, 'name', c.name) ORDER BY c.name)
            FROM categories c WHERE EXISTS (SELECT 1 FROM products p WHERE p.category_id = c.id)), '[]'::jsonb),
        'price_min', COALESCE((SELECT min(price) FROM (
            SELECT COALESCE(v.sale_price, v.original_price) AS price FROM product_variants v
            UNION ALL
            SELECT COALESCE(p.sale_price, p.original_price) FROM products p
             WHERE NOT EXISTS (SELECT 1 FROM product_variants v2 WHERE v2.product_id = p.id)) prices), 0),
        'price_max', COALESCE((SELECT max(price) FROM (
            SELECT COALESCE(v.sale_price, v.original_price) AS price FROM product_variants v
            UNION ALL
            SELECT COALESCE(p.sale_price, p.original_price) FROM products p
             WHERE NOT EXISTS (SELECT 1 FROM product_variants v2 WHERE v2.product_id = p.id)) prices), 0)
    ) INTO v_result;

    RETURN v_result;
END;
$$;

-- Atomic partial-success deletion (RULE-3/RULE-4). Counting and deleting in one
-- statement closes the TOCTOU window a client-side guard would leave open.
-- reference_count counts records that REFERENCE a product from outside it. The
-- product's own children (product_images, product_option_types, product_variants,
-- product_sku_registry) are owned parts with ON DELETE CASCADE and never block
-- their parent. No such referencing table exists yet; when order_items ships, add
-- its count to the single expression below and nothing else changes.
-- image_keys carries only object keys no surviving product_images row still uses,
-- so shared content is never removed (RULE-8).
CREATE OR REPLACE FUNCTION public.delete_products(product_ids UUID[])
RETURNS TABLE (id UUID, name TEXT, reference_count BIGINT, deleted BOOLEAN, image_keys TEXT[])
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('products.delete') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    WITH candidates AS (
        SELECT p.id, p.name,
               0::bigint AS reference_count,          -- no external referencing table exists yet
               COALESCE((SELECT array_agg(i.object_key) FROM product_images i
                          WHERE i.product_id = p.id), ARRAY[]::text[]) AS image_keys
        FROM products p
        WHERE p.id = ANY(product_ids)
    ),
    removed AS (
        DELETE FROM products
        WHERE products.id IN (SELECT c.id FROM candidates c WHERE c.reference_count = 0)
        RETURNING products.id
    )
    SELECT c.id, c.name, c.reference_count,
           EXISTS (SELECT 1 FROM removed r WHERE r.id = c.id),
           COALESCE((
               SELECT array_agg(k) FROM unnest(c.image_keys) AS k
               WHERE NOT EXISTS (SELECT 1 FROM product_images i WHERE i.object_key = k)
           ), ARRAY[]::text[])
    FROM candidates c;
END;
$$;

REVOKE ALL ON FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.get_admin_product_filters() FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.delete_products(UUID[])     FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.admin_products_page(TEXT, TEXT, UUID[], UUID[], NUMERIC, NUMERIC, TEXT, INT, INT) TO authenticated;
GRANT EXECUTE ON FUNCTION public.get_admin_product_filters() TO authenticated;
GRANT EXECUTE ON FUNCTION public.delete_products(UUID[])     TO authenticated;
```

### RLS policies

`products` already carries `products_public_read`, `products_admin_read` (`products.view`),
`products_admin_insert` (`products.create`), and `products_admin_update` (`products.edit`) from
migrations 0002/0023. Deletion was out of scope there, so the DELETE policy is added here — the RPC
runs as DEFINER, but a direct PostgREST `DELETE` must be gated too (admin enforcement Layer 4).

```sql
ALTER TABLE products ENABLE ROW LEVEL SECURITY;   -- already enabled; stated for completeness

CREATE POLICY "products_admin_delete" ON products
    FOR DELETE USING ((SELECT public.authorize('products.delete')));
```

## 11. Open Questions, Risks & Assumptions

None — all questions resolved.

- **⚠️ Risk — ✅ Accepted:** FR-12, RULE-3, and AC-15/AC-16/AC-17 (reference-blocked deletion) cannot
  be proven against real data. No table references `products` today — orders, carts, and order items
  do not exist in `TheShop-Dev` (verified) — so `delete_products` reports `reference_count = 0` for
  every product and blocks nothing. Mitigated to handler and component level: TASK-010 and TASK-020
  prove the blocked path against a stubbed outcome, and the count lives in one SQL expression
  extended when `order_items` ships (TASK-011). Residual, accepted: the SQL branch itself stays a
  `0`-literal, so those three ACs need an explicit E2E gate waiver naming this risk.

- **⚠️ Risk — ✅ Accepted:** name search is `ILIKE '%term%'`, which no btree index can serve, so it is
  a sequential scan over `products`. Accepted at current catalogue size; revisit with a `pg_trgm` GIN
  index when the product count reaches the low thousands.

---
**Status:** Resolved · **Spec:** `.specs/manage-product/spec.md` · **Created:** 2026-09-08 · **Resolved:** 2026-09-08

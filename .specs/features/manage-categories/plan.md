# Implementation Plan — Manage Categories

> Companion to `.specs/manage-categories/spec.md`. This plan is technical (HOW); the spec is
> non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

Promote `categories` from a two-column reference table into a fully managed admin aggregate and back
it with the `/admin/categories` page the console already links to. `Category` gains
`Description`/`ImagePath`/`IsActive`/`CreatedAt` and the mutation methods `Brand` already has; the
table gains its first write RLS policies, a normalized-name unique index, a staff-only
`category-images` bucket, and two `SECURITY DEFINER` RPCs that make the RULE-6 delete guard count
products the client cannot see. `Category.Slug` is retired across all four layers (spec FR-11), and
`get_catalogue_filters()` starts filtering its category facet on `is_active` so deactivation reaches
customers (spec FR-13) **without** hiding any product.

One fix to a sibling feature rides along: `brands_read`'s `is_active` predicate is reverted to
`USING (true)` (Decision 13), because it makes deactivating a brand break catalogue rendering for
that brand's published products — the exact hazard Decision 2 avoids for categories.

Nearly every shared seam this feature needs was already extracted by `manage-brands` —
`ShopFilterPanel`, `ShopSortSelect`, `ShopConfirmDialog`, `ShopBulkActionBar`, `ShopImageUpload`,
`ShopPagination`, `QueryStatePageBase<T>`, `Paginator<T>`, `SortCatalogue<T>`, `IFileStorage` +
`StorageArea`. This plan consumes them as-is and adds no new shared component.

## 2. Tech Stack

- **Domain:** C# (no external deps) — `Category` rebuilt as a managed aggregate, three exceptions,
  two enums.
- **Application:** MediatR, FluentValidation, `Result<T>`, `PagedResult<T>` / `PaginationRequest`
  (already generic in `Common/Models/`), `[RequiresPermission]` + `AuthorizationBehavior`.
- **Infrastructure:** `supabase-csharp` (Postgrest queries + two new RPCs), `IFileStorage` /
  `StorageArea.CategoryImages` for image disposal.
- **Web:** MudBlazor (`MudTable`, `MudCheckBox`, `MudSwitch`, `MudDialog`), the shared components
  listed in §1, `QueryStatePageBase<CategoryQueryState>`; bUnit for tests.
- **Persistence:** Supabase (PostgreSQL + RLS). `categories` currently has **one** policy
  (`categories_public_read`) and no write policies at all.

## 3. High-level Architecture

```
ManageCategories.razor  (search, single-select status filter, 4-way sort, selection, bulk bar)
   ↓
IMediator.Send(GetCategoriesPageQuery | SetCategoryStatusCommand | DeleteCategoriesCommand)
   ↓
AuthorizationBehavior  ← [RequiresPermission("categories.view|create|edit|delete")]
   ↓
{…}Handler (Application)
   ├── ICategoryRepository.GetPageAsync / GetProductCountsAsync / DeleteManyAsync
   ├── category.Rename(...) / .Activate() / .Deactivate() / .RemoveImage()   // invariants here
   └── IFileStorage.DeleteAsync(StorageArea.CategoryImages, …)               // RULE-11
   ↓
SupabaseCategoryRepository (Infrastructure)
   ├── From<CategoryRecord>() … Range(from,to)        → categories (RLS: categories_public_read)
   ├── rpc category_product_counts(uuid[])            → SECURITY DEFINER, bypasses products RLS
   └── rpc delete_categories(uuid[])                  → atomic partial-success delete
   ↓
Result<PagedResult<CategoryListItemDto>> | Result<CategoryDeletionOutcomeDto>
   ↓
ManageCategories.razor → PushStateAsync(CategoryQueryState) → URL → ApplyStateAsync → re-render
```

## 4. Data Model

### Domain entities & value objects

- **`Category`** (rewritten, `Domain/Entities/Category.cs`) — today it is an immutable
  `Id`/`Name`/`Slug` triple with "no invariants to enforce". It becomes the same shape as `Brand`:
  - Properties: `Id`, `Name` (private set), `Description?`, `ImagePath?`, `IsActive`.
    **`Slug` is removed** (spec FR-11).
  - `MaxNameLength = 100`, `MaxDescriptionLength = 250` (RULE-3 — brand-matched per the spec's
    image/limit constraint).
  - `Create(string name, string? description, bool isActive)` — generates the id, validates.
    **Breaking:** the existing `Create(Guid id, string name, string slug)` is gone; its one
    production call site is `ProductMapper.cs:33` and it moves to `Rehydrate`.
  - `Rehydrate(Guid id, string name, string? description = null, string? imagePath = null,
    bool isActive = true)` — no re-validation; persisted rows are already valid.
  - `Rename(string)`, `ChangeDescription(string?)`, `Activate()`, `Deactivate()`,
    `AttachImage(string)`, `RemoveImage()` (returns the previous key so the handler can dispose the
    object without the entity knowing storage exists — RULE-11).
  - `CreatedAt` is **not** a domain property. It is a persistence-ordering concern only (it feeds
    the two new sorts and nothing else), so it stays on `CategoryRecord` — exactly how `Brand`
    handles `BrandSortOption.NewestFirst` today.
- **`CategorySortOption`** (new, `Domain/Enums/`) — `NameAToZ = 0`, `NameZToA = 1`,
  `NewestFirst = 2`, `OldestFirst = 3` (spec FR-5 — four orders, name A→Z default).
- **`CategoryStatusFilter`** (new, `Domain/Enums/`) — `Active = 1`, `Inactive = 2`. Carried as
  `CategoryStatusFilter?`; **"every category" is the absence of a filter (`null`), never a member**
  — which is precisely spec RULE-16's "no separate All/None option to choose".
- **Exceptions** (new, `Domain/Exceptions/`) — `CategoryNameRequiredException`,
  `CategoryNameTooLongException`, `CategoryDescriptionTooLongException`.

### DTOs (Application → Web)

- **`CategoryDto`** — `Id, Name, Description, ImageUrl, IsActive`. Feeds the edit form.
- **`CategoryListItemDto`** — `Id, Name, Description, ImageUrl, IsActive, ProductCount`.
  `ProductCount` is the authoritative RPC count driving the FR-15 message and the in-use indicator.
- **`CategoryImageUpload`** — `FileName, ContentType, long SizeInBytes, Stream Content` (mirrors
  `BrandLogoUpload`).
- **`CategoryDeletionOutcomeDto`** — `int DeletedCount, IReadOnlyList<BlockedCategoryDto> Blocked`.
- **`BlockedCategoryDto`** — `Guid Id, string Name, int ProductCount`.
- **`CategoryStatusChangeDto`** — `int ChangedCount`, counting only categories that actually changed
  state.

### Database tables (new or modified)

| Table | Purpose | Key columns |
|---|---|---|
| `categories` (modified) | Add `description`, `image_path`, `is_active`, `created_at`; **drop `slug`** (and its `categories_slug_key` unique index) | `id, name, description, image_path, is_active, created_at` |
| `products` (unmodified) | `category_id UUID NOT NULL REFERENCES categories(id)` — no cascade, so the FK is the last-resort guard behind RULE-6 | `category_id` |
| `storage.buckets` (row added) | `category-images`, staff-read only | — |

### Indexes

- `ux_categories_name_normalized ON categories (lower(btrim(name)))` (new, UNIQUE) — enforces
  RULE-2 at the database and becomes the **only** uniqueness constraint on a category name.
- `idx_categories_name_lower ON categories (lower(name))` (new) — serves `ORDER BY lower(name)`.
- `idx_categories_created_at ON categories (created_at DESC)` (new) — serves the Newest/Oldest sorts.
  The five seeded categories all take the migration's `DEFAULT now()`, so those two sorts cannot
  distinguish them until new categories are added; the repository's `id` tiebreak (TASK-017) keeps
  paging deterministic regardless, and AC-32 is written against "categories added at different
  times". Backfilling a synthetic spread was rejected as fabricating history.
- `categories_slug_key` — **dropped** with the column.
- `idx_products_category` (existing) — serves the delete guard's per-category count.

## 5. Core Design Decisions

1. **Decision:** Mirror the shipped `manage-brands` architecture member-for-member rather than
   generalizing the two features into shared abstractions — one aggregate, one repository interface,
   one command per capability, one page, per feature.
   - **Why:** the reusable seams were *already* extracted when brands was built (§1 lists eleven of
     them), so what remains duplicated is duplication the project's own rules mandate:
     `Features/Categories/` is a vertical slice (Rule 9); `[RequiresPermission("categories.delete")]`
     needs a compile-time-constant code, so a generic `SetStatusCommand<T>` cannot carry its own
     gate; `SortCatalogue<T>`'s own doc explicitly rules that sort enums stay per-feature "so a brand
     list cannot be asked to sort by price"; and each RPC must name its own `authorize()` permission.
     The one genuinely generic seam this feature needs — file storage — extends by a single
     `StorageArea` enum member, which is the abstraction working as designed.
   - **Rejected:** a shared `ReferenceDataEntity` base for `Brand`/`Category` (cross-aggregate
     inheritance to share three validation lines, and the two entities are already expected to
     diverge); a table-name-parameterized delete RPC (loses the per-permission `authorize()` guard
     and takes a dynamic identifier).
   - **Also rejected — lifting `ManageBrands.razor.cs`'s selection/bulk/confirm/row-mutation
     plumbing into a shared `AdminListPageBase`.** `ManageCategories.razor.cs` (TASK-022) knowingly
     reproduces roughly 200 lines of it, so Rule 25's second-call-site bar *is* met. Deferred anyway:
     the extraction would refactor a shipped admin page under a regression gate inside the largest
     slice in flight, and a two-instance generalization is the one most likely to be wrong — Orders
     and Products will each want a different table shape. One duplicated file buys a third data
     point. Revisit when that third list lands.

2. **Decision:** An Inactive category is hidden from the customer-facing **filter facet only** —
   `get_catalogue_filters()` gains `WHERE c.is_active` — while `categories_public_read`
   (`USING (true)`) is left exactly as it is.
   - **Why:** this is what the spec actually asks for. FR-13 removes an Inactive category "from the
     customer-facing category filter and from the list of categories products can be assigned to",
     and both RULE-13 and the Section 4 constraint insist that "no product is deleted, hidden, or
     moved" as a side effect. Restricting the `categories` SELECT policy would do the opposite:
     `ProductRecord` embeds `CategoryRecord` via Postgrest resource embedding, so an RLS-hidden
     category comes back as `null` and `ProductMapper.ToDomain` throws
     `InvalidOperationException("…missing its embedded category join.")` — deactivating one category
     would break the whole catalogue page for every product in it.
   - **Rejected:** copying brands' `USING (is_active OR authorize('brands.view'))` predicate onto
     `categories`. It is the consistent-looking choice and it is wrong here for the reason above —
     and it is wrong on `brands` too, which Decision 13 now fixes rather than mirrors.

3. **Decision:** The RULE-6 delete guard and the list's per-category product counts run in two
   `SECURITY DEFINER` PL/pgSQL RPCs — `delete_categories(uuid[])` and
   `category_product_counts(uuid[])` — each re-checking `authorize('categories.delete'|'.view')` as
   its first statement.
   - **Why:** identical to brands' Decisions 2/3 and forced by the same policy. `products`' only
     SELECT policy is `products_public_read USING (is_published = true)`, so **no** client can count
     unpublished products — yet RULE-6 counts a product "whether or not it is currently available to
     customers", and AC-19 names the discontinued-products case explicitly. A client-side guard would
     report 0, attempt the delete, and surface a raw `23503` instead of the count the spec requires.
     Counting and deleting in one statement also closes the TOCTOU window.
   - **Rejected:** a Postgrest embedded `products(count)` (RLS-filtered to published only); widening
     `products`' SELECT policy (a categories feature must not enlarge the products security surface,
     and a `categories.delete` holder needn't hold `products.view`); catching `23503` (count-free
     message, no partial success).

4. **Decision:** Single and bulk actions share one set-based command each —
   `SetCategoryStatusCommand(IReadOnlyList<Guid>, bool)` and
   `DeleteCategoriesCommand(IReadOnlyList<Guid>)`. The inline row toggle (FR-19) and the single-row
   delete (FR-14) are one-element calls.
   - **Why:** RULE-15's partial-success semantics then apply uniformly, and FR-15 (a single in-use
     category refused with its product count) is literally the one-element case of FR-25 — one
     handler, one code path, no drift between the single and bulk refusal messages.
   - **Rejected:** separate single/bulk handlers that must keep identical guard semantics.

5. **Decision:** The status filter renders through the existing `ShopFilterPanel` as a
   `FilterKind.SingleSelect` group with exactly two options and **no "All" option**.
   - **Why:** this is RULE-16 rendered directly. Single-select options draw as checkboxes, so
     unchecking the selected one already means "no narrowing" and emits `null`; the panel's existing
     `OnSingleSelectChanged((GroupKey, Value?))` contract gives the swap-and-clear behaviour FR-4
     describes for free. An explicit "All" would be a second encoding of the same state and would
     force a no-op branch on every consumer down to the repository.
   - **Rejected:** a multi-select group (permits the meaningless Active+Inactive state RULE-16
     forbids); a bare `MudSelect` outside the panel.

6. **Decision:** Retire `Category.Slug` end to end — the Domain property, the `Create` overload that
   took it, `CategoryRecord.Slug`, and the `categories.slug` column with its `categories_slug_key`
   unique index.
   - **Why:** spec FR-11 and AC-24. Verified across `src/`: the only production reader is
     `ProductMapper.cs:33` passing it straight into `Category.Create`; nothing keys a route, `Href`,
     or `NavigateTo` on it; `ProductFilterDefinitions` keys the category facet on `c.Id.ToString()`;
     and `get_catalogue_filters()` never projects it. Its only live effect is a `UNIQUE` index that
     can reject a rename RULE-2 permits — the collision AC-24 now requires to succeed.
     Migration `0002` seeds products via `WHERE slug = …`, but `0002` always runs before this
     migration on a replay, so that seed stays valid.
   - **Rejected:** dropping only the unique index (leaves an unusable column and defers the
     decision); keeping it (AC-24 then cannot pass).

7. **Decision:** `ExistsByNormalizedNameAsync(name, Guid? excludeCategoryId, ct)` takes the
   exclusion parameter from the outset.
   - **Why:** AC-11 requires a no-op save to succeed, which a self-inclusive uniqueness check would
     reject. With the slug gone, `ux_categories_name_normalized` plus this pre-check and its `23505`
     translation is the whole of RULE-2.
   - **Rejected:** filtering the self-match in the handler (pushes a persistence concern up a layer,
     same round trip).

8. **Decision:** Routes are `/admin/categories/new` and `/admin/categories/{id:guid}/edit`, id-keyed,
   with `Routes.Admin.ManageCategories` (already reserved) as the post-save return target.
   - **Why:** spec FR-20 and constraint "the edit address identifies the category by its own
     permanent identifier … so a saved link keeps working after the category is renamed" — and with
     the slug retired, the id is the only identifier a category has.
   - **Rejected:** dialog-based add/edit (the spec requires bookmarkable per-form addresses).

9. **Decision:** `GetCategoryByIdQuery` requires `categories.view`; the **page** carries the
   `categories.edit` gate via `[AuthorizePermission("categories.edit")]` on `EditCategory`.
   - **Why:** the brands precedent (its Decision 11), and reading a category is a read — gating it on
     `edit` would make any future read-only detail view need a duplicate query. The boundary does not
     weaken: a view-only admin deep-linking to the edit URL is denied by the route policy before the
     page renders (AC-20/AC-23), and `UpdateCategoryCommand` independently requires `categories.edit`.
   - **Rejected:** `categories.edit` on the query; relying on the query alone for AC-20 (the page
     would render its chrome before failing, which is not the standard denied experience).

10. **Decision:** Selection lives in the page's `HashSet<CategoryListItemDto>` and is **not** part of
    `CategoryQueryState` (the URL); `ApplyStateAsync` clears it.
    - **Why:** RULE-14 requires the selection to clear on any page/search/filter/sort change, and
      `QueryStatePageBase` funnels all four through one `ApplyStateAsync` — clearing there satisfies
      RULE-14 by construction rather than by remembering it in four handlers.
    - **Rejected:** URL-persisted selection (violates RULE-14 on Back/Forward, bloats links).

11. **Decision:** New categories are created **Active by default** in the Domain, not only in the
    form: `CreateCategoryCommand` carries `bool IsActive` and the add page seeds it `true`, while
    `Category.Create` treats `isActive` as the caller's explicit choice.
    - **Why:** spec FR-7 / RULE-5 make Active the default a staff member can override *before*
      saving, which is a form-default, not an invariant. Keeping the entity explicit means the
      Rehydrate path and any future importer cannot silently flip a category live.
    - **Rejected:** defaulting the parameter in `Category.Create` (hides the decision from the one
      place — the form — where the spec puts it).

12. **Decision:** The bulk-delete outcome message reports **counts only**; blocked categories are
    identified in the table, where they stay selected and carry an in-use indicator showing their
    product count. The single-category refusal (`Category_InUse`, FR-15) names its one category.
    - **Why:** matches the shipped brands behaviour and satisfies AC-29's "the two in-use categories
      … are named as still in use" in the place the user is looking, with a message that stays
      readable regardless of selection size.
    - **Rejected:** enumerating names in the snackbar (unbounded length).

13. **Decision:** Fix the same hazard on `brands` in this feature — revert `brands_read` to
    `USING (true)` (keeping the policy name), leaving `get_catalogue_filters()`' already-present
    `WHERE b.is_active` brand facet as the sole mechanism hiding an Inactive brand from customers.
    This makes `brands` and `categories` consistent again, on the correct side.
    - **Why:** `brands_read` is currently
      `USING (is_active OR (SELECT authorize('brands.view')))` (migration `0013`), and
      `ProductMapper.ToDomain` throws `InvalidOperationException` when the embedded `BrandRecord`
      comes back null. Deactivating a brand therefore breaks catalogue rendering for its published
      products — the identical failure Decision 2 exists to avoid. The customer-facing intent
      (migration `0013`'s stated goal: "Inactive brands must be invisible to customers") is already
      fully served by the facet filter that migration `0012` added, so the RLS predicate is
      redundant *and* harmful. Fixing it here rather than filing it keeps the two sibling admin
      features from documenting opposite rules for the same problem.
    - **Consequence — three shipped tests change their assertions, intentionally.**
      `SupabaseBrandRepositorySchemaTests.BrandsRead_WhenBrandIsInactiveAndCallerIsAnonymous_IsHidden`
      and `…_WhenBrandIsInactiveAndCallerLacksBrandsViewPermission_IsHidden` assert precisely the
      predicate being reverted; they are replaced by coverage asserting the new contract (the row is
      readable, the **facet** excludes it, and a published product with an Inactive brand still
      maps). The schema fixtures at `ManageBrandsSchemaTests.cs:504` and
      `SupabaseBrandRepositorySchemaTests.cs:432` hardcode the old DDL and are updated to match.
      Per the deviation procedure these edits would otherwise be a failed regression gate — they are
      pre-authorized here because reverting the predicate *is* the decision.
    - **Rejected:** logging it as an accepted risk and filing it separately (leaves a known
      catalogue-breaking defect live while this plan explicitly reasons about it); narrowing
      `ProductMapper` to tolerate a null embed instead (hides a data-integrity signal behind a
      silent fallback, and `brand_id` is `NOT NULL` so null always means RLS, never bad data).

## 6. Core Functional Flow

### Flow 1: Browse / search / filter / sort / page (spec Behaviors 1–3)

1. `ManageCategories.razor.cs : QueryStatePageBase<CategoryQueryState>` —
   `ApplyStateAsync(state, ct)` runs on first render and every URL change.
2. It clears `_selectedItems` and `_blockedIds` (RULE-14), snapshots search/status/sort, then
   `await BusyState.RunAsync(BusyKeys.Categories.ManageList, () => _categories.GoToAsync(state.Page, ct))`.
3. `AuthorizationBehavior` checks `categories.view`; `GetCategoriesPageQueryValidator` clamps
   page/page-size to the fixed 10 (spec constraint).
4. `GetCategoriesPageHandler` → `ICategoryRepository.GetPageAsync(...)` →
   `GetProductCountsAsync(pageIds)` → `Result.Ok(PagedResult<CategoryListItemDto>)`.
5. Search/filter/sort handlers call `PushStateAsync(state with { …, Page = 1 })` — RULE-10's page
   reset lives in the state record, so it cannot be forgotten per control.
6. Empty result → `ShopFilterPanel`'s clear affordance plus the no-match empty state; zero categories
   at all → the empty state with the Add affordance for `categories.create` holders (AC-22).

### Flow 2: Add a category (spec Behavior 4)

1. `AddCategory.razor` — `[AuthorizePermission("categories.create")]`; the status control is seeded
   Active (Decision 11, RULE-5).
2. Submit → `CreateCategoryCommand(Name, Description, IsActive, Image)`.
3. `CreateCategoryCommandValidator` → RULE-1/RULE-3/RULE-4 keys (Section 9).
4. `CreateCategoryHandler`: `ExistsByNormalizedNameAsync(name, excludeCategoryId: null)` →
   `Fail(AlreadyExists)`; `Category.Create(...)` inside
   `try { } catch (DomainException ex) { return Result.Fail(ex.MessageKey); }`; on an image, upload
   to `StorageArea.CategoryImages` **after** the name check passes, then `AttachImage`; `AddAsync`.
   If `AddAsync` fails, the just-uploaded object is deleted best-effort so no image is stored for a
   category that was not saved (spec edge case).
5. Snackbar `Strings.Category_Created` → navigate to `Routes.Admin.ManageCategories`.

### Flow 3: Edit a category (spec Behavior 5)

1. `EditCategory.razor.cs` — `[Route(Routes.Admin.EditCategoryPattern)]`, `[Parameter] Guid Id`,
   `[AuthorizePermission("categories.edit")]` (Decision 9). Sends `GetCategoryByIdQuery(Id)`; a
   not-found result renders the "no longer exists" message and returns to the list (AC-23).
2. Submit → `UpdateCategoryCommand(Id, Name, Description, IsActive, NewImage, RemoveImage)`.
3. `UpdateCategoryHandler`: load → `ExistsByNormalizedNameAsync(name, excludeCategoryId: Id)`
   (Decision 7, AC-11) → `Rename`/`ChangeDescription`/`Activate`|`Deactivate` in the same
   `try/catch (DomainException)`. **No slug is recomputed — nothing derived from the name exists**
   (FR-11, AC-24).
4. Image: capture `previousPath = category.ImagePath`; on remove → `category.RemoveImage()`; on a new
   upload → `UploadAsync` then `AttachImage`. After a successful `UpdateAsync`, best-effort
   `IFileStorage.DeleteAsync(previousPath)` (RULE-11, AC-13) — an orphaned object never fails the
   request.
5. `Result.Ok(CategoryDtoMapper.ToDto(...))` → snackbar `Strings.EditCategory_Success` → back to the
   list.

### Flow 4: Flip status inline or in bulk (spec Behaviors 9, 11)

1. Activate → no prompt; `SetCategoryStatusCommand([id], true)` immediately (RULE-12).
2. Deactivate → `ShopConfirmDialog` naming the category (or the count, bulk) → on confirm,
   `SetCategoryStatusCommand(ids, false)`.
3. `SetCategoryStatusHandler` loads each category, calls `Activate()`/`Deactivate()`, persists, and
   returns `CategoryStatusChangeDto(ChangedCount)`. A category already in the target status is a
   no-op and is **not** counted, so the reported number matches what the list visibly changes.
4. Page reloads the current state (selection cleared); snackbar reports the count. On failure the
   switch reverts and `Localizer[result.Error]` is shown (spec edge case).

### Flow 5: Delete one or many (spec Behaviors 6, 7, 8, 12)

1. `ShopConfirmDialog` states permanence and names the category, or states the count for a bulk
   delete (RULE-7). Cancelling changes nothing and keeps the selection (AC-18).
2. `DeleteCategoriesCommand(ids)` → `[RequiresPermission("categories.delete")]`.
3. `DeleteCategoriesHandler` → `ICategoryRepository.DeleteManyAsync(ids, ct)` → the
   `delete_categories` RPC → `CategoryDeletionOutcomeDto(DeletedCount, Blocked[])`. Image objects for
   deleted categories are disposed best-effort using the paths the RPC returns (RULE-11, AC-17).
4. UI (Decision 12): all deleted → success snackbar. Some blocked → `Category_BulkDeletePartial`
   with both counts, blocked ids stay selected and their rows show the in-use indicator (AC-29).
   All blocked → `Category_BulkDeleteAllBlocked` (AC-30). A single blocked delete uses
   `Category_InUse`, which names its one category and its product count (FR-15, AC-19).
5. Reload lands on the last valid page when the current one emptied (spec edge case).

## 7. Development Plan

### Step 1 — Domain (`shop-domain-implementer`)

**Depends on:** resolved plan.

- [ ] **TASK-001** — Rewrite `Category` (`Domain/Entities/Category.cs`) as a managed aggregate per
  Section 4: add `Description`, `ImagePath`, `IsActive`, the 100/250 limits, `Create`, `Rehydrate`,
  `Rename`, `ChangeDescription`, `Activate`, `Deactivate`, `AttachImage`, `RemoveImage`; **remove
  `Slug` and the `Create(Guid, string, string)` overload** (Decision 6). Extract the shared
  trim/validate helpers so `Create` and `Rename` cannot diverge. Leaves `ProductMapper.cs:33` broken
  until TASK-016.
- [ ] **TASK-002** — `CategoryNameRequiredException`, `CategoryNameTooLongException`,
  `CategoryDescriptionTooLongException` in `Domain/Exceptions/`, each carrying its Section 9
  `MessageKey`.
- [ ] **TASK-003** — `CategorySortOption` (four members) and `CategoryStatusFilter` (Active/Inactive
  only — `null` is "every category", RULE-16) in `Domain/Enums/`.
- [ ] **TASK-004** — Rewrite `tests/TheShop.Domain.Tests/Entities/CategoryTests.cs`: drop every slug
  assertion; cover name required/too-long on both `Create` and `Rename`, description limit,
  activate/deactivate transitions, and `RemoveImage` returning the prior path and clearing it.

**Completion gate:** Domain builds · no `Slug` member remains on `Category` · every new method
covered by a test · no outer-layer type in Domain · public API (including the changed `Create`
signature) reported for the Application step.

### Step 2 — Application (`shop-application-implementer`)

**Depends on:** Step 1's reported Domain API.

- [ ] **TASK-005** — `Features/Categories/DTOs/`: `CategoryDto`, `CategoryListItemDto`,
  `CategoryImageUpload`, `CategoryDeletionOutcomeDto`, `BlockedCategoryDto`,
  `CategoryStatusChangeDto`; `Features/Categories/Mappers/CategoryDtoMapper`.
- [ ] **TASK-006** — `ICategoryRepository` in `Common/Interfaces/` with the seven members the
  handlers need (`ExistsByNormalizedNameAsync(name, Guid? excludeCategoryId, ct)`, `AddAsync`,
  `GetPageAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteManyAsync`, `GetProductCountsAsync`);
  add `StorageArea.CategoryImages`.
- [ ] **TASK-007** — `GetCategoriesPageQuery(string? Search, CategoryStatusFilter? Status,
  CategorySortOption Sort, PaginationRequest Pagination)` + handler + validator in
  `Features/Categories/Queries/GetCategoriesPage/`; `[RequiresPermission("categories.view")]`;
  returns `Result<PagedResult<CategoryListItemDto>>`.
- [ ] **TASK-008** — `GetCategoryByIdQuery(Guid Id)` + handler in
  `Features/Categories/Queries/GetCategoryById/`; `[RequiresPermission("categories.view")]`
  (Decision 9); fails with `Category_NotFound`.
- [ ] **TASK-009** — `CreateCategoryCommand` + handler + validator in
  `Features/Categories/Commands/CreateCategory/`; `[RequiresPermission("categories.create")]`;
  upload-then-compensate per Flow 2 step 4.
- [ ] **TASK-010** — `UpdateCategoryCommand` + handler + validator in
  `Features/Categories/Commands/UpdateCategory/`; `[RequiresPermission("categories.edit")]`; image
  replace/remove disposal per Flow 3 step 4.
- [ ] **TASK-011** — `SetCategoryStatusCommand(IReadOnlyList<Guid>, bool)` + handler + validator in
  `Features/Categories/Commands/SetCategoryStatus/`; `[RequiresPermission("categories.edit")]`;
  `ChangedCount` excludes categories already in the target status (Flow 4 step 3).
- [ ] **TASK-012** — `DeleteCategoriesCommand(IReadOnlyList<Guid>)` + handler + validator in
  `Features/Categories/Commands/DeleteCategories/`; `[RequiresPermission("categories.delete")]`;
  validator rejects an empty/`Guid.Empty`-containing id list and de-duplicates.
- [ ] **TASK-013** — `CategoryErrorKeys` (`Features/Categories/`) plus every Section 9 key added to
  `Strings.resx` and mirrored in `Strings.fr.resx`.
- [ ] **TASK-014** — Application tests: one class per TASK-007…012 command/query/validator/handler,
  covering the partial-success matrix (all-deletable / mixed / all-blocked), self-name exclusion
  (AC-11), image disposal on remove and on replace, the create-failure image cleanup, and
  `ChangedCount` excluding already-in-target-status categories. Plus `CategoryDtoMapperTests`.

**Completion gate:** feature-folder convention followed · every admin command/query carries
`[RequiresPermission]` · every Section 9 outcome translated to a resource key · all contracts
consumed by Infra/Web are written and compiling.

### Step 3 — Contract freeze

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `Category.Create` / `Rehydrate` (no `slug`) + mutation methods | Domain | Application, Infrastructure | Stable |
| `CategorySortOption` / `CategoryStatusFilter` | Domain | Application, Infrastructure, Web | Stable |
| `ICategoryRepository` (7 members) | Application | Infrastructure | Stable |
| `StorageArea.CategoryImages` | Application | Infrastructure | Stable |
| `GetCategoriesPageQuery` / `Result<PagedResult<CategoryListItemDto>>` | Application | Web | Stable |
| `GetCategoryByIdQuery` / `Result<CategoryDto>` | Application | Web | Stable |
| `CreateCategoryCommand` / `UpdateCategoryCommand` / `CategoryImageUpload` | Application | Web | Stable |
| `SetCategoryStatusCommand` / `Result<CategoryStatusChangeDto>` | Application | Web | Stable |
| `DeleteCategoriesCommand` / `Result<CategoryDeletionOutcomeDto>` | Application | Web | Stable |

### Step 4 — Infrastructure (`shop-infra-implementer`) — runs in parallel with Step 5

**Depends on:** contract freeze (`ICategoryRepository` + the `Category` factory signatures stable).

- [ ] **TASK-015** — Migration `0020_manage_categories.sql` applied via Supabase MCP, exactly as in
  Section 10: the four new `categories` columns, `DROP COLUMN slug`, the three indexes, the three
  write RLS policies, the `category-images` bucket with its four storage policies, the two
  `SECURITY DEFINER` RPCs, and the `get_catalogue_filters()` `WHERE c.is_active` hardening. **No
  permission-seed work** — `categories.*` is already seeded by `0007` and granted to Admin
  (`p.module IN (… 'categories' …)`), SuperAdmin, and Support-view by `0017` (verified).
- [ ] **TASK-016** — `CategoryRecord`: add `description`, `image_path`, `is_active`, `created_at`,
  **drop `slug`**; new `Persistence/Mappers/CategoryMapper.cs` (`ToDomain`/`ToRecord`); fix
  `ProductMapper.cs:33` to `Category.Rehydrate(...)`; new records `CategoryProductCountRecord` and
  `DeleteCategoriesResultRecord`; register `[StorageArea.CategoryImages] = ("category-images",
  "categories")` in `SupabaseFileStorage`'s area registry.
- [ ] **TASK-017** — `SupabaseCategoryRepository : ICategoryRepository` in
  `Persistence/Repositories/`: paged/filtered/sorted query over `CategoryRecord` (`ILIKE` name
  search, `is_active` equality filter, name/`created_at` ordering **with the `id` tiebreak** so
  paging stays deterministic across tied rows), `GetByIdAsync`, `AddAsync`, `UpdateAsync`, and the
  two RPC calls. Register in `Infrastructure/DependencyInjection.cs`.
- [ ] **TASK-018** — Failure translation: `23505` on `ux_categories_name_normalized` →
  `CategoryErrorKeys.AlreadyExists`; `23503` from a raced product insert → the blocked-category
  outcome rather than a throw; `insufficient_privilege` from either RPC → `RbacErrorKeys.AccessDenied`.
- [ ] **TASK-019** — Infrastructure tests: `CategoryMapperTests`, `ManageCategoriesSchemaTests`
  (columns, indexes, all three write policies present — the `0019` lesson: a missing UPDATE/DELETE
  policy makes PostgREST return 200 OK having matched zero rows, i.e. a false success toast),
  `SupabaseCategoryRepositorySchemaTests` (paged projection + failure-code translation). Update
  `ProductMapperTests` for the dropped `CategoryRecord.Slug`, and **delete**
  `SupabaseProductRepositorySchemaTests.InsertCategory_WhenSlugAlreadyExists_ThrowsUniqueViolation`
  plus the `slug` parameter of its `InsertCategoryAsync` helper — it asserts a constraint that no
  longer exists.
- [ ] **TASK-029** — Migration `0021_brands_read_public.sql` (Decision 13): drop and recreate
  `brands_read` as `FOR SELECT USING (true)`, keeping the policy name. Update the two schema
  fixtures that hardcode the old DDL (`ManageBrandsSchemaTests.cs:504`,
  `SupabaseBrandRepositorySchemaTests.cs:432`), and **replace** the two
  `BrandsRead_WhenBrandIsInactive…_IsHidden` tests with coverage of the new contract: the row is
  readable by anon, `get_catalogue_filters()` omits the Inactive brand from its facet, and a
  published product carrying an Inactive brand still maps through `ProductMapper` without throwing.
  These assertion edits are pre-authorized by Decision 13 and are **not** a regression-gate breach.

**Completion gate:** migration applies cleanly · no `slug` reference remains in Infrastructure ·
both functions `SECURITY DEFINER` with their `authorize(...)` guard and pinned `search_path` ·
all three `categories` write policies verified live · `brands_read` is `USING (true)` and the brand
facet still hides Inactive brands · repository satisfies the frozen interface · no Infrastructure
type leaks inward.

### Step 5 — Web (`shop-ui-implementer`) — runs in parallel with Step 4

**Depends on:** contract freeze (command/DTO shapes stable).

**Figma references**

- **File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop
- **Page:** `2629:1873` — the **Categories** canvas. All four frames below were verified against the
  live file (Desktop Bridge, 2026-08-02); child ids are the real structure, not inferred.
- **Nodes:**
  - `2629:1947` — **Manage Categories** (1440×1732), the populated list. Children:
    `2629:1953` title row (heading + Add button), `2629:1956` the list region, and `2629:2054` the
    **Bulk Selection Bar** — an instance of the shared `2611:7585` component whose contents confirm
    the spec exactly: a "2 selected" count, Active / Inactive / Delete actions, and a dismiss icon
    button (FR-22/23/24/26). Map it to the shipped `ShopBulkActionBar`.
  - `2629:1932` — **Manage Categories – Empty** (1440×1300), the empty state (AC-22). Children:
    `2629:1938` title row, `2629:1941` the empty region.
  - `2629:1874` — **Add Category** form (1440×1090). Children: `2629:1880` title row, `2629:1882`
    the form body — name, description, image upload, status control (AC-6, AC-7).
  - `2629:1903` — **Edit Category** form (1440×1090). Children: `2629:1909` title row, `2629:1911`
    the form body — the same fields pre-filled, with image replace/remove (AC-8, AC-13).
- **Shell:** every frame carries the standard announcement bar, appbar, breadcrumb, footer and
  copyright bar as instances of existing components — reuse the shipped shell; only the body is new.

**Two stale artefacts in the design — do not copy them.** These frames were duplicated from the
Brands page and partially renamed:

1. **Frame names lie.** `2629:1874` is still named "Create Brand", `2629:1903` "Edit Brand",
   `2629:1956` "Brands", and `2629:1941` "Product Section". The *content* confirms these are the
   category screens — the breadcrumb leaf text reads "Create Category" / "Edit Category". Build
   categories; the names are cosmetic debt in the design file.
2. **The add/edit breadcrumb trail is wrong.** Both form frames render `Home › Brands › Create
   Category`. The list frames correctly render `Admin Console › Categories`. Implement all four on
   the list frames' trail — `BreadcrumbTrail.Admin()` then `Categories`, then the leaf — matching
   the shipped admin pages. Do **not** reproduce the `Home › Brands` prefix.

- [ ] **TASK-020** — `SortSlugs.Oldest = "oldest"` (append-only — the file's own contract);
  `CategorySortCatalogue : SortCatalogue<CategorySortOption>` in `Common/Sorting/` declaring all four
  orders with `NameAToZ` as the default (FR-5); `Strings.Sort_Oldest` added to both resx files.
- [ ] **TASK-021** — `CategoryQueryState` record (`IUrlQueryState<CategoryQueryState>`) in
  `Pages/Admin/`: search, status, sort, page; `Page = 1` folded into every mutator (RULE-10);
  defaults omitted from the URL; an unrecognised status or sort slug degrades to "no filter" /
  default rather than erroring.
- [ ] **TASK-022** — `ManageCategories.razor` / `.razor.cs` on
  `QueryStatePageBase<CategoryQueryState>`, `[Route(Routes.Admin.ManageCategories)]` +
  `[AuthorizePermission("categories.view")]`: `MudTable` rows per `2629:1947`, selection cleared in
  `ApplyStateAsync` (Decision 10, RULE-14), page-scoped select-all with the selected count (FR-22),
  inline status `MudSwitch` with the deactivate confirm (FR-19), per-row edit/delete inside
  `AuthorizeView Policy="@PolicyNames.Permission(...)"`, `ShopBulkActionBar` shown only when the
  selection is non-empty and offering only the permitted actions (FR-26), empty and no-match states,
  and the blocked-row in-use indicator (Decision 12).
- [ ] **TASK-023** — `AddCategory.razor` / `.razor.cs` per `2629:1874`,
  `[AuthorizePermission("categories.create")]`: name/description fields, `ShopImageUpload`, the
  status control **seeded Active** (Decision 11, RULE-5), validation display, success → back to the
  list.
- [ ] **TASK-024** — `EditCategory.razor` / `.razor.cs` per `2629:1903`,
  `[AuthorizePermission("categories.edit")]` (Decision 9): pre-filled form, `ShopImageUpload` for
  replace/remove, validation display, the deleted-category message with a route back to the list
  (AC-23), success → back to the list.
- [ ] **TASK-025** — `Routes.Admin.AddCategory = "/admin/categories/new"`,
  `EditCategoryPattern = "/admin/categories/{id:guid}/edit"`, `EditCategory(Guid id)`, and **update
  `ManageCategories`' doc comment** — it currently says "not backed by a page … resolves to the app's
  not-found route". `BusyKeys.Categories` (`ManageList`, `AddCategory`, `EditCategory`,
  `CategoryStatus`, `DeleteCategories`). Every UI resource string from Section 9 in `Strings.resx` +
  `Strings.fr.resx`.
- [ ] **TASK-026** — bUnit tests: `ManageCategoriesTests` (permission-driven control visibility,
  selection clearing on state change, bulk-bar gating, single-select filter swap/clear per AC-4,
  blocked-delete messaging), `AddCategoryTests` (Active default), `EditCategoryTests`,
  `CategorySortCatalogueTests`, `CategoryLocalizationTests`.

**Completion gate:** matches the four verified Figma nodes · the breadcrumb reads
`Admin Console › Categories › …` on all four screens, not the design's stale `Home › Brands` prefix ·
MudBlazor-only, no hardcoded strings or design tokens (Rules 2–5, 11, 14–19) · consumes only frozen
contracts · users lacking a permission see the control **absent, not disabled** · the status filter
can never reach a both-selected state and offers no All/None option (RULE-16).

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5 complete.

- [ ] **TASK-027** — Cross-layer verification: solution builds with zero `Category.Slug` references,
  DI resolves `ICategoryRepository`, migration applied, and the primary + failure flows work
  end-to-end — including a category referenced only by an **unpublished** product (the case
  Decision 3 exists for) and the AC-24 rename the dropped slug constraint used to reject.
  **Also load the live product catalogue** and confirm that (a) deactivating a category removes it
  from the filter facet while its products still list and open correctly (Decision 2, RULE-13), and
  (b) the admin console's Categories card now navigates to a real page.
- [ ] **TASK-030** — Brands regression pass for Decision 13 / TASK-029, on the live catalogue:
  deactivate a brand that owns published products, then confirm the catalogue still renders those
  products (the `ProductMapper` throw is gone), the brand has left the filter facet, and
  manage-brands still lists and edits it. This is the human check behind a change to shipped
  customer-facing behaviour.
- [ ] **TASK-028** — Run `/theshop.test manage-categories` → `/theshop.verify manage-categories` →
  `/theshop.review manage-categories` → `/theshop.document`.

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
| AC-1: 10/page, image+name+description+status, name A→Z, no status selected | TASK-007, TASK-017, TASK-021, TASK-022 |
| AC-2: next page keeps search/filter/sort | TASK-007, TASK-021, TASK-022 |
| AC-3: case-insensitive name search, resets to page 1 | TASK-007, TASK-017, TASK-021 |
| AC-4: single-select status swap/clear, never All/None, never both | TASK-003, TASK-007, TASK-017, TASK-021, TASK-022, TASK-026 |
| AC-5: name Z→A keeps search+filter, page 1 | TASK-003, TASK-017, TASK-020, TASK-021 |
| AC-6: add with name+description+image, created Active by default | TASK-001, TASK-009, TASK-023 |
| AC-7: name-only add works; Inactive-before-save works | TASK-001, TASK-009, TASK-023 |
| AC-8: edit all four fields | TASK-001, TASK-010, TASK-024 |
| AC-9: empty/whitespace name rejected | TASK-001, TASK-002, TASK-009, TASK-010, TASK-013 |
| AC-10: duplicate name rejected, input preserved | TASK-006, TASK-015, TASK-018, TASK-010, TASK-024 |
| AC-11: no-op save succeeds (self-name excluded) | TASK-006, TASK-010, TASK-014, TASK-017 |
| AC-12: name >100 / description >250 rejected with the right message | TASK-001, TASK-002, TASK-009, TASK-010, TASK-013 |
| AC-13: image remove / replace, old object gone | TASK-001, TASK-010, TASK-014, TASK-024 |
| AC-14: bad image type/size refused, existing image intact | TASK-009, TASK-010, TASK-013, TASK-023, TASK-024 |
| AC-15: Inactive hides from customer filter + assignment, products kept | TASK-001, TASK-011, TASK-015, TASK-022, TASK-027 |
| AC-16: inline activate immediate, deactivate confirms | TASK-011, TASK-022 |
| AC-17: single delete confirm + permanence, image discarded | TASK-012, TASK-015, TASK-017, TASK-022 |
| AC-18: cancelling the confirm changes nothing | TASK-022, TASK-026 |
| AC-19: in-use category refused with product count (incl. discontinued) | TASK-012, TASK-015, TASK-014, TASK-022 |
| AC-20: view-only sees no add/edit/toggle/delete/bulk; direct link denied | TASK-007, TASK-022, TASK-023, TASK-024, TASK-026 |
| AC-21: no `categories.view` → access denied on the page | TASK-022, TASK-026 |
| AC-22: empty state and no-match state | TASK-007, TASK-022 |
| AC-23: add/edit links; edit survives rename; deleted → message + return | TASK-008, TASK-024, TASK-025 |
| AC-24: "Vape Kits" + rename to "Vape.Kits" both allowed (no slug clash) | TASK-001, TASK-015, TASK-010, TASK-016, TASK-019 |
| AC-25: EN/FR across list, filters, sort, forms, dialogs, messages | TASK-013, TASK-020, TASK-025, TASK-026 |
| AC-26: keyboard + screen-reader across all controls and dialogs | TASK-022, TASK-023, TASK-024, TASK-026 |
| AC-27: page-scoped select-all with count; selection clears on state change | TASK-021, TASK-022, TASK-026 |
| AC-28: bulk deactivate confirms with count, applies to all | TASK-011, TASK-022 |
| AC-29: mixed bulk delete — 3 deleted, both counts, 2 blocked stay selected and named | TASK-012, TASK-015, TASK-014, TASK-022 |
| AC-30: all-blocked selection deletes nothing, explains | TASK-012, TASK-015, TASK-014, TASK-022 |
| AC-31: edit-but-not-delete sees no bulk Delete; direct attempt denied | TASK-012, TASK-015, TASK-022, TASK-026 |
| AC-32: newest/oldest sorts, page 1, search+filter retained | TASK-003, TASK-015, TASK-017, TASK-020, TASK-021 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

- `CreateCategoryCommandValidator` / `UpdateCategoryCommandValidator`: `Name` not empty after trim
  (RULE-1) → `Category_NameRequired`; `Name` ≤ 100 and `Description` ≤ 250 (RULE-3) →
  `Category_NameTooLong` / `Category_DescriptionTooLong`; image content type ∈ {png, jpeg, webp} and
  ≤ 2 MB (RULE-4) → `Category_ImageInvalidType` / `Category_ImageTooLarge`. `UpdateCategoryCommand`
  additionally requires a non-empty `Id`.
- `GetCategoriesPageQueryValidator`: `Pagination.Page ≥ 1`; `PageSize` clamped to 10 via
  `PaginationRequest.Normalized(maxPageSize: 10)` (spec constraint — the page size is fixed and not
  staff-selectable) → `Category_PageInvalid`.
- `SetCategoryStatusCommandValidator` / `DeleteCategoriesCommandValidator`: `CategoryIds` non-empty,
  no `Guid.Empty`, de-duplicated → `Category_CategoryIdsRequired`.

### Domain exceptions

- `CategoryNameRequiredException` — `Create`/`Rename` with a blank name. `MessageKey = Category_NameRequired`.
- `CategoryNameTooLongException` — > 100 chars. `MessageKey = Category_NameTooLong`.
- `CategoryDescriptionTooLongException` — > 250 chars. `MessageKey = Category_DescriptionTooLong`.

### Result.Fail error keys (new entries in `Strings.resx`)

| Key | English text |
|---|---|
| `Category_NameRequired` | "A category name is required." |
| `Category_NameTooLong` | "The category name must be 100 characters or fewer." |
| `Category_DescriptionTooLong` | "The description must be 250 characters or fewer." |
| `Category_AlreadyExists` | "A category with that name already exists." |
| `Category_ImageInvalidType` | "The image must be a PNG, JPG, or WebP file." |
| `Category_ImageTooLarge` | "The image must be 2 MB or smaller." |
| `Category_CreateFailed` | "The category could not be created. Please try again." |
| `Category_NotFound` | "This category no longer exists." |
| `Category_UpdateFailed` | "The category could not be updated. Please try again." |
| `Category_DeleteFailed` | "The category could not be deleted. Please try again." |
| `Category_StatusChangeFailed` | "The category's status could not be changed. Please try again." |
| `Category_PageInvalid` | "That page of categories does not exist." |
| `Category_CategoryIdsRequired` | "Select at least one category first." |
| `Category_Created` | "Category created." |
| `Category_InUse` | "{0} contains {1} product(s) and cannot be deleted. Make it Inactive instead." |
| `Category_BulkDeletePartial` | "{0} category(ies) deleted. {1} kept because products still belong to them." |
| `Category_BulkDeleteAllBlocked` | "None of the selected categories could be deleted — products still belong to them. Make them Inactive instead." |

Existing keys reused: `RbacErrorKeys.AccessDenied`, `Filter_Status`, `Filter_StatusActive`,
`Filter_StatusInactive`, `Filter_Clear`, `Sort_Label`, `Sort_NameAZ`, `Sort_NameZA`, `Sort_Newest`.
New UI-only strings — `Sort_Oldest` (TASK-020) and the `ManageCategories_*` / `AddCategory_*` /
`EditCategory_*` families for the page title, heading, column headers, search placeholder, empty and
no-match states, bulk-action labels, confirm-dialog copy, success snackbars, row ARIA labels, and the
in-use indicator — are added in TASK-025, mirroring the shipped `ManageBrands_*` key set one-for-one.
All keys mirrored in `Strings.fr.resx` (`[TODO]` acceptable on the first pass — the review step's
French-completeness gate catches stragglers).

### Edge cases → owner

| Spec edge case | Handled by |
|---|---|
| No categories exist at all | empty state with permission-gated Add (TASK-022) |
| Search/filter matches nothing | no-match state + clear affordance (TASK-022) |
| Last page emptied by a delete | `Paginator.LastAsync()` clamp after reload (TASK-022) |
| Category already deleted by someone else | `Category_NotFound` from TASK-008/010; list refresh |
| No-op save on the edit form | `excludeCategoryId` in the uniqueness check (TASK-006, TASK-017) |
| Add without description or image | both nullable; `PlaceholderImage.For(name)` in the list projection (TASK-017) |
| Image removed while editing | `RemoveImage()` + best-effort storage delete after commit (TASK-010) |
| Bad image type/size | validator, before any upload runs (TASK-009, TASK-010) |
| Add/edit/delete fails mid-flight | `Result.Fail` + input preserved; created-but-unsaved image cleaned up (TASK-009) |
| List still loading | `BusyFor` on `BusyKeys.Categories.ManageList` — loading, not "no results" (TASK-022) |
| Inline toggle fails | switch reverts, `Localizer[result.Error]` (TASK-022) |
| Deactivating the only category a set of products has | products keep the category; only the facet drops it (TASK-015, Decision 2) |
| Bulk delete where everything is in use | `Category_BulkDeleteAllBlocked` (TASK-012, TASK-022) |
| Selection + page/search/filter/sort change | cleared in `ApplyStateAsync` (TASK-022, RULE-14) |
| Confirm dismissed with a selection | selection retained; nothing sent (TASK-022) |
| Bulk action partly fails | the delete RPC is one transaction — per-category outcome, never indeterminate — then the list reloads from the server (TASK-015, TASK-022) |
| Rename previously blocked by a slug clash | no longer possible — constraint dropped (TASK-001, TASK-015, AC-24) |

## 10. Database Schema & RLS Policies

### Schema

```sql
-- ============================================================================
-- 0020_manage_categories
-- Promotes `categories` from read-only reference data to a managed admin
-- aggregate. Companion plan: .specs/manage-categories/plan.md §10
-- ============================================================================

ALTER TABLE categories
    ADD COLUMN IF NOT EXISTS description TEXT,
    ADD COLUMN IF NOT EXISTS image_path  TEXT,
    ADD COLUMN IF NOT EXISTS is_active   BOOLEAN     NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS created_at  TIMESTAMPTZ NOT NULL DEFAULT now();

-- Retire the name-derived identifier (spec FR-11, Decision 6). Dropping the column drops
-- categories_slug_key with it, so a rename can no longer be refused for a slug clash (AC-24).
-- Migration 0002 seeds products via `WHERE slug = …`, but 0002 always runs before this one on
-- a replay, so that seed stays valid.
ALTER TABLE categories DROP COLUMN IF EXISTS slug;

-- RULE-2: case/space-insensitive name uniqueness, enforced at the DB. After the slug drop this
-- is the ONLY uniqueness constraint a category name must satisfy.
CREATE UNIQUE INDEX IF NOT EXISTS ux_categories_name_normalized
    ON categories (lower(btrim(name)));

CREATE INDEX IF NOT EXISTS idx_categories_name_lower  ON categories (lower(name));
CREATE INDEX IF NOT EXISTS idx_categories_created_at  ON categories (created_at DESC);

-- Authoritative per-category product counts. SECURITY DEFINER because products' only SELECT
-- policy is products_public_read USING (is_published = true) — an unpublished product must
-- still count against deletion (spec RULE-6, AC-19), and no client role can see it.
CREATE OR REPLACE FUNCTION public.category_product_counts(category_ids UUID[])
RETURNS TABLE (category_id UUID, product_count BIGINT)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('categories.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    SELECT c.id, count(p.id)
    FROM categories c
    LEFT JOIN products p ON p.category_id = c.id
    WHERE c.id = ANY(category_ids)
    GROUP BY c.id;
END;
$$;

-- Atomic partial-success deletion (spec RULE-15). Counting and deleting in one statement closes
-- the TOCTOU window a client-side guard would leave open.
CREATE OR REPLACE FUNCTION public.delete_categories(category_ids UUID[])
RETURNS TABLE (id UUID, name TEXT, image_path TEXT, product_count BIGINT, deleted BOOLEAN)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('categories.delete') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    WITH candidates AS (
        SELECT c.id, c.name, c.image_path,
               (SELECT count(*) FROM products p WHERE p.category_id = c.id) AS product_count
        FROM categories c
        WHERE c.id = ANY(category_ids)
    ),
    removed AS (
        DELETE FROM categories
        WHERE categories.id IN (SELECT k.id FROM candidates k WHERE k.product_count = 0)
        RETURNING categories.id
    )
    SELECT k.id, k.name, k.image_path, k.product_count,
           EXISTS (SELECT 1 FROM removed r WHERE r.id = k.id)
    FROM candidates k;
END;
$$;

REVOKE ALL ON FUNCTION public.category_product_counts(UUID[]) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.delete_categories(UUID[])       FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.category_product_counts(UUID[]) TO authenticated;
GRANT EXECUTE ON FUNCTION public.delete_categories(UUID[])       TO authenticated;

-- Category-image bucket, staff-visible only (spec: the image reaches no customer surface).
INSERT INTO storage.buckets (id, name, public)
VALUES ('category-images', 'category-images', true)
ON CONFLICT (id) DO NOTHING;
```

### RLS policies

`categories` has RLS enabled (migration `0002`) but carries **only** `categories_public_read`
(`USING (true)`) — there are no write policies at all, so every insert/update/delete is currently
denied. All three are added here; per the `0019` lesson, an absent UPDATE/DELETE policy makes
PostgREST return `200 OK` having matched zero rows, i.e. a silent false success.

```sql
-- SELECT is deliberately UNCHANGED — see plan §5 Decision 2. Restricting it to is_active would
-- null out ProductRecord's embedded CategoryRecord for every product in a deactivated category
-- and break the catalogue, contradicting spec RULE-13 ("no product is hidden as a side effect").
-- Inactive categories are hidden from customers by the facet filter below, not by RLS.

CREATE POLICY "categories_admin_insert" ON categories
    FOR INSERT WITH CHECK ((SELECT public.authorize('categories.create')));

CREATE POLICY "categories_admin_update" ON categories
    FOR UPDATE
    USING ((SELECT public.authorize('categories.edit')))
    WITH CHECK ((SELECT public.authorize('categories.edit')));

CREATE POLICY "categories_admin_delete" ON categories
    FOR DELETE USING ((SELECT public.authorize('categories.delete')));

-- category-images storage objects: read for categories.view, write per action (mirrors brand-logos).
CREATE POLICY "category_images_read" ON storage.objects
    FOR SELECT USING (bucket_id = 'category-images' AND (SELECT public.authorize('categories.view')));
CREATE POLICY "category_images_insert" ON storage.objects
    FOR INSERT WITH CHECK (bucket_id = 'category-images' AND (SELECT public.authorize('categories.create')));
CREATE POLICY "category_images_update" ON storage.objects
    FOR UPDATE USING (bucket_id = 'category-images' AND (SELECT public.authorize('categories.edit')))
    WITH CHECK (bucket_id = 'category-images' AND (SELECT public.authorize('categories.edit')));
CREATE POLICY "category_images_delete" ON storage.objects
    FOR DELETE USING (bucket_id = 'category-images' AND (SELECT public.authorize('categories.delete')));
```

### Companion migration — `0021_brands_read_public.sql` (Decision 13)

```sql
-- brands_read has been USING (is_active OR authorize('brands.view')) since migration 0013. That
-- predicate nulls out ProductRecord's embedded BrandRecord for any published product whose brand
-- is Inactive, and ProductMapper.ToDomain throws on a null embed — so deactivating a brand breaks
-- catalogue rendering for its products. The customer-facing goal 0013 stated ("Inactive brands
-- must be invisible to customers") is already met by get_catalogue_filters()' `WHERE b.is_active`
-- brand facet, added in 0012. Revert the predicate; keep the policy name.
DROP POLICY IF EXISTS "brands_read" ON brands;

CREATE POLICY "brands_read" ON brands
    FOR SELECT USING (true);
```

No change to `brands_admin_insert` / `brands_admin_update` / `brands_admin_delete`, and no change to
`get_catalogue_filters()`' brand branch — it already carries the facet filter this now relies on.

### Customer-facing facet (spec FR-13)

```sql
-- get_catalogue_filters(): the category facet gains the is_active predicate the brand facet
-- already has (migration 0012). This is the whole of FR-13's customer-facing effect — an
-- Inactive category leaves the filter sidebar while its products stay published and browsable.
-- Re-declared in full (CREATE OR REPLACE) with only the category branch changed:
--     FROM public.categories c
--     WHERE c.is_active            ← added
```

The two RPCs above are `SECURITY DEFINER` and bypass RLS by design; each re-checks the caller's
permission with `authorize(...)` as its first statement, so the permission boundary is preserved
(`authorize()` reads live role assignments, not JWT claims). `products.category_id`'s
`NOT NULL REFERENCES categories(id)` with no cascade remains the last-resort guard behind RULE-6.

**No permission-seed work.** `categories.view/create/edit/delete` are seeded by `0007` and granted to
Admin (`p.module IN (… 'categories' …)`), to SuperAdmin by its all-permissions seed, and view-only to
Support by `0017` — all verified against the committed migrations.

## 11. Open Questions, Risks & Assumptions

No open questions and no unratified assumptions remain — all six items resolved into Sections 4–10:
the `AdminListPageBase` extraction was declined (Decision 1), the brands RLS hazard was **mitigated**
rather than accepted (Decision 13 + TASK-029/TASK-030 + the `0021` migration in Section 10), all four
Figma nodes were verified against the live file (Step 5), and both assumptions were ratified as
written (the shared `created_at` now sits in Section 4's index notes, `RemoveImage()`'s return
contract in Section 4's entity description).

Two risks are knowingly accepted and stay logged here:

- **⚠️ Risk — ✅ Accepted:** `ALTER TABLE categories DROP COLUMN slug` (TASK-015) is destructive and
  irreversible; the values are unrecoverable and reinstating slugs later means a fresh backfill.
  Accepted because nothing reads the value — verified across `src/`, the migrations, and
  `get_catalogue_filters()` — so no consumed data is lost, and slugs are regenerable from names.
  Required by FR-11/AC-24.
- **⚠️ Risk — ✅ Accepted:** `delete_categories` returns `image_path` so the handler can dispose the
  image, but the row is already gone by the time storage deletion runs — a storage failure orphans
  the object with no row to retry from. Accepted as consistent with the create path's compensation
  stance: an orphaned object never fails the request. Owned by TASK-012.

---
**Status:** Resolved · **Spec:** `.specs/manage-categories/spec.md` · **Created:** 2026-08-02 · **Resolved:** 2026-08-02

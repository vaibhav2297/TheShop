# Implementation Plan — Manage Brands

> Companion to `.specs/manage-brands/spec.md`. This plan is technical (HOW); the spec is
> non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

Turn the `/admin/brands` permission-gated shell into a full brand-management surface: a paged,
searchable, filterable, sortable brand list with row selection and bulk Activate/Deactivate/Delete,
plus an id-addressed edit form. Three cross-cutting jobs ride along: deletion needs an authoritative
product-reference check that counts **unpublished** products (spec RULE-6), which today's
`products_public_read` RLS policy makes impossible from the client; the catalogue's filter sidebar
and sort control are extracted into feature-agnostic shared components; and `Brand.Slug` is removed
outright across all four layers (spec FR-9), retiring the name-derived identifier and the unique
constraint that could reject a legitimate rename.

## 2. Tech Stack

- **Domain:** C# (no external deps) — `Brand` mutation methods, slug removal, two new enums.
- **Application:** MediatR, FluentValidation, `Result<T>`, `PagedResult<T>` / `PaginationRequest`
  (both already generic in `Common/Models/`).
- **Infrastructure:** `supabase-csharp` (Postgrest queries + two RPCs), `IFileStorage` for logo
  disposal.
- **Web:** MudBlazor (`MudTable`, `MudCheckBox`, `MudSwitch`, `MudDialog`, `MudSelect`), existing
  `QueryStatePageBase<TState>`, `Paginator<T>`, `ShopPagination`, `ShopImageUpload`; bUnit for tests.
- **Persistence:** Supabase (PostgreSQL + RLS). All four `brands` RLS policies already exist.

## 3. High-level Architecture

```
ManageBrands.razor  (selection, filters, sort, search, bulk bar)
   ↓
IMediator.Send(GetBrandsPageQuery | SetBrandStatusCommand | DeleteBrandsCommand)
   ↓
AuthorizationBehavior  ← [RequiresPermission("brands.view|edit|delete")]
   ↓
{…}Handler (Application)
   ├── IBrandRepository.GetPageAsync / GetProductCountsAsync / DeleteManyAsync
   ├── brand.Rename(...) / .Activate() / .Deactivate()   // invariants enforced here
   └── IFileStorage.DeleteAsync(StorageArea.BrandLogos, …)  // RULE-11
   ↓
SupabaseBrandRepository (Infrastructure)
   ├── From<BrandRecord>() … Range(from,to)          → brands (RLS: brands_read)
   ├── rpc brand_product_counts(uuid[])              → SECURITY DEFINER, bypasses products RLS
   └── rpc delete_brands(uuid[])                     → atomic partial-success delete
   ↓
Result<PagedResult<BrandListItemDto>> | Result<BrandDeletionOutcomeDto>
   ↓
ManageBrands.razor → PushStateAsync(BrandQueryState) → URL → ApplyStateAsync → re-render
```

## 4. Data Model

### Domain entities & value objects

- **`Brand`** (modified, `Domain/Entities/Brand.cs`):
  - **Removed:** the `Slug` property, `GenerateSlug`, the `[GeneratedRegex] NonAlphanumericRun()`
    member, and consequently the `partial` modifier on the class (the source-generated regex was its
    only reason to be partial). `Create(name, description, isActive)` keeps its signature;
    `Rehydrate(...)` **loses its `slug` parameter** — a breaking change with two call sites
    (`BrandMapper.cs:12`, `ProductMapper.cs:34`).
  - `Name` becomes `private set` (`Slug` no longer exists to keep in sync).
  - `Rename(string name)` — trims and re-validates only. Throws `BrandNameRequiredException` /
    `BrandNameTooLongException`.
  - `ChangeDescription(string? description)` — throws `BrandDescriptionTooLongException`.
  - `Activate()` / `Deactivate()` — set `IsActive` (RULE-5 makes a bool sufficient).
  - `RemoveLogo()` — returns the previous `LogoPath` (or `null`) and clears it, so the handler can
    dispose the object (RULE-11) without the entity knowing about storage.
  - Existing `AttachLogo(string)` is reused for replace; the handler captures the outgoing
    `LogoPath` before calling it.
- **`BrandSortOption`** (new, `Domain/Enums/`) — `NameAToZ = 0`, `NameZToA = 1`. Name is the only
  sort field (spec FR-5).
- **`BrandStatusFilter`** (new, `Domain/Enums/`) — `Active = 1`, `Inactive = 2`. Carried as
  `BrandStatusFilter?` everywhere; **"all" is the absence of a filter (`null`), not a member** — the
  enum holds only values that narrow the query, matching the `string? Search` beside it.
- **`Category.Slug`** is deliberately **untouched** — the spec scopes the retirement to brands.

### DTOs (Application → Web)

- **`BrandDto`** (modified) — drops `Slug`; now `Id, Name, Description, LogoUrl, IsActive`.
- **`BrandListItemDto`** (new) — `Id, Name, Description, LogoUrl, IsActive, ProductCount`.
  `ProductCount` is the authoritative count from the RPC; it drives the FR-13 message and lets the
  UI pre-warn before a doomed delete.
- **`BrandDeletionOutcomeDto`** (new) — `int DeletedCount, IReadOnlyList<BlockedBrandDto> Blocked`.
- **`BlockedBrandDto`** (new) — `Guid Id, string Name, int ProductCount`. `Name` feeds the
  *single*-brand `Brand_InUse` message and the blocked-row in-use indicator; the *bulk* message is
  count-only and never enumerates names (Decision 12).
- **`BrandStatusChangeDto`** (new) — `int ChangedCount`, counting only brands that actually changed
  state; a brand already in the target status is a no-op and is excluded.
- **Shared filtering models moved to `Common/Filtering/`** (see Decision 4): `FilterGroupDto`,
  `FilterOptionDto`, `AppliedFilterDto`, `RangeFilterDto` (was `PriceRangeDto`), `SortOptionDto`
  (new — `Value`, `LabelKey`).

### Database tables (new or modified)

| Table | Purpose | Key columns |
|---|---|---|
| `brands` (modified) | **Drop `slug`** (and with it the `brands_slug_key` unique index) | `id, name, description, logo_path, is_active, created_at` |
| `products` (unmodified) | `brand_id UUID NOT NULL REFERENCES brands(id)` — no cascade, so the FK is the last-resort guard behind RULE-6 | `brand_id` |

No new tables and **no new RLS policies** — `brands_read`, `brands_admin_insert`,
`brands_admin_update`, `brands_admin_delete` are already live and verified. Two new functions, one
new index, one dropped column (Section 10).

### Indexes

- `idx_brands_name_lower ON brands(lower(name))` (new) — serves the `ORDER BY lower(name)` behind
  `BrandSortOption`. `brands` is a small reference table, so the `ILIKE '%…%'` search needs no
  trigram index; revisit only if the table grows past a few thousand rows.
- `ux_brands_normalized_name` (existing, unique on normalized name) — enforces RULE-2 and is now the
  **only** uniqueness constraint on a brand name.
- `brands_slug_key` — **dropped** with the column.

## 5. Core Design Decisions

1. **Decision:** Single and bulk actions share one set-based command each —
   `SetBrandStatusCommand(IReadOnlyList<Guid> BrandIds, bool IsActive)` and
   `DeleteBrandsCommand(IReadOnlyList<Guid> BrandIds)`. The inline row toggle (FR-18) and the
   single-row delete (FR-12) are simply one-element calls.
   - **Why:** RULE-13's partial-success semantics then apply uniformly, and FR-13 (a single in-use
     brand is refused with its product count) is literally the one-element case of FR-22 — one
     handler, one code path, no divergence between the single and bulk refusal messages.
   - **Rejected:** separate `DeleteBrandCommand` + `BulkDeleteBrandsCommand` — two handlers that
     must keep identical guard semantics, exactly the drift RULE-6/RULE-13 can't afford.

2. **Decision:** Deletion executes in a `SECURITY DEFINER` PL/pgSQL RPC, `delete_brands(uuid[])`,
   that re-checks `authorize('brands.delete')`, counts referencing products, deletes only the
   unreferenced brands, and returns both the deleted ids and the blocked ones with their counts.
   - **Why:** two hard reasons. (a) `products_public_read` — `USING (is_published = true)` — is the
     *only* SELECT policy on `products`, so **no** client can count unpublished products; a
     client-side guard would report 0 for a brand referenced only by unpublished products, attempt
     the delete, and surface a raw `23503` FK violation instead of the message RULE-6 specifies
     (which explicitly counts products "whether or not currently available to customers").
     (b) Counting and deleting in one statement removes the TOCTOU window in which a product is
     assigned to a brand between the check and the delete.
   - **Rejected:** count-then-delete from the repository (wrong counts + racy); adding a
     `products` SELECT policy for `products.view` holders (widens the products security surface for
     a brands feature, and a `brands.delete` holder needn't hold `products.view`); relying on
     catching `23503` (yields a count-free message and can't do partial success).

3. **Decision:** Per-brand product counts for the list come from a second `SECURITY DEFINER` RPC,
   `brand_product_counts(uuid[])`, gated on `authorize('brands.view')` and called with just the ids
   on the current page.
   - **Why:** same RLS blindness as Decision 2 — the count shown next to a brand must match the
     count the delete guard will use, or the UI contradicts itself. Precedent:
     `0013_admin_dashboard_counts.sql` already uses a permission-guarded `SECURITY DEFINER` function.
   - **Rejected:** a Postgrest embedded `products(count)` resource (RLS-filtered to published only);
     making the whole paged list an RPC (loses Postgrest paging/filtering for no benefit).

4. **Decision:** Extract the catalogue's filter sidebar and sort control into feature-agnostic
   shared components — `ShopFilterPanel` and `ShopSortSelect` in `Components/Common/` — driven
   entirely by `IReadOnlyList<FilterGroupDto>` / `IReadOnlyList<SortOptionDto>`, with the filter
   models moved from `Features/Products/DTOs/` to `Application/Common/Filtering/`. Both inherit
   `MudComponentBase` and forward `Class`/`Style` to their root (Rules 23, 24). `PriceRangeDto`
   becomes `RangeFilterDto` and the range group takes a `Func<decimal,string>` formatter parameter
   instead of calling `CurrencyFormatter` directly.
   - **Why:** per user direction. `ProductFilterPanel` is already fully data-driven ("hard-codes no
     filter set") — only its *placement*, its currency-hardcoded range group, and the DTOs' feature
     namespace tie it to Products. Brands is the second real call site, which is precisely when
     Rule 25 says extract. `PagedResult<T>`, `PaginationRequest`, `Paginator<T>`,
     `QueryStatePageBase<TState>`, and `ShopPagination` are already generic and reused as-is.
   - **Rejected:** a second brand-specific panel (the duplication the direction exists to prevent);
     a fully generic `SortDescriptor` replacing the per-feature enums — `ProductSortOption` /
     `BrandSortOption` are use-case concepts that keep sort orders compile-time-checked and their
     slug maps deep-linkable; only the *rendering* needed generalizing.

5. **Decision:** The status filter renders through `ShopFilterPanel` as a single-select
   `FilterGroupDto` (`FilterKind.SingleSelect`, a new enum member) rather than a multi-select group.
   - **Why:** Active/Inactive are mutually exclusive (spec FR-4); modelling it as multi-select
     would allow the meaningless "Active + Inactive" state and a second way to express "all".
   - The group offers **no "All" option**: single-select options render as checkboxes, so
     unchecking the selected one already says "no narrowing" and emits a `null` value. An explicit
     "All" would be a second encoding of the same state, and would force a no-op branch on every
     consumer down to the repository.
   - **Rejected:** a bare `MudSelect` outside the panel (splits the filter surface in two and
     defeats the shared-panel direction).

6. **Decision:** Remove `Brand.Slug` end to end — Domain property and generator, `BrandDto` field,
   `BrandRecord` column mapping, the `brands.slug` column with its `brands_slug_key` unique index,
   `BrandErrorKeys.SlugConflict`, and the repository's slug-violation translation — in this feature,
   per user direction and spec FR-9.
   - **Why:** nothing reads the brand slug. Verified across `src/`: no route, `Href`, or
     `NavigateTo` builds a URL from it; the catalogue's brand filter keys options on
     `b.Id.ToString()` (`ProductFilterDefinitions`), not the slug; `get_catalogue_filters()` does not
     project it; and no `.razor` renders it. Its only live effect was a `UNIQUE` index that can
     reject a name RULE-2 permits — the documented `"Test Verify"` vs `"Test.Verify"` collision
     (`SupabaseBrandRepository.cs:20`), which spec AC-28 now requires to succeed. Removing it deletes
     a rejection path with no compensating benefit.
   - **Rejected:** keeping the column but dropping only the unique index (leaves the slug unusable
     as a lookup key — defers the decision rather than settling it); keeping it as-is (AC-28 then
     cannot pass); also retiring `Category.Slug` (out of scope per the spec — see the ⚠️ in
     Section 11).

7. **Decision:** `ExistsByNormalizedNameAsync` gains an `excludeBrandId` parameter.
   - **Why:** AC-9 requires a brand to keep its own name on a no-op save, which a self-inclusive
     uniqueness check would reject. With the slug gone, `ux_brands_normalized_name` is the sole
     uniqueness constraint, so this check plus its `23505` translation is the whole of RULE-2.
   - **Rejected:** filtering the self-match in the handler (pushes a persistence concern up a layer
     and still costs the same round trip).

8. **Decision:** The edit route is `/admin/brands/{id:guid}/edit`, id-keyed.
   - **Why:** per user direction and spec FR-24 — and now the only option, since the slug that could
     otherwise have keyed it no longer exists.
   - **Rejected:** a dialog-based edit (the spec requires a bookmarkable per-brand address).

9. **Decision:** Selection state lives in the page's `HashSet<Guid>` and is **not** part of
   `BrandQueryState` (the URL). Every state application clears it.
   - **Why:** RULE-12 requires the selection to clear on any page/search/filter/sort change, and
     `QueryStatePageBase` funnels all four through one `ApplyStateAsync` — clearing there satisfies
     RULE-12 by construction rather than by remembering to do it in four handlers. Selection is also
     transient and not worth deep-linking.
   - **Rejected:** URL-persisted selection (violates RULE-12 on Back/Forward, and bloats links).

10. **Decision:** Deactivation confirmations (RULE-14) and delete confirmations (RULE-7) share one
    new reusable `ShopConfirmDialog` in `Components/Common/`, taking title/body/confirm-label
    resource keys and a `Color` for the confirm button.
    - **Why:** four confirmation sites (row delete, bulk delete, row deactivate, bulk deactivate)
      with identical mechanics; no confirm dialog exists in the project yet. Rule 25's "second call
      site" bar is comfortably cleared.
    - **Rejected:** four inline `MudDialog` blocks; `MudMessageBox` alone (no resource-key discipline
      and no consistent focus handling for the accessibility constraint).

11. **Decision:** `GetBrandByIdQuery` requires `brands.view`, not `brands.edit`; AC-16's
    "direct link is denied" is enforced by the **page**, which wraps its body in
    `<AuthorizeView Policy="@PolicyNames.Permission(PermissionCatalogue.Brands.Edit.Code)">` —
    the same shape `AddBrand.razor` already uses for `brands.create`.
    - **Why:** reading a brand is a read, and gating it on `edit` would make a future read-only
      brand detail view need a duplicate query. The authorization boundary doesn't weaken: a
      view-only admin who deep-links to `/admin/brands/{id}/edit` gets the standard access-denied
      experience from the page gate, and `UpdateBrandCommand` independently requires `brands.edit`,
      so no write is reachable without it.
    - **Rejected:** `brands.edit` on the query (couples a read permission to the one caller that
      happens to exist today); relying on the query alone for AC-16 (the page would render its
      chrome before failing, which is not the "standard access-denied experience" AC-16 asks for).

12. **Decision:** The bulk-delete outcome message reports **counts only**
    (`Brand_BulkDeletePartial`); the blocked brands are identified in the **table**, where they stay
    selected and carry an in-use indicator showing their product count. The single-brand refusal
    (`Brand_InUse`, FR-13) still names its one brand inline.
    - **Why:** per user direction. AC-24's "the two in-use brands … are named as still in use" is
      satisfied by the list rather than the toast — with the rows already staying selected
      (RULE-13), the indicator names them in the place the user is looking, and the message stays
      readable regardless of selection size.
    - **Rejected:** enumerating names in the snackbar (unbounded message length, needs truncation
      rules); a separate blocked-results dialog (new component, scope beyond Step 5).

## 6. Core Functional Flow

### Flow 1: Browse / search / filter / sort / page (spec Behaviors 1–3)

1. `ManageBrands.razor.cs : QueryStatePageBase<BrandQueryState>` — `ApplyStateAsync(state, ct)` runs
   on first render and every URL change.
2. It clears `_selected`, then `await BusyState.RunAsync(BusyKeys.Brands.ManageList, …)` dispatching
   `Mediator.Send(new GetBrandsPageQuery(state.Search, state.Status, state.Sort, pagination), ct)`.
3. `AuthorizationBehavior` checks `brands.view`; `GetBrandsPageQueryValidator` clamps page/page-size.
4. `GetBrandsPageHandler` → `IBrandRepository.GetPageAsync(...)` → `GetProductCountsAsync(pageIds)`
   → `Result.Ok(PagedResult<BrandListItemDto>)`.
5. Search/filter/sort handlers call `PushStateAsync(state with { … , Page = 1 })` — RULE-10's
   page reset lives in the state record, so it cannot be forgotten per control.
6. Empty result → `ShopFilterPanel`'s clear affordance + the no-match empty state (AC-18).

### Flow 2: Edit a brand (spec Behavior 4)

1. `EditBrand.razor` wraps its whole body in
   `<AuthorizeView Policy="@PolicyNames.Permission(PermissionCatalogue.Brands.Edit.Code)">` — this,
   not the query, is what denies a view-only admin's direct link (Decision 11, AC-16).
   `EditBrand.razor.cs` (`[Route(Routes.Admin.EditBrand)]`, `[Parameter] Guid Id`) sends
   `GetBrandByIdQuery(Id)` (`brands.view`); a not-found result renders the "no longer exists"
   message and returns to the list (AC-27).
2. Submit → `UpdateBrandCommand(Id, Name, Description, IsActive, NewLogo, RemoveLogo)`.
3. `UpdateBrandCommandValidator` → RULE-1/RULE-3/RULE-4 keys.
4. `UpdateBrandHandler`: load → `ExistsByNormalizedNameAsync(name, excludeBrandId: Id)` →
   `Fail(AlreadyExists)`; `brand.Rename(...)`, `.ChangeDescription(...)`, `.Activate()/.Deactivate()`
   inside `try { } catch (DomainException ex) { return Result.Fail(ex.MessageKey); }`. No slug is
   recomputed — nothing derived from the name exists (FR-9, AC-28).
5. Logo: capture `previousPath = brand.LogoPath`; on `RemoveLogo` → `brand.RemoveLogo()`; on new
   upload → `UploadAsync` then `AttachLogo`. After a successful `UpdateAsync`, best-effort
   `IFileStorage.DeleteAsync` on `previousPath` (RULE-11) — mirroring `CreateBrandHandler`'s
   compensation stance: an orphaned object never fails the request.
6. `Result.Ok(BrandDtoMapper.ToDto(...))` → snackbar `Strings.EditBrand_Success` → navigate to
   `Routes.Admin.ManageBrands`.

### Flow 3: Flip status inline or in bulk (spec Behaviors 9–10)

1. Activate → no prompt; `SetBrandStatusCommand([id], true)` immediately (RULE-14).
2. Deactivate → `ShopConfirmDialog` naming the brand (or the count, bulk) → on confirm,
   `SetBrandStatusCommand(ids, false)`.
3. `SetBrandStatusHandler` loads each brand, calls `Activate()`/`Deactivate()`, persists, returns
   `BrandStatusChangeDto(ChangedCount)`. The action applies unconditionally to every selected brand,
   but a brand already in the target status is a no-op and is **not** counted — deactivating a
   selection of 5 that already holds 2 inactive brands reports 3, matching what the list visibly
   changes.
4. Page re-applies current state (reload + selection cleared); snackbar reports the count. On
   failure the switch reverts and `Localizer[result.Error]` is shown (spec edge case).

### Flow 4: Delete one or many (spec Behaviors 5, 6, 11)

1. `ShopConfirmDialog` states permanence and names the brand or the count (RULE-7).
2. `DeleteBrandsCommand(ids)` → `[RequiresPermission("brands.delete")]`.
3. `DeleteBrandsHandler` → `IBrandRepository.DeleteManyAsync(ids, ct)` → the `delete_brands` RPC →
   `BrandDeletionOutcomeDto(DeletedCount, Blocked[])`. Logo objects for deleted brands are disposed
   best-effort via `IFileStorage.DeleteAsync` (RULE-11), using the paths the RPC returns.
4. UI (Decision 12): `DeletedCount > 0 && Blocked.Count == 0` → success snackbar.
   `Blocked.Count > 0` → `Brand_BulkDeletePartial` with **both counts only**, offering Deactivate
   instead (FR-22); the blocked ids stay selected and their rows render an in-use indicator with the
   product count, which is how AC-24's "named as still in use" is met. A single-brand delete refused
   this way uses `Brand_InUse`, which does name its one brand (FR-13).
   `DeletedCount == 0 && Blocked.Count == ids.Count` → the all-blocked message (AC-25).
5. Reload lands on the last valid page when the current one emptied (spec edge case).

## 7. Development Plan

### Step 1 — Domain (`shop-domain-implementer`)

**Depends on:** resolved plan.

- [ ] **TASK-001** — Remove the slug from `Brand`: delete the `Slug` property, `GenerateSlug`, the
  `[GeneratedRegex] NonAlphanumericRun()` member and the now-unneeded `partial` modifier; drop the
  `slug` parameter from `Rehydrate(...)` (Decision 6). Leaves two call sites broken until TASK-016.
- [ ] **TASK-002** — `Brand` mutations: `Name` → `private set`; add `Rename`, `ChangeDescription`,
  `Activate`, `Deactivate`, `RemoveLogo` (returns the previous path). Reuse the three existing brand
  exceptions; extract the shared trim/validate logic so `Create` and `Rename` cannot diverge.
- [ ] **TASK-003** — `BrandSortOption` and `BrandStatusFilter` enums in `Domain/Enums/`; add
  `FilterKind.SingleSelect = 2` (Decision 5).
- [ ] **TASK-004** — Update `tests/TheShop.Domain.Tests/Entities/BrandTests.cs`: drop every slug
  assertion; add rename re-validation (required / too long), description limit, activate/deactivate
  transitions, and `RemoveLogo` returning the prior path and clearing it. Leave `CategoryTests.cs`
  alone — `Category.Slug` stays.

**Completion gate:** Domain builds · no `Slug` member remains on `Brand` · every new method covered ·
no outer-layer type in Domain · public API (incl. the changed `Rehydrate` signature) reported for the
Application step.

### Step 2 — Application (`shop-application-implementer`)

**Depends on:** Step 1's reported Domain API.

- [ ] **TASK-005** — Move `FilterGroupDto`, `FilterOptionDto`, `AppliedFilterDto` to
  `Application/Common/Filtering/`; rename `PriceRangeDto` → `RangeFilterDto`; add
  `SortOptionDto(string Value, string LabelKey)`. Update every `Features/Products` reference.
  **Pure relocation — a namespace/name change and nothing else.** No member added, removed, or
  retyped; no behavior change (Decision 4, catalogue regression gate).
- [ ] **TASK-006** — Drop `Slug` from `BrandDto`; update `BrandDtoMapper`; delete
  `BrandErrorKeys.SlugConflict` and its `Strings.resx` / `Strings.fr.resx` entries. Add
  `BrandListItemDto`, `BrandDeletionOutcomeDto`, `BlockedBrandDto`, `BrandStatusChangeDto` under
  `Features/Brands/DTOs/`.
- [ ] **TASK-007** — `GetBrandsPageQuery(string? Search, BrandStatusFilter? Status,
  BrandSortOption Sort, PaginationRequest Pagination)` + handler + validator in
  `Features/Brands/Queries/GetBrandsPage/`; `[RequiresPermission("brands.view")]`; returns
  `Result<PagedResult<BrandListItemDto>>`.
- [ ] **TASK-008** — `GetBrandByIdQuery(Guid Id)` + handler in `Features/Brands/Queries/GetBrandById/`;
  `[RequiresPermission("brands.view")]` (Decision 11 — the edit *page* carries the `brands.edit`
  gate); returns `Result<BrandDto>` failing with `Brand_NotFound`.
- [ ] **TASK-009** — `UpdateBrandCommand` + handler + validator in
  `Features/Brands/Commands/UpdateBrand/`; `[RequiresPermission("brands.edit")]`. Logo
  replace/remove disposal per Flow 2 step 5.
- [ ] **TASK-010** — `SetBrandStatusCommand(IReadOnlyList<Guid>, bool)` + handler + validator in
  `Features/Brands/Commands/SetBrandStatus/`; `[RequiresPermission("brands.edit")]`; returns
  `Result<BrandStatusChangeDto>` whose `ChangedCount` excludes brands already in the target status
  (Flow 3 step 3).
- [ ] **TASK-011** — `DeleteBrandsCommand(IReadOnlyList<Guid>)` + handler + validator in
  `Features/Brands/Commands/DeleteBrands/`; `[RequiresPermission("brands.delete")]`; returns
  `Result<BrandDeletionOutcomeDto>`; validator rejects an empty id list.
- [ ] **TASK-012** — Extend `IBrandRepository`: `GetPageAsync`, `GetByIdAsync`, `UpdateAsync`,
  `DeleteManyAsync`, `GetProductCountsAsync`, and **`ExistsByNormalizedNameAsync(name,
  Guid? excludeBrandId, ct)`** (Decision 7 — update the existing `CreateBrandHandler` call site to
  pass `null`).
- [ ] **TASK-013** — New `BrandErrorKeys` entries + `Strings.resx` keys from Section 9, mirrored in
  `Strings.fr.resx`.
- [ ] **TASK-014** — Application tests: one handler test class per TASK-007…011, covering the
  partial-success matrix (all-deletable / mixed / all-blocked), self-name-exclusion (AC-9), logo
  disposal on remove and replace, and `ChangedCount` excluding already-in-target-status brands. Update
  `tests/TheShop.Application.Tests/Features/Brands/Mappers/BrandDtoMapperTests.cs` for the dropped
  `Slug`.

**Completion gate:** feature-folder convention followed · every Section 9 outcome translated · no
`SlugConflict` reference remains · `Features/Products` still compiles after TASK-005 · all
Infra/Web-consumed contracts compiling.

### Step 3 — Contract freeze

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `Brand.Rehydrate(...)` without `slug` | Domain | Infrastructure | Stable |
| `BrandDto` without `Slug` | Application | Web | Stable |
| `IBrandRepository` (6 new/changed members) | Application | Infrastructure | Stable |
| `GetBrandsPageQuery` / `Result<PagedResult<BrandListItemDto>>` | Application | Web | Stable |
| `UpdateBrandCommand` / `GetBrandByIdQuery` / `Result<BrandDto>` | Application | Web | Stable |
| `SetBrandStatusCommand` / `Result<BrandStatusChangeDto>` | Application | Web | Stable |
| `DeleteBrandsCommand` / `Result<BrandDeletionOutcomeDto>` | Application | Web | Stable |
| `Common/Filtering/*` DTO namespace + `FilterKind.SingleSelect` | Application | Web | Stable |
| `BrandSortOption` / `BrandStatusFilter` | Domain | Web, Infrastructure | Stable |

### Step 4 — Infrastructure (`shop-infra-implementer`) — runs in parallel with Step 5

**Depends on:** contract freeze (`IBrandRepository` + the `Rehydrate` signature stable).

- [ ] **TASK-015** — Migration `0014_manage_brands.sql` applied via Supabase MCP, exactly as in
  Section 10: `ALTER TABLE brands DROP COLUMN slug` (takes `brands_slug_key` with it), add
  `idx_brands_name_lower`, create `brand_product_counts` and `delete_brands`. No new RLS policies —
  all four `brands` policies already exist (verified against the live database).
- [ ] **TASK-016** — Drop `Slug` from `BrandRecord`; update `BrandMapper`; fix the
  `Brand.Rehydrate(...)` call in `ProductMapper.cs:34` (the products query embeds a brand). Verified
  clean: `get_catalogue_filters()` never projected the slug, and no product-side DTO or record
  carries it.
- [ ] **TASK-017** — Extend `SupabaseBrandRepository`: paged/filtered/sorted query over
  `BrandRecord` (status filter, `ILIKE` name search, `ORDER BY lower(name)`, `Range(from,to)` via
  the existing `PostgrestPaginationExtensions`, exact count for `TotalCount`); `GetByIdAsync`;
  `UpdateAsync`; the two RPC calls. **Delete** `SlugUniqueConstraint`, `IsSlugUniqueViolation`, and
  the `Brand_SlugConflict` catch.
- [ ] **TASK-018** — Failure translation: `23505` on `ux_brands_normalized_name` →
  `BrandErrorKeys.AlreadyExists`; `23503` from a raced product insert → the blocked-brand outcome
  rather than a throw; `insufficient_privilege` from either RPC → `RbacErrorKeys.AccessDenied`.
- [ ] **TASK-019** — Infrastructure tests: update `BrandMapperTests` and `ProductMapperTests` for the
  dropped `Slug`; **delete**
  `SupabaseProductRepositorySchemaTests.InsertBrand_WhenSlugAlreadyExists_ThrowsUniqueViolation` —
  it asserts a constraint that no longer exists (keep the `InsertCategory_…` sibling). Add a
  round-trip test for the new paged projection and the failure-code translation table.

**Completion gate:** migration applies cleanly · no `slug` reference remains in Infrastructure ·
both functions `SECURITY DEFINER` with their `authorize(...)` guard and pinned `search_path` ·
repository satisfies the frozen interface · no Infrastructure type leaks inward.

### Step 5 — Web (`shop-ui-implementer`) — runs in parallel with Step 4

**Depends on:** contract freeze (command/DTO shapes stable).

**Figma references**

- **File:** https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop
- **Nodes:**
  - `2534:5044` — **Manage Brands page** (verified). Full page frame: announcement bar, appbar,
    breadcrumb, body, footer — reuse the existing shell components; only the body is new.
  - `2534:5050` — Title row: "MANAGE BRANDS" heading + the Add Brand button (`2577:9653`), which
    renders only for `brands.create` (FR-6).
  - `2577:9540` — **Filters** container (the sidebar this feature generalizes into
    `ShopFilterPanel`); `2577:9541` — the Status Filter group (All/Active/Inactive, Decision 5).
  - `2577:9189` — **Brand Section**: the list region, containing `2577:9190` Brand Table (rows:
    logo, name, description, status, per-row actions, selection checkbox), `2577:9253` Pagination
    (map to the existing `ShopPagination`), and `2586:9801` **Bulk Action Bar** (FR-20/21/23).
  - `2534:4332` — **Empty state**, "no brands" (AC-18).
  - `2576:9035` — **Edit Brand** form (AC-6, AC-10, AC-11).

**Unverified-node rule.** `2534:4332` and `2576:9035` have never resolved — the Figma API
rate-limited on every attempt across three planning passes (both the REST path and the Desktop
Bridge). Their ids stay recorded and `shop-ui-implementer` re-fetches them at build time as usual.
**If either still fails to resolve, it is not a blocker:** build that surface from the spec's
acceptance criteria plus the verified patterns already in the repo — `AddBrand.razor` for the edit
form's layout, upload affordance and validation display, and the existing catalogue empty state for
AC-18 — and report in the implementation summary which node could not be reached, so design review
knows what to eyeball. `2534:5044` and its children *were* verified, including the Bulk Action Bar
and Status Filter.

- [ ] **TASK-020** — Extract `ShopFilterPanel` and `ShopSortSelect` into `Components/Common/`
  (`MudComponentBase`, `Class`/`Style` forwarded — Rules 23/24), add `SingleSelect` group rendering
  and the `Func<decimal,string>` range formatter, then refactor `ProductCatalogue.razor` and its
  sort control onto them (Decision 4). **Regression gate — this touches shipped, customer-facing
  UI:** the only additions permitted are the `SingleSelect` branch and the formatter parameter;
  rendered output for every existing product-catalogue case must be unchanged.
  `ProductFilterPanelTests`, `ProductCatalogueTests` and `CatalogueQueryStateTests` must pass with
  their **assertions untouched** — move/rename the files alongside the components, but an edited
  expectation counts as a failed gate, not a passing one. If an assertion genuinely must change,
  stop and raise it as a deviation (Section 7 deviation procedure) rather than editing it in place.
- [ ] **TASK-021** — `ShopConfirmDialog` in `Components/Common/`: resource-key title/body/confirm
  label, confirm `Color`, focus-on-open and focus-return (Decision 10, accessibility constraint).
- [ ] **TASK-022** — `BrandQueryState` record (`IUrlQueryState<BrandQueryState>`) +
  `BrandSortOptionSlug` in `Pages/Admin/`: search, status, sort, page; `Page = 1` reset folded into
  every mutator (RULE-10); defaults omitted from the URL. (Unrelated to `Brand.Slug` — these are
  stable URL tokens for sort orders, mirroring the existing `ProductSortOptionSlug`.)
- [ ] **TASK-023** — `ManageBrands.razor` / `.razor.cs` rebuilt on
  `QueryStatePageBase<BrandQueryState>`: `MudTable` rows per `2577:9190`, selection `HashSet<Guid>`
  cleared in `ApplyStateAsync` (Decision 9, RULE-12), page-scoped select-all, inline status
  `MudSwitch` with the deactivate confirm, per-row edit/delete gated by
  `AuthorizeView Policy="@PolicyNames.Permission(...)"`, bulk action bar shown only when the
  selection is non-empty and only with the actions the user holds (FR-23), empty/no-match states,
  and the **blocked-row in-use indicator** — after a partial bulk delete the blocked rows stay
  selected and show their `ProductCount`, which is how AC-24 names them (Decision 12).
- [ ] **TASK-024** — `EditBrand.razor` / `.razor.cs` per `2576:9035` (or the unverified-node rule
  above if it doesn't resolve): body wrapped in
  `<AuthorizeView Policy="@PolicyNames.Permission(PermissionCatalogue.Brands.Edit.Code)">` so a
  view-only admin's direct link is denied (Decision 11, AC-16); pre-filled form, `ShopImageUpload`
  for logo replace/remove, validation display, deleted-brand message, success → back to the list.
- [ ] **TASK-025** — `Routes.Admin.EditBrand = "/admin/brands/{id:guid}/edit"`, `BusyKeys.Brands`
  additions (`ManageList`, `EditBrand`, `BrandStatus`, `DeleteBrands`), and every resource string
  from Section 9 in `Strings.resx` + `Strings.fr.resx`.
- [ ] **TASK-026** — bUnit tests: `ManageBrandsTests` (permission-driven control visibility,
  selection clearing on state change, bulk bar gating, blocked-delete messaging),
  `EditBrandTests`, `ShopConfirmDialogTests`, `ShopFilterPanelTests` (single-select group).

**Completion gate:** matches the Figma nodes (or the unverified-node rule was applied and reported) ·
MudBlazor-only, no hardcoded strings or design tokens (Rules 2–5, 11, 14–19) · consumes only frozen
contracts · users lacking a permission see the control absent, not disabled · **product catalogue
unregressed: the three existing catalogue test classes pass with unedited assertions, and the
catalogue renders and filters identically to before TASK-020.**

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5 complete.

- [ ] **TASK-027** — Cross-layer verification: solution builds with zero `Brand.Slug` references,
  DI resolves, migration applied, and the primary + failure flows work end-to-end — including a
  brand referenced only by an *unpublished* product (the case Decision 2 exists for) and the AC-28
  rename that the dropped slug constraint used to reject. **Also load the live product catalogue and
  exercise its filters, range group and sort** — TASK-020 refactored shipped customer-facing UI, and
  this is the human check behind that regression gate.
- [ ] **TASK-028** — Run `/theshop.test manage-brands` → `/theshop.verify manage-brands` →
  `/theshop.review manage-brands` → `/theshop.document`.

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
| AC-1: list, 10/page, name A→Z, filter on All | TASK-007, TASK-017, TASK-022, TASK-023 |
| AC-2: next page keeps search/filter/sort | TASK-007, TASK-022, TASK-023 |
| AC-3: case-insensitive name search, resets to page 1 | TASK-007, TASK-017, TASK-022 |
| AC-4: status filter Inactive / All | TASK-003, TASK-007, TASK-017, TASK-020 |
| AC-5: reverse name sort keeps search+filter, page 1 | TASK-003, TASK-017, TASK-020, TASK-022 |
| AC-6: edit all four fields | TASK-002, TASK-009, TASK-024 |
| AC-7: empty/whitespace name rejected | TASK-002, TASK-009, TASK-013, TASK-024 |
| AC-8: rename to an existing name rejected, input preserved | TASK-012, TASK-018, TASK-009, TASK-024 |
| AC-9: no-op save succeeds (self-name excluded) | TASK-012, TASK-014, TASK-009 |
| AC-10: logo remove / replace, old object gone | TASK-002, TASK-009, TASK-014, TASK-024 |
| AC-11: bad logo type/size refused, existing logo intact | TASK-009, TASK-013, TASK-024 |
| AC-12: status Inactive hides from customers, Active restores | TASK-002, TASK-010, TASK-023 |
| AC-13: single delete confirm + permanence, logo discarded | TASK-011, TASK-015, TASK-021, TASK-023 |
| AC-14: cancelling the confirm changes nothing | TASK-021, TASK-023, TASK-026 |
| AC-15: in-use brand refused with product count (incl. discontinued) | TASK-011, TASK-015, TASK-014, TASK-023 |
| AC-16: view-only sees no add/edit/toggle/delete/bulk; direct link denied (page-level `brands.edit` gate — Decision 11) | TASK-007, TASK-023, TASK-024, TASK-026 |
| AC-17: no `brands.view` → access denied on the page | TASK-023, TASK-026 |
| AC-18: empty state and no-match state | TASK-007, TASK-023 |
| AC-19: EN/FR across list, filters, sort, form, dialogs, messages | TASK-013, TASK-025 |
| AC-20: keyboard + screen-reader across all controls and dialogs | TASK-021, TASK-023, TASK-024, TASK-026 |
| AC-21: inline activate immediate, deactivate confirms | TASK-010, TASK-021, TASK-023 |
| AC-22: page-scoped select-all; selection clears on state change | TASK-023, TASK-026 |
| AC-23: bulk deactivate confirms with count, applies to all | TASK-010, TASK-021, TASK-023 |
| AC-24: mixed bulk delete — 3 deleted, both counts in the message, 2 blocked rows stay selected and named by their in-use indicator (Decision 12) | TASK-011, TASK-015, TASK-014, TASK-023 |
| AC-25: all-blocked selection deletes nothing, explains | TASK-011, TASK-015, TASK-014, TASK-023 |
| AC-26: edit-but-not-delete sees no bulk Delete; direct attempt denied | TASK-011, TASK-023, TASK-026 |
| AC-27: edit link survives rename, explains after deletion | TASK-008, TASK-025, TASK-024 |
| AC-28: "Test Verify" + rename to "Test.Verify" both allowed (no derived-identifier clash) | TASK-001, TASK-015, TASK-009, TASK-019 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

- `UpdateBrandCommandValidator`: `Id` not empty; `Name` not empty after trim (RULE-1) →
  `Brand_NameRequired`; `Name` ≤ 100 and `Description` ≤ 250 (RULE-3) → `Brand_NameTooLong` /
  `Brand_DescriptionTooLong`; logo content type ∈ {png, jpeg, webp} and ≤ 2 MB (RULE-4) →
  `Brand_LogoInvalidType` / `Brand_LogoTooLarge`.
- `GetBrandsPageQueryValidator`: `Pagination.Page ≥ 1`; `PageSize` clamped to 10 via
  `PaginationRequest.Normalized(maxPageSize: 10)` (spec constraint — fixed page size).
- `SetBrandStatusCommandValidator` / `DeleteBrandsCommandValidator`: `BrandIds` non-empty, no
  `Guid.Empty`, de-duplicated.

### Domain exceptions

- `BrandNameRequiredException` — `Rename` with blank name. `MessageKey = Brand_NameRequired`.
- `BrandNameTooLongException` — > 100 chars. `MessageKey = Brand_NameTooLong`.
- `BrandDescriptionTooLongException` — > 250 chars. `MessageKey = Brand_DescriptionTooLong`.

(All three already exist and are reused — `Rename`/`ChangeDescription` share `Create`'s validation.)

### Result.Fail error keys

**Removed:** `Brand_SlugConflict` (and `BrandErrorKeys.SlugConflict`) — the constraint it reported is
gone (Decision 6).

**New entries in `Strings.resx`:**

| Key | English text |
|---|---|
| `Brand_NotFound` | "This brand no longer exists." |
| `Brand_UpdateFailed` | "The brand could not be updated. Please try again." |
| `Brand_DeleteFailed` | "The brand could not be deleted. Please try again." |
| `Brand_StatusChangeFailed` | "The brand's status could not be changed. Please try again." |
| `Brand_InUse` | "{0} is used by {1} product(s) and cannot be deleted. Make it Inactive instead." |
| `Brand_BulkDeletePartial` | "{0} brand(s) deleted. {1} kept because products still use them." |
| `Brand_BulkDeleteAllBlocked` | "None of the selected brands could be deleted — products still use them. Make them Inactive instead." |

Existing keys reused: `Brand_AlreadyExists`, `Brand_NameRequired`, `Brand_NameTooLong`,
`Brand_DescriptionTooLong`, `Brand_LogoInvalidType`, `Brand_LogoTooLarge`,
`RbacErrorKeys.AccessDenied`. UI-only strings (page title, column headers, filter/sort labels,
bulk-action labels, confirm dialog copy, empty states, success snackbars, and the blocked-row in-use
indicator from Decision 12) are added in TASK-025. All
keys mirrored in `Strings.fr.resx` (`[TODO]` acceptable on the first pass — the review step's
French-completeness gate catches stragglers).

### Edge cases → owner

| Spec edge case | Handled by |
|---|---|
| Brand already deleted by someone else | `Brand_NotFound` from TASK-008/009; list refresh |
| Last page emptied by a delete | `Paginator.LastAsync()` clamp after reload (TASK-023) |
| Bulk delete partly fails mid-way | RPC is one transaction — outcome is all-or-per-brand, never indeterminate (TASK-015) |
| Inline toggle fails | switch reverts, `Localizer[result.Error]` (TASK-023) |
| Selection + page/filter change | cleared in `ApplyStateAsync` (TASK-023, RULE-12) |
| Rename previously blocked by a slug clash | no longer possible — constraint dropped (TASK-001, TASK-015, AC-28) |
| Logo replace leaves an orphan | best-effort `IFileStorage.DeleteAsync` after commit (TASK-009) |

## 10. Database Schema & RLS Policies

### Schema

```sql
-- Retire the brand's name-derived identifier (spec FR-9, Decision 6). Dropping the column
-- drops the brands_slug_key UNIQUE index with it, so a rename can no longer be refused for a
-- slug clash (AC-28). Migration 0002 seeds brands via `WHERE slug = …`, but 0002 always runs
-- before this migration on a replay, so its seed stays valid.
ALTER TABLE brands DROP COLUMN IF EXISTS slug;

CREATE INDEX IF NOT EXISTS idx_brands_name_lower ON brands (lower(name));

-- Authoritative per-brand product counts. SECURITY DEFINER because products' only SELECT
-- policy is products_public_read USING (is_published = true) — an unpublished product must
-- still count against deletion (spec RULE-6), and no client role can see it.
CREATE OR REPLACE FUNCTION public.brand_product_counts(brand_ids UUID[])
RETURNS TABLE (brand_id UUID, product_count BIGINT)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('brands.view') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    SELECT b.id, count(p.id)
    FROM brands b
    LEFT JOIN products p ON p.brand_id = b.id
    WHERE b.id = ANY(brand_ids)
    GROUP BY b.id;
END;
$$;

-- Atomic partial-success deletion (spec RULE-13). Counting and deleting in one statement
-- closes the TOCTOU window a client-side guard would leave open.
CREATE OR REPLACE FUNCTION public.delete_brands(brand_ids UUID[])
RETURNS TABLE (id UUID, name TEXT, logo_path TEXT, product_count BIGINT, deleted BOOLEAN)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
BEGIN
    IF NOT public.authorize('brands.delete') THEN
        RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
    END IF;

    RETURN QUERY
    WITH candidates AS (
        SELECT b.id, b.name, b.logo_path,
               (SELECT count(*) FROM products p WHERE p.brand_id = b.id) AS product_count
        FROM brands b
        WHERE b.id = ANY(brand_ids)
    ),
    removed AS (
        DELETE FROM brands
        WHERE brands.id IN (SELECT c.id FROM candidates c WHERE c.product_count = 0)
        RETURNING brands.id
    )
    SELECT c.id, c.name, c.logo_path, c.product_count,
           EXISTS (SELECT 1 FROM removed r WHERE r.id = c.id)
    FROM candidates c;
END;
$$;

REVOKE ALL ON FUNCTION public.brand_product_counts(UUID[]) FROM PUBLIC, anon;
REVOKE ALL ON FUNCTION public.delete_brands(UUID[])        FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.brand_product_counts(UUID[]) TO authenticated;
GRANT EXECUTE ON FUNCTION public.delete_brands(UUID[])        TO authenticated;
```

### RLS policies

**No changes.** All four `brands` policies already exist and were verified against the live
database:

```sql
-- brands_read          SELECT  USING (is_active OR (SELECT authorize('brands.view')))
-- brands_admin_insert  INSERT  WITH CHECK ((SELECT authorize('brands.create')))
-- brands_admin_update  UPDATE  USING/CHECK ((SELECT authorize('brands.edit')))
-- brands_admin_delete  DELETE  USING ((SELECT authorize('brands.delete')))
```

The two functions above are `SECURITY DEFINER` and therefore bypass RLS by design; each re-checks
the caller's permission with `authorize(...)` as its first statement, so the permission boundary is
preserved (`authorize()` reads live role assignments, not JWT claims). `products.brand_id`'s
`NOT NULL REFERENCES brands(id)` with no cascade remains the last-resort guard behind RULE-6.

## 11. Open Questions, Risks & Assumptions

No open questions and no unratified assumptions remain — all nine items resolved into Sections 4–9.
Three risks are knowingly accepted and stay logged here:

- **⚠️ Risk — ✅ Accepted:** `ALTER TABLE brands DROP COLUMN slug` (TASK-015) is destructive and
  irreversible; the values are unrecoverable and reinstating slugs later means a fresh backfill.
  Accepted because nothing reads the value — verified across `src/`, the migrations, and
  `get_catalogue_filters()` — so no consumed data is lost, and slugs are regenerable from names.
- **⚠️ Risk — ✅ Accepted:** `Category.Slug` survives while `Brand.Slug` goes, leaving the codebase
  deliberately inconsistent. Accepted as scoped out by the spec; the slug concept is expected to be
  retired more broadly in later work. Owned by no TASK here — logged so review reads it as intent,
  not oversight.
- **⚠️ Risk — ✅ Accepted:** `delete_brands` returns `logo_path` so the handler can dispose the logo,
  but the row is already gone when storage deletion runs — a storage failure orphans the object with
  no row to retry from. Accepted as consistent with `CreateBrandHandler`'s compensation stance: an
  orphaned object never fails the request. Owned by TASK-011.

---
**Status:** Resolved · **Spec:** `.specs/manage-brands/spec.md` · **Created:** 2026-07-25 · **Resolved:** 2026-07-26

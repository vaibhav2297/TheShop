# Test report — manage-product

_Run: 2026-09-09 · commit `969e915` · verdict ✅ Ready for E2E — deferred proof remains_
_Snapshot of one run — regenerate with `/theshop-test-merged manage-product`._

## Tests written

| File | Feature-trait cases |
|---|---|
| `tests/TheShop.Domain.Tests/Enums/AdminProductSortOptionTests.cs` (new) | 8 |
| `tests/TheShop.Domain.Tests/Enums/ProductStatusFilterTests.cs` (new) | 3 |
| `tests/TheShop.Application.Tests/Features/Products/Mappers/AdminProductDtoMapperTests.cs` (existing, unchanged) | 9 |
| `tests/TheShop.Application.Tests/Features/Products/Queries/GetAdminProductsPageHandlerTests.cs` (existing, unchanged) | 7 |
| `tests/TheShop.Application.Tests/Features/Products/Queries/GetAdminProductsPageQueryValidatorTests.cs` (extended: +2 negative-PriceMax / inclusive-bounds cases) | 9 |
| `tests/TheShop.Application.Tests/Features/Products/Queries/GetAdminProductsPageQueryTests.cs` (existing, unchanged) | 1 |
| `tests/TheShop.Application.Tests/Features/Products/Queries/GetAdminProductFiltersHandlerTests.cs` (existing, unchanged) | 1 |
| `tests/TheShop.Application.Tests/Features/Products/Queries/GetAdminProductFiltersQueryTests.cs` (existing, unchanged) | 1 |
| `tests/TheShop.Application.Tests/Features/Products/Commands/DeleteProducts/DeleteProductsCommandTests.cs` (existing, unchanged) | 1 |
| `tests/TheShop.Application.Tests/Features/Products/Commands/DeleteProducts/DeleteProductsCommandValidatorTests.cs` (existing, unchanged) | 4 |
| `tests/TheShop.Application.Tests/Features/Products/Commands/DeleteProducts/DeleteProductsHandlerTests.cs` (existing, unchanged) | 9 |
| `tests/TheShop.Application.Tests/Features/Products/Commands/SetProductStatus/SetProductStatusCommandTests.cs` (existing, unchanged) | 1 |
| `tests/TheShop.Application.Tests/Features/Products/Commands/SetProductStatus/SetProductStatusCommandValidatorTests.cs` (existing, unchanged) | 4 |
| `tests/TheShop.Application.Tests/Features/Products/Commands/SetProductStatus/SetProductStatusHandlerTests.cs` (existing, unchanged) | 8 |
| `tests/TheShop.Infrastructure.Tests/Persistence/AdminProductMapperTests.cs` (new) | 7 |
| `tests/TheShop.Infrastructure.Tests/Persistence/ManageProductsSchemaTests.cs` (new) | 26 |
| `tests/TheShop.Web.Tests/Pages/Admin/ManageProductsTests.cs` (extended: +9 cases — select-all page-scope, singular variant wording, equal-price collapse, loading state, no-match state, clear-restores, last-page recovery, generic failure, Add-button navigation) | 45 |

Several Application/Web/Domain files already existed on disk (untracked), from prior partial implementation run. Read, verified against spec/plan, found correct. Reused as-is, not rewritten. `AuthorizeAsProductManager()` in `ManageProductsTests.cs` extended: added `products.create` grant. Previously missing; new Add-button test needed it; no prior test relied on its absence.

**Expected total (manifest):** 144 · **Feature classes:** 17

## Tests run

| Project | Discovered | Passed | Failed | Skipped |
|---|---|---|---|---|
| TheShop.Domain.Tests | 11 | 11 | 0 | 0 |
| TheShop.Application.Tests | 55 | 55 | 0 | 0 |
| TheShop.Infrastructure.Tests | 33 | 33 | 0 | 0 |
| TheShop.Web.Tests | 45 | 45 | 0 | 0 |
| **Total** | **144** | **144** | **0** | **0** |

**Reconciliation:** 144 discovered / 144 expected — exact match. **Pass rate:** 144/144 (100%).

## Failures and warnings

None remaining. Prior run found and fixed one confirmed production defect (not a test defect):

### Fixed — `delete_products` image-key disposal (RULE-8)

- **Was failing:** `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.DeleteProducts_ReturnsTheDeletedProductsImageKeysForDisposal` (Infrastructure, integration).
- **Root cause — confirmed via independent repro on bare Postgres 16, outside this suite.** `delete_products` computed `image_keys` by checking `NOT EXISTS (SELECT 1 FROM product_images i WHERE i.object_key = k)` *after* the `removed` CTE's `DELETE FROM products` had already cascade-removed those `product_images` rows via `ON DELETE CASCADE`. PostgreSQL gives no guarantee that a cascade side effect from one data-modifying CTE is visible to a sibling read later in the same statement — the check always read the pre-cascade snapshot, always found the row, always excluded the key. `image_keys` was therefore always empty, so `DeleteProductsHandler`'s `IFileStorage.DeleteAsync` disposal loop never ran for a real deletion.
- **Fix applied.** `candidates` now computes "exclusively owned" (`NOT EXISTS ... i2.product_id <> p.id`) against the live pre-delete rows, and the final `SELECT` only surfaces `image_keys` for a candidate actually removed. Applied to `supabase/migrations/0028_manage_products.sql`, mirrored in `ManageProductsSchemaTests.cs`'s reproduced schema, and applied live to `TheShop-Dev` (migration `fix_delete_products_image_keys_cascade_visibility`). `get_advisors` shows no new security finding.
- **Verified:** the four `delete_products` tests pass, including the previously-failing one; full manage-product Infrastructure suite reruns 33/33.

No skipped tests, vacuous assertions, improper async signatures, timing sleeps, swallowed exceptions, nondeterministic time, or placeholder names found across manifest-listed files.

## Acceptance criteria

| AC | Status |
|---|---|
| AC-1 | Passed |
| AC-2 | Passed |
| AC-3 | Passed |
| AC-4 | Passed |
| AC-5 | Passed |
| AC-6 | Passed |
| AC-7 | Passed |
| AC-8 | Deferred — E2E |
| AC-9 | Deferred — E2E |
| AC-10 | Passed |
| AC-11 | Passed |
| AC-12 | Deferred — E2E |
| AC-13 | Passed |
| AC-14 | Passed |
| AC-15 | Passed |
| AC-16 | Passed |
| AC-17 | Passed |
| AC-18 | Passed |
| AC-19 | Deferred — manual |
| AC-20 | Deferred — manual |
| AC-21 | Passed |
| AC-22 | Passed |
| AC-23 | Passed |
| AC-24 | Passed |
| AC-25 | Passed |
| AC-26 | Passed |
| AC-27 | Passed |
| AC-28 | Passed |
| AC-29 | Passed |

**AC status:** 24 Passed · 5 Deferred (AC-8, AC-9, AC-12 — E2E; AC-19, AC-20 — manual/E2E) · 0 Failed · 0 Not Covered.

## Verdict

**✅ Ready for E2E — deferred proof remains**

Every discovered test passes (144/144), no skips or warnings, and every AC is Passed or validly Deferred. AC-8, AC-9, and AC-12 defer completion/cross-feature proof to `/theshop-e2e`; AC-19 and AC-20 defer locale and keyboard-accessibility proof to a manual/E2E pass. Their supporting unit tests are green. Next step: `/theshop-e2e manage-product`.

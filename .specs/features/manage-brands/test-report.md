# Test report — manage-brands

_Run: 2026-07-31 · commit `8c30462` · verdict ✅ Ready_
_Snapshot of one run — regenerate with `/theshop.test manage-brands`._

## Tests written (shop-test-writer)

- `tests/TheShop.Domain.Tests/Entities/BrandTests.cs` — +24 tests (Rename/ChangeDescription/Activate/Deactivate/RemoveLogo)
- `tests/TheShop.Application.Tests/Features/Brands/**` — 88 tests across `GetBrandsPage`, `GetBrandById`, `UpdateBrand`, `SetBrandStatus`, `DeleteBrands`
- `tests/TheShop.Infrastructure.Tests/Persistence/ManageBrandsSchemaTests.cs` — 21 tests (Testcontainers suite: RPCs, dropped slug, RLS)
- `tests/TheShop.Web.Tests/Pages/Admin/EditBrandTests.cs` — 20 tests
- `tests/TheShop.Web.Tests/Components/Common/ShopConfirmDialogTests.cs` — 5 tests
- `tests/TheShop.Web.Tests/Pages/Admin/ManageBrandsTests.cs` — +28 tests (total 40)
- `tests/TheShop.Web.Tests/Components/Common/ShopFilterPanelTests.cs` — +6 tests
- `tests/TheShop.Web.Tests/Resources/BrandLocalizationTests.cs` — +45 tests (EN/FR resx coverage)

**Coverage by category:** Happy path: ✅ · Validation: ✅ · Edge cases: ✅ · Auth guard: ✅

## Tests run (shop-test-runner)

| Metric | Value |
|---|---|
| Expected (manifest) | 273 |
| Discovered/run | 273 |
| Reconciliation | ✅ matched |
| Passed | 273 |
| Failed | 0 |
| Skipped | 0 |
| Pass rate | 100% |
| Runner status | 🟢 Green |

No failures. No warnings flagged.

**Fixes applied since the previous run** (see prior `test-report.md` for the original failure set):
- `ShopConfirmDialogTests.cs` — rewritten to render `ShopConfirmDialog` through a real `MudDialogProvider` + `IDialogService` (matching `ManageBrands.razor.cs`'s own usage and MudBlazor's own test pattern), because MudBlazor 9.7.0's `MudDialog` resolves its container via an internal `IMudDialogInstanceInternal` cascading parameter that a hand-cascaded `IMudDialogInstance` substitute can no longer satisfy.
- `ManageBrandsTests.cs` — the two in-flight-loading assertions were corrected to match the actual, deliberately documented UX (`MutationBusyKeys` doc comment in `ManageBrands.razor.cs`): the table's own `Loading` is intentionally unused for row-scoped mutations, and only one spinner (the acted-on row's) exists — there is no separate bulk-bar spinner.
- `ManageBrandsSchemaTests.cs` — `InitializeAsync` reordered so `CreateRlsTestRolesAsync` (creates the `anon`/`authenticated` roles) runs before `ApplyManageBrandsSchemaAsync`, whose verbatim migration SQL revokes from `anon` — that role must exist first (a real Supabase instance always has it; the Testcontainers fixture didn't).
- `ManageBrandsSchemaTests.cs` — the two "lacks permission" RLS tests for `UPDATE`/`DELETE` were corrected: a `USING`-only RLS denial doesn't throw an exception (that's only true of a `WITH CHECK` failure on `INSERT`) — it silently matches zero rows. Renamed `UpdateBrand_WhenCallerLacksBrandsEditPermission_ThrowsRowLevelSecurityViolation` → `..._TheUpdateAffectsNoRows` and `DeleteBrandRow_WhenCallerLacksBrandsDeletePermission_ThrowsRowLevelSecurityViolation` → `..._TheDeleteAffectsNoRows`, asserting on the affected-row count instead of an expected exception.

No production code was changed — every fix was in the test fixtures.

## Acceptance criteria

| AC | Status |
|---|---|
| AC-1 | ✅ Passed |
| AC-2 | ✅ Passed |
| AC-3 | ✅ Passed |
| AC-4 | ✅ Passed |
| AC-5 | ✅ Passed |
| AC-6 | ✅ Passed |
| AC-7 | ✅ Passed |
| AC-8 | ✅ Passed |
| AC-9 | ✅ Passed |
| AC-10 | ✅ Passed |
| AC-11 | ✅ Passed |
| AC-12 | ✅ Passed |
| AC-13 | ✅ Passed |
| AC-14 | ✅ Passed |
| AC-15 | ✅ Passed |
| AC-16 | ✅ Passed |
| AC-17 | ✅ Passed |
| AC-18 | ✅ Passed |
| AC-19 | ✅ Passed |
| AC-20 | ✅ Passed |
| AC-21 | ✅ Passed |
| AC-22 | ✅ Passed |
| AC-23 | ✅ Passed |
| AC-24 | ✅ Passed |
| AC-25 | ✅ Passed |
| AC-26 | ✅ Passed |
| AC-27 | ✅ Passed |
| AC-28 | ✅ Passed |

**AC status:** 28 passed · 0 failed · 0 not covered

## Verdict

**✅ Ready for code review — all tests pass and all acceptance criteria pass**

All 273 tests across all four layers pass, reconciled exactly against the manifest, with every one of the 28 acceptance criteria verified. Next: `/theshop.verify manage-brands`.

---

*Full per-failure breakdown and recommendations are in the shop-test-runner output above. The user should refer to that for fix guidance.*

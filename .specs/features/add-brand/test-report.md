# Test report — add-brand

_Run: 2026-07-23 · commit `05c0f88` (+ uncommitted fix) · verdict ✅ Ready_
_Snapshot of one run — regenerate with `/theshop.test add-brand`._

**Update:** The `ShopImageUpload` `MaxFileSize` double-validation bug (see Failure 1 below) has been fixed — `MaxFileSize` is no longer passed to the inner `MudFileUpload` (it silently swallowed oversized files before the component's own `FilesChanged`/size-guard could run). `ShopImageUpload.razor.cs`'s manual size check is now the sole validator, consistent with how content-type is already validated. Re-run: **131/131 passed**, all 10 ACs ✅.

## Tests written (shop-test-writer)

- `tests/TheShop.Domain.Tests/Entities/BrandTests.cs` — 19 tests
- `tests/TheShop.Domain.Tests/ValueObjects/PermissionCatalogueTests.cs` — 3 tests
- `tests/TheShop.Application.Tests/Features/Brands/Commands/CreateBrand/CreateBrandCommandValidatorTests.cs` — 19 tests
- `tests/TheShop.Application.Tests/Features/Brands/Commands/CreateBrand/CreateBrandHandlerTests.cs` — 19 tests
- `tests/TheShop.Application.Tests/Features/Brands/Commands/CreateBrand/CreateBrandCommandTests.cs` — 1 test
- `tests/TheShop.Application.Tests/Features/Brands/Mappers/BrandDtoMapperTests.cs` — 4 tests
- `tests/TheShop.Infrastructure.Tests/Persistence/BrandMapperTests.cs` — 5 tests
- `tests/TheShop.Infrastructure.Tests/Persistence/SupabaseBrandRepositorySchemaTests.cs` — 16 tests
- `tests/TheShop.Web.Tests/Components/Common/ShopImageUploadTests.cs` — 6 tests
- `tests/TheShop.Web.Tests/Pages/Admin/AddBrandTests.cs` — 14 tests
- `tests/TheShop.Web.Tests/Resources/BrandLocalizationTests.cs` — 25 tests

**Coverage by category:** Happy path: ✅ · Validation: ✅ · Edge cases: ✅ · Auth guard: ✅

## Tests run (shop-test-runner)

| Metric | Value |
|---|---|
| Expected (manifest) | 131 |
| Discovered/run | 131 |
| Reconciliation | ✅ matched |
| Passed | 131 |
| Failed | 0 |
| Skipped | 0 |
| Pass rate | 100% |
| Runner status | 🟢 Green |

No failures. Fixed: `ShopImageUploadTests.SelectFile_ExceedingTheMaxFileSize_DoesNotAcceptItAndShowsTheSizeError` now passes after removing `MaxFileSize="@MaxFileSize"` from `src/TheShop.Web/Components/Common/ShopImageUpload.razor`'s `MudFileUpload`.

No warnings flagged.

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

**AC status:** 10 passed · 0 failed · 0 not covered

## Verdict

**✅ Ready for code review — all tests pass and all acceptance criteria pass**

131/131 tests pass, the discovered count reconciled exactly against the manifest, and all 10 acceptance criteria are ✅ Passed.

---

*Full per-failure breakdown and recommendations are in the shop-test-runner output above. The user should refer to that for fix guidance.*

# Test report — admin-console

_Run: 2026-07-25 · commit `125ea35` · verdict ✅ Ready_
_Snapshot of one run — regenerate with `/theshop.test admin-console`._

## Tests written (shop-test-writer)

- `tests/TheShop.Application.Tests/Features/Admin/Queries/GetAdminDashboard/GetAdminDashboardHandlerTests.cs` — 7 tests
- `tests/TheShop.Application.Tests/Features/Admin/AdminDashboardCatalogueTests.cs` — 7 tests
- `tests/TheShop.Web.Tests/Components/Admin/AdminModuleCardTests.cs` — 16 tests
- `tests/TheShop.Web.Tests/Pages/Admin/AdminConsoleTests.cs` — 7 tests
- `tests/TheShop.Web.Tests/Components/Common/ProfileMenuTests.cs` — 2 tests
- `tests/TheShop.Web.Tests/Resources/AdminConsoleLocalizationTests.cs` — 13 tests
- `tests/TheShop.Infrastructure.Tests/Persistence/SupabaseAdminDashboardRepositorySchemaTests.cs` — 13 tests

**Coverage by category:** Happy path: ✅ · Validation: N/A (query carries no input fields) · Edge cases: ✅ · Auth guard: ✅

## Tests run (shop-test-runner)

| Metric | Value |
|---|---|
| Expected (manifest) | 65 |
| Discovered/run | 65 |
| Reconciliation | ✅ matched |
| Passed | 65 |
| Failed | 0 |
| Skipped | 0 |
| Pass rate | 100% |
| Runner status | 🟢 Green |

No failures.

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

**AC status:** 7 passed · 0 failed · 0 not covered

## Verdict

**✅ Ready for code review — all tests pass and all acceptance criteria pass**

All 65 manifest-promised tests ran and passed with exact reconciliation, and every acceptance criterion (AC-1 through AC-7) is ✅ Passed with no skips or warnings.

---

*Full per-failure breakdown and recommendations are in the shop-test-runner output above. The user should refer to that for fix guidance.*

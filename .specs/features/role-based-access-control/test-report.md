# Test report — role-based-access-control

_Run: 2026-07-15 · commit `15ffd86` · verdict ✅ Ready_
_Snapshot of one run — regenerate with `/theshop.test role-based-access-control`._

## Tests written (shop-test-writer)

- `tests/TheShop.Domain.Tests/ValueObjects/PermissionTests.cs` — 19 tests
- `tests/TheShop.Domain.Tests/ValueObjects/PermissionCatalogueTests.cs` — 17 tests
- `tests/TheShop.Domain.Tests/Entities/RoleTests.cs` — 12 tests
- `tests/TheShop.Domain.Tests/Entities/UserAccessTests.cs` — 10 tests
- `tests/TheShop.Application.Tests/Common/Behaviors/AuthorizationBehaviorTests.cs` — 7 tests
- `tests/TheShop.Application.Tests/Features/Roles/Queries/GetMyPermissionsHandlerTests.cs` — 3 tests
- `tests/TheShop.Infrastructure.Tests/Persistence/RbacAuthorizationTests.cs` — 24 tests
- `tests/TheShop.Infrastructure.Tests/Persistence/RbacPolicyRegressionTests.cs` — 11 tests
- `tests/TheShop.Web.Tests/Auth/PermissionAuthorizationHandlerTests.cs` — 4 tests
- `tests/TheShop.Web.Tests/Auth/AdminAreaAuthorizationHandlerTests.cs` — 3 tests
- `tests/TheShop.Web.Tests/State/PermissionStateTests.cs` — 8 tests
- `tests/TheShop.Web.Tests/Components/Common/AccessDeniedViewTests.cs` — 4 tests
- `tests/TheShop.Web.Tests/Pages/Admin/ManageProductsTests.cs` — 4 tests
- `tests/TheShop.Web.Tests/Resources/RbacLocalizationTests.cs` — 55 tests

**Coverage by category:** Happy path: ✅ · Validation: ✅ · Edge cases: ✅ · Auth guard: ✅

## Tests run (shop-test-runner)

| Metric | Value |
|---|---|
| Expected (manifest) | 181 |
| Discovered/run | 181 (58 Domain + 10 Application + 78 Web + 35 Infrastructure) |
| Reconciliation | ✅ matched |
| Passed | 181 |
| Failed | 0 |
| Skipped | 0 |
| Pass rate | 100% |
| Runner status | 🟢 Green |

No failures.

No warnings flagged.

**Note on this run:** the prior run flagged 2 failures in `AccessDeniedViewTests` (AC-10) with an `InvalidOperationException` — bUnit could not resolve `PermissionState` for `AccessDeniedView`'s `[Inject]` property. Root cause was a **test bug**, not a production defect: `AccessDeniedView` correctly injects and reads `PermissionState.HasAnyAdminPermission`, but `AccessDeniedViewTests.cs` and `ManageProductsTests.cs` only configured bUnit's `AddAuthorization()`/`SetPolicies()` auth-context, which doesn't back `PermissionState` at all (that store is hydrated from `IMediator`, independent of the ASP.NET Core authorization policy pipeline). Fixed by registering a real `PermissionState` instance (backed by a substituted `IMediator` returning canned `GetMyPermissionsQuery` results, then `HydrateAsync()`'d) into each test's `Services` collection before render — 4 tests updated in `AccessDeniedViewTests.cs`, 2 in `ManageProductsTests.cs`.

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

**AC status:** 11 passed · 0 failed · 0 not covered

## Verdict

**✅ Ready for code review — all tests pass and all acceptance criteria pass**

All 181 tests reconcile exactly against the manifest, all pass, and all 11 acceptance criteria pass — including AC-10, once the test-side `PermissionState` DI gap was fixed.

---

*Full per-failure breakdown and recommendations are in the shop-test-runner output above. The user should refer to that for fix guidance.*

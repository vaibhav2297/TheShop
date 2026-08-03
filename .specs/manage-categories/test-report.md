# Test report — manage-categories

_Run: 2026-08-02 · commit `4243dd8` · verdict ✅ Ready_
_Snapshot of one run — regenerate with `/theshop.test-merged manage-categories`._

## Tests written

Nearly the entire feature-trait inventory already existed from the implementation pass (constitution
Rule 29 — every new handler/repository/value object/domain method ships with a test). This run
audited that inventory against the spec, closed two coverage gaps, and produced the E2E journey and
manifest.

- `tests/TheShop.Domain.Tests/Entities/CategoryTests.cs` — 43 manage-categories cases (existing; file also carries 1 unrelated `product-catalogue`-trait case)
- `tests/TheShop.Application.Tests/Features/Categories/**` (18 classes) — 133 cases (existing)
- `tests/TheShop.Infrastructure.Tests/Persistence/CategoryMapperTests.cs` — 5 cases (existing)
- `tests/TheShop.Infrastructure.Tests/Persistence/ManageCategoriesSchemaTests.cs` — 22 cases (existing)
- `tests/TheShop.Infrastructure.Tests/Persistence/SupabaseCategoryRepositorySchemaTests.cs` — 14 cases (existing)
- `tests/TheShop.Infrastructure.Tests/Persistence/ProductMapperTests.cs` — 1 manage-categories case (existing, extended cross-feature file)
- `tests/TheShop.Infrastructure.Tests/Persistence/SupabaseBrandRepositorySchemaTests.cs` — 2 manage-categories cases (existing, extended cross-feature file for plan Decision 13)
- `tests/TheShop.Web.Tests/Common/Sorting/CategorySortCatalogueTests.cs` — 12 cases (existing; 2 own + 10 inherited from the shared `SortCatalogueTests<TSort>` base)
- `tests/TheShop.Web.Tests/Pages/Admin/AddCategoryTests.cs` — 13 cases (existing)
- `tests/TheShop.Web.Tests/Pages/Admin/EditCategoryTests.cs` — 19 cases (existing)
- `tests/TheShop.Web.Tests/Pages/Admin/ManageCategoriesTests.cs` — 38 cases (**+1 new**: `Render_WhenSearchMatchesNoCategories_ShowsTheNoMatchMessage`, closing the AC-22 no-match-state gap)
- `tests/TheShop.Web.Tests/Resources/CategoryLocalizationTests.cs` — 72 cases (existing, theory-driven over the full EN/FR key set)

AC-26 (accessibility) was previously unmapped despite coverage existing — `AddCategoryTests.cs`'s
`Render_WithASelectedImage_RemoveButtonHasALocalizedAriaLabel` now carries that mapping, mirroring
manage-brands' identical bUnit-reach caveat.

**E2E (unmanifested):**
- `tests/TheShop.E2E.Tests/Journeys/ManageCategoriesJourneyTests.cs` — new, 2 journeys (`AC6_Admin_creates_a_category_and_sees_it_listed`, `AC8_Admin_edits_a_category_and_it_persists`)
- `tests/TheShop.E2E.Tests/Pages/Admin/ManageCategoriesPage.cs`, `AddCategoryPage.cs`, `EditCategoryPage.cs` — new page objects, mirroring the shipped Brand page objects

## Tests run

| Metric | Value |
|---|---|
| Expected (manifest `totalTests`) | 374 |
| Discovered (sum of 4 project runs) | 374 |
| Reconciliation | ✅ exact match |
| Passed | 374 |
| Failed | 0 |
| Skipped | 0 |
| Pass rate | 100% |
| Status | ✅ all green |

Per-project breakdown (`dotnet test <project> --filter "Feature=manage-categories" --no-build`):

| Project | Discovered | Passed | Failed |
|---|---|---|---|
| TheShop.Domain.Tests | 43 | 43 | 0 |
| TheShop.Application.Tests | 133 | 133 | 0 |
| TheShop.Infrastructure.Tests | 44 | 44 | 0 |
| TheShop.Web.Tests | 154 | 154 | 0 |

The Infrastructure project's Testcontainers-backed classes (`ManageCategoriesSchemaTests`,
`SupabaseCategoryRepositorySchemaTests`, the two `manage-categories`-tagged cases in
`SupabaseBrandRepositorySchemaTests`) failed on the first attempt with
`DotNet.Testcontainers.Builders.DockerUnavailableException` — Docker Desktop's daemon was still
starting up in this environment. A second run, after the daemon finished starting, passed all 44
Infrastructure cases cleanly; this is recorded as an environment timing issue, not a code defect.

E2E journeys were not run (out of scope for this command) but their project was built successfully:
`dotnet build tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj` — 0 errors.

## Failures and warnings

None. No skips, vacuous assertions, improper async signatures, timing sleeps, swallowed exceptions
(the two `try { mutate() } catch (DomainException) { }` blocks in `CategoryTests.cs` are deliberate —
each asserts the entity's state is unchanged after a rejected mutation, not an ignored failure), or
placeholder test names found in the manifest-listed files.

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
| AC-8 | Passed |
| AC-9 | Passed |
| AC-10 | Passed |
| AC-11 | Passed |
| AC-12 | Passed |
| AC-13 | Passed |
| AC-14 | Passed |
| AC-15 | Passed |
| AC-16 | Passed |
| AC-17 | Passed |
| AC-18 | Passed |
| AC-19 | Passed |
| AC-20 | Passed |
| AC-21 | Passed |
| AC-22 | Passed |
| AC-23 | Passed |
| AC-24 | Passed |
| AC-25 | Passed |
| AC-26 | Passed |
| AC-27 | Passed |
| AC-28 | Passed |
| AC-29 | Passed |
| AC-30 | Passed |
| AC-31 | Passed |
| AC-32 | Passed |

**AC status:** 32 Passed · 0 Failed · 0 Not Covered

## Verdict

**✅ Ready**

Exact reconciliation (374/374), every discovered case passed, no skips or warnings, and all 32 spec
ACs map to at least one passing test. Next step: `/theshop.verify manage-categories`.

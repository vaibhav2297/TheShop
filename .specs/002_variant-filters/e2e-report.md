# Variant Filters — E2E Report

**Feature:** `002_variant-filters`
**Date:** 2026-10-22
**Commit:** `7846b5f1a372492d6f1043f6d2f5cda3afcac614` plus current feature changes.
**Verdict:** ✅ VERIFIED

## Results

Buckets: **19 E2E / 0 unit / 0 manual**. Every spec AC classified exactly once, in spec order.

Latest result per unique journey: **19 expected / 19 discovered / 19 passed / 0 failed / 0 skipped**. Three repair rounds. Passing evidence combines filtered runs; no final full-suite rerun.

| Run | Selected/discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Initial | 19/19 | 0 | 19 | 0 |
| Repair 1 | 19/19 | 14 | 5 | 0 |
| Repair 2 | 5/5 | 4 | 1 | 0 |
| Repair 3 | 1/1 | 1 | 0 | 0 |

44 case executions; 19 unique journeys. After initial selection, only failing methods rerun. All latest outcomes pass. Existing Test row remains Passing (142/142 lower-level cases).

## Static evidence

- Unit manifest gate: pass.
- E2E gate: pass initially and after every test edit. 19 unrelated hook warnings remain outside feature scope.
- E2E project build: pass after every edit; 4 existing analyzer warnings, 0 errors.
- Visual gate: pass. No supplied Figma URL; normal behavioral verdict applies.
- `git diff --check`: pass.
- Knowledge graph refreshed: 10,596 nodes, 26,608 edges, 555 communities. Latest edit changes no topology. SQL extraction unavailable (`tree_sitter_sql` missing); real local RPCs exercised below.

## Commands

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 manifest -Feature 002_variant-filters
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 e2e -Feature 002_variant-filters
dotnet build tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --nologo --no-restore -m:1 -p:UseSharedCompilation=false -v:q
pwsh -NoProfile -ExecutionPolicy Bypass -File tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1
$env:E2E_HEADLESS = "1"
dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --filter "Category=E2E&Feature=002_variant-filters" --logger "console;verbosity=normal" --nologo --no-build
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 visual -Feature 002_variant-filters
```

Repair 2 adds an OR filter containing exactly AC-8, AC-10, AC-13, AC-14, and AC-15 FQNs. Repair 3 adds exactly `FullyQualifiedName=TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC10_ChangingVariantsResetsPageAndSelectionWhilePagingRetainsCriteria`. Category/Feature filters retained.

## AC evidence

Full journey files, FQNs, and counts: [e2e-manifest.json](e2e-manifest.json).

| AC | Bucket | Latest result | Journey evidence | Passing run |
|---|---|---|---|---:|
| AC-1 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC1_BothListingsShowConfiguredGroupsWithoutDuplicateChoices` | Repair 1 |
| AC-2 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC2_LaterPageChoicesRemainAvailableAfterSearchAndFiltering` | Repair 1 |
| AC-3 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC3_SavedConfigurationAndFinalRemovalRefreshBothListings` | Repair 1 |
| AC-4 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC4_MultipleChoicesMatchEitherValueAndRemainSelected` | Repair 1 |
| AC-5 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC5_OneVariantMustSatisfyAllSelectedGroups` | Repair 1 |
| AC-6 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC6_MultipleMatchingVariantsDoNotDuplicateProductsAndClearRestoresPlainProduct` | Repair 1 |
| AC-7 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC7_PublicUsesActiveVariantsWhileManagementIncludesInactiveAndExcludesDeleted` | Repair 1 |
| AC-8 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC8_PublicBrandCategoryPriceAndSortCombineWithVariantRestriction` | Repair 2 |
| AC-9 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC9_ManagementCriteriaPreserveFullPriceRangeAndVariantCount` | Repair 1 |
| AC-10 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC10_ChangingVariantsResetsPageAndSelectionWhilePagingRetainsCriteria` | Repair 3 |
| AC-11 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC11_RemovingOneChoiceAndFinalChoicePreservesOtherSelections` | Repair 1 |
| AC-12 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC12_NoMatchKeepsClearActionAndClearRestoresResults` | Repair 1 |
| AC-13 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC13_RefreshSharedLinkAndHistoryRestoreCriteriaOnBothListings` | Repair 2 |
| AC-14 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC14_NoEligibleVariantsLeavesFixedControlsUsableOnBothListings` | Repair 2 |
| AC-15 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterCustomerJourneyTests.AC15_UnpublishedChoicesStayHiddenAndCustomerCannotOpenManagement` | Repair 2 |
| AC-16 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC16_OutdatedLinkExplainsRemovedChoiceAndPreservesValidCriteria` | Repair 1 |
| AC-17 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC17_MetadataAndResultFailuresShowRetryAndRecoverOnBothListings` | Repair 1 |
| AC-18 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC18_DesktopAndMobileKeyboardControlsHaveNamesSelectionAndVisibleFocus` | Repair 1 |
| AC-19 | e2e | Pass | `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC19_InactiveOnlyMatchIsExcludedPubliclyAndIncludedForStaff` | Repair 1 |

Real browser evidence covers dynamic groups, all-page choices, saved configuration, OR/AND matching on one variant, deduplication, inactive/public separation, fixed criteria/prices, page/row-selection reset, removal/clearing, refresh/shared links/Back/Forward, empty groups, guest/customer/staff visibility, stale-link notices, recoverable metadata/result failures, native keyboard focus, and mobile/desktop selection parity.

## Findings and repairs

No unresolved product findings.

### Local fixture shape — test defect

Affected FQNs: all 19 listed above; shared class `VariantFilterData`.
Symptom: `PGRST204: Could not find the 'slug' column of 'brands' in the schema cache`.
Cause confirmed: migrations 0015/0020 remove brand/category slug columns. Removed obsolete seed payload fields.
Trace example: [AC-4 initial setup trace](../../tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC4_MultipleChoicesMatchEitherValueAndRemainSelected.zip). Trace records context creation before setup failure; no listing navigation. This retained trace belongs to initial failed attempt.

### Existing variant price label — test defect

FQN: `TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC8_PublicBrandCategoryPriceAndSortCombineWithVariantRestriction`.
Symptom: expected `$10.00`, actual `From $10.00`.
Confirmed against existing `ProductCard.PriceLabel`. Assert exact `Strings.ManageProducts_PriceFrom` output.
Trace: [AC-8 price assertion](../../tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.VariantFilterPublicJourneyTests.AC8_PublicBrandCategoryPriceAndSortCombineWithVariantRestriction.zip). Retained failed-attempt trace; latest AC-8 result passes.

### Cached persona sessions after reset — environment defect

Affected FQNs:

- `TheShop.E2E.Tests.Journeys.VariantFilterCustomerJourneyTests.AC15_UnpublishedChoicesStayHiddenAndCustomerCannotOpenManagement`
- `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC14_NoEligibleVariantsLeavesFixedControlsUsableOnBothListings`
- `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC10_ChangingVariantsResetsPageAndSelectionWhilePagingRetainsCriteria`
- `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC13_RefreshSharedLinkAndHistoryRestoreCriteriaOnBothListings`

Class: environment cache used by existing `AuthStateFactory`.
Symptom: Login shown instead of management/access-denied content. Stored persona state becomes 27-byte empty state.
Cause: local database reset invalidates cached sessions; existing factory reuses any existing state file.
Repair: clear only generated admin/customer cache files; existing OTP harness creates fresh sessions. Auth/Fixtures/tools source unchanged.
Trace example: [AC-15 Login snapshot](../../tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.VariantFilterCustomerJourneyTests.AC15_UnpublishedChoicesStayHiddenAndCustomerCannotOpenManagement.zip). Fresh-session reruns prove correct customer denial and staff access.

### Default sort omitted from URL — test defect

FQN: `TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC10_ChangingVariantsResetsPageAndSelectionWhilePagingRetainsCriteria`.
Symptom: expected `sort=name-asc`; URL omits default sort.
Confirmed existing query-state serialization contract. Start with explicit `name-desc`; verify it survives variant changes and paging. Page reset, row clearing, and selected value assertions retained.
Trace: [AC-10 URL assertion](../../tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.VariantFilterStaffJourneyTests.AC10_ChangingVariantsResetsPageAndSelectionWhilePagingRetainsCriteria.zip). Latest rerun passes.

## Environment and teardown

- Authorized script starts/resets local Supabase, applies migrations 0001–0033, and seeds existing personas.
- Chromium runs headless through existing fixtures; app launch/stop remains fixture-owned.
- Unique `e2evf-*` rows; service-role setup restricted to loopback API. Browser requests exercise actual local RLS/RPC behavior. Failure recovery tests inject deliberate 503 responses.
- No remote project reads/writes during E2E.
- Generated product rows after teardown: **0**, verified through local PostgreSQL.
- App process stopped by fixture; no listener on **5218**, verified with netstat.
- Local Supabase database remains running and healthy. Explicit shutdown: `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1`.
- Production, migrations, shared E2E harness, unit/component tests, spec, plan, project/package files unchanged during E2E.

## Next step

`$theshop-ship 002_variant-filters`. Optional Document may run first.

# Homepage Shop by Brand — E2E Report

**Date:** 2026-11-28  
**Commit:** `abf3292`  
**Verdict:** ✅ VERIFIED

**Coverage:** 11 E2E ACs, 4 unit ACs, 0 manual. Expected browser cases: 11; discovered: 11; passed: 11; failed: 0; skipped: 0; repair rounds: 1. The Test stage passed 37/37 lower-level tests.

The feature E2E gate and project build passed. The gate reported 19 nonblocking hook warnings in other features. The local Supabase stack was reset, migrated through `0040_manage_homepage_brands.sql`, and seeded by `start-e2e-env.ps1`. Chromium was installed in the ignored D: test cache after the default C: cache ran out of space. The first browser run passed 10/11; AC-5 read the reopened rows before the edit view finished loading. An explicit Playwright wait and auto-waiting text assertion fixed that test. Its focused retry and the final 11-case feature run passed.

| AC | Bucket | Result | Evidence |
|---|---|---|---|
| AC-1 | E2E | Passed | `ManageHomepageBrandJourneyTests.AC1_AddPrefillsFieldsButNoBrands` |
| AC-2 | E2E | Passed | `ManageHomepageBrandJourneyTests.AC2_EditRestoresSavedFieldsStatusAndOrder` |
| AC-3 | E2E | Passed | `ManageHomepageBrandJourneyTests.AC3_ChoosingActiveBrandsUpdatesDraftOnceWithIdentity` |
| AC-4 | Unit | Corroborated by passing Test stage | `ManageHomepageBrandTests.FullSelection_DisablesAddUntilOneRemoved` |
| AC-5 | E2E | Passed | `ManageHomepageBrandJourneyTests.AC5_ReorderedBrandsPersistAfterSave` |
| AC-6 | Unit | Corroborated by passing Test stage | `HomepageBrandSectionTests.Create_HiddenEmptySelection_AcceptsZeroButVisibleRequiresOne` |
| AC-7 | Unit | Corroborated by passing Test stage | `ManageHomepageBrandTests.Save_ValidationError_RetainsDraftAndDoesNotNavigate` |
| AC-8 | E2E | Passed | `ManageHomepageBrandJourneyTests.AC8_ChangedFieldsAndHiddenStatusPersistTogether` |
| AC-9 | E2E | Passed | `ManageHomepageBrandJourneyTests.AC9_CancelDiscardsUnsavedChanges` |
| AC-10 | Unit | Corroborated by passing Test stage | `ManageHomepageBrandTests.Edit_PendingLoadShowsSkeletonThenFailureAllowsRetry` |
| AC-11 | E2E | Passed | `ManageHomepageBrandGuestJourneyTests.AC11_GuestCannotOpenEditor` |
| AC-12 | E2E | Passed | `ManageHomepageBrandJourneyTests.AC12_ResponsiveKeyboardControlsRemainOperable` |
| AC-13 | E2E | Passed | `ManageHomepageBrandJourneyTests.AC13_InactiveBrandNeverAppearsInPickerOrSearch` |
| AC-14 | E2E | Passed | `ManageHomepageBrandJourneyTests.AC14_DialogCancelAndCloseDiscardChoicesUntilApply` |
| AC-15 | E2E | Passed | `ManageHomepageBrandJourneyTests.AC15_UnmatchedSearchShowsGuidanceAndClearRestoresChoices` |

**Findings:**

| FQN / scope | Class | Symptom | Trace | Hypothesis / resolution |
|---|---|---|---|---|
| `PlaywrightFixture.InitializeAsync` | Environment | All 11 cases initially failed before navigation because Chromium v1255 was absent. | Initial test output; no browser trace. | Installed required Chromium in `.sdd/.test-work/ms-playwright`; C: lacked space. |
| `ManageHomepageBrandJourneyTests.AC5_ReorderedBrandsPersistAfterSave` | Test timing | Immediate row read after reopening returned an empty collection. | `tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.ManageHomepageBrandJourneyTests.AC5_ReorderedBrandsPersistAfterSave.zip` | The page was still loading; wait for edit state and row text. Focused retry and full suite passed. |

Fixture teardown stopped the app; port 5218 is free. Supabase remains running. Explicit shutdown: `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1`.

**Next:** `$theshop-ship 008_homepage-brand` (optional Document stage first).

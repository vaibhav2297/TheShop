# Featured — E2E Report

**Feature:** `005_homepage-featured`  
**Date:** 2026-11-02  
**Commit:** `72d2c99ec1070e5d3642195c535d1d4a4e55a084` (feature work in working tree)  
**Verdict:** ✅ VERIFIED

All 20 Featured acceptance criteria passed in Chromium against local Supabase. The first run passed 17 and exposed three test errors. After one repair round, those three passed. No product source was changed or behavior assertion weakened.

| Bucket | ACs |
|---|---:|
| E2E | 20 |
| Unit | 0 |
| Manual | 0 |

| Expected | Discovered | Passed | Failed | Skipped | Repair rounds |
|---:|---:|---:|---:|---:|---:|
| 20 | 20 | 20 | 0 | 0 | 1 |

`featured-initial-20261102.trx`: 20 discovered, 17 passed, 3 failed, 0 skipped. `featured-repair1-20261102.trx`: only the three failed methods rerun, 3 passed, 0 failed/skipped. Both files are under `C:/Users/vaibh/AppData/Local/Temp/theshop-featured-e2e/`. Each AC maps to one method in [e2e-manifest.json](e2e-manifest.json); the three rerun results replace their first-run failures.

## Acceptance criteria

Evidence below names the executed method; full FQNs and coverage reasons are in [e2e-manifest.json](e2e-manifest.json). All rows use the E2E bucket.

| AC | Result | Browser evidence |
|---|---|---|
| AC-1 | Pass | `AC1_HomepageNavigationShowsDefaultsAndLiveAddHeading` — navigation, defaults, live heading. |
| AC-2 | Pass | `AC2_HiddenEditHydratesBothTilesAndLiveHeading` — hidden saved state and artwork. |
| AC-3 | Pass | `AC3_GuestRedirectsAndCustomerOrActionOnlyStaffCannotReadManagement` — guest, customer, Create/Edit-only access denial. |
| AC-4 | Pass | `AC4_ViewOnlyAbsentAndHiddenStatesHaveNoMutationAndDirectRpcIsDenied` — read-only UI and denied RPC. |
| AC-5 | Pass | `AC5_PickDropReplaceRemoveAndCancelledPickerPreserveOtherTileAndSavedState` — picker, drop, replacement, removal, cancel. |
| AC-6 | Pass | `AC6_RealPngJpegWebpAndExactFiveMegabytesDecodeInSquareFrames` — real formats, inclusive 5 MB, square frames. |
| AC-7 | Pass | `AC7_InvalidSelectionsKeepAcceptedArtworkAndShowSpecificFeedback` — invalid files and preview retention. |
| AC-8 | Pass | `AC8_MissingRequiredFieldsShowErrorsWithoutSavingOrFillingHeadingFallback` — required fields and no unintended save. |
| AC-9 | Pass | `AC9_IncompleteButtonPairsTargetMissingFieldsAndBlankPairsSave` — button pair validation. |
| AC-10 | Pass | `AC10_UnsafeLinksFailAndRootRelativeAndHttpLinksPersist` — unsafe links blocked, valid links saved. |
| AC-11 | Pass | `AC11_SuccessWaitsForCompleteCommitAndReopenPreservesHiddenThenVisibleContent` — delayed commit, saved notification, reopened state. |
| AC-12 | Pass | `AC12_CancelAddAndEditDiscardsTextImageReplacementRemovalAndStatus` — canceled drafts leave saved state intact. |
| AC-13 | Pass | `AC13_TwoCreatorsAndRepeatedSaveNeverDuplicateAndConflictRequiresReload` — singleton, conflict reload, last save wins. |
| AC-14 | Pass | `AC14_ResponsiveLoadingSkeletonAndFailedReadRetryNeverExposeFalseAdd` — loading, read failure, retry. |
| AC-15 | Pass | `AC15_SecondUploadFailurePermissionDenialAndUncertainSaveRetainDraftAndSavedRow` — failure recovery retains draft and row. |
| AC-16 | Pass | `AC16_TallSettingsScrollThenStopWith24PixelsClearanceAndReleaseWithinEditor` — tall sidebar stops while left content scrolls. |
| AC-17 | Pass | `AC17_ShortSettingsStayBelowHeaderWithAccessibleActionsAndEditorContainment` — short sidebar and actions remain reachable. |
| AC-18 | Pass | `AC18_ReverseScrollErrorsResizeTextZoomAndFocusKeepPanelReachableAndDraftIntact` — reverse scroll, errors, resize, zoom, focus. |
| AC-19 | Pass | `AC19_TabletAndPhoneUseNormalScrollingAndPreserveDraftAcrossLayouts` — responsive flow and retained input. |
| AC-20 | Pass | `AC20_KeyboardLabelsErrorsImageActionsAndEnglishControlsRemainUsableWithoutOverflow` — keyboard, labels, errors, no overflow. |

## Repair findings

| FQN / class | First-run symptom and evidence | Cause and repair |
|---|---|---|
| `TheShop.E2E.Tests.Journeys.ManageFeaturedAnonymousJourneyTests.AC3_GuestRedirectsAndCustomerOrActionOnlyStaffCannotReadManagement` / fixture | `GrantActionAsync` received REST 404. [Trace](../../tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.ManageFeaturedAnonymousJourneyTests.AC3_GuestRedirectsAndCustomerOrActionOnlyStaffCannotReadManagement.zip). | Fixture queried nonexistent `profiles`; seed puts persona IDs in `customers`. Changed fixture lookup to `customers`. |
| `TheShop.E2E.Tests.Journeys.ManageFeaturedJourneyTests.AC11_SuccessWaitsForCompleteCommitAndReopenPreservesHiddenThenVisibleContent` / assertion | Negative notification assertion waited for a notification element that correctly did not exist while commit was held. [Trace](../../tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.ManageFeaturedJourneyTests.AC11_SuccessWaitsForCompleteCommitAndReopenPreservesHiddenThenVisibleContent.zip). | Asserted zero notifications containing “Featured saved.” The delayed commit and database-empty assertions remain. |
| `TheShop.E2E.Tests.Journeys.ManageFeaturedJourneyTests.AC15_SecondUploadFailurePermissionDenialAndUncertainSaveRetainDraftAndSavedRow` / locator | Two valid notifications made an unfiltered notification locator ambiguous. [Trace](../../tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.ManageFeaturedJourneyTests.AC15_SecondUploadFailurePermissionDenialAndUncertainSaveRetainDraftAndSavedRow.zip). | Filtered each notification by the expected resource string; all failure-path assertions remain. |

## Gates and environment

- Unit manifest gate passed; Test ledger remains Passing, 55/55 from the earlier focused run.
- Featured E2E static gate passed before run and after test repair. Its 19 warnings concern other features; no Featured hook is missing.
- E2E project build passed after repair: 5 warnings, 0 errors. `git diff --check` passed.
- Local `start-e2e-env.ps1` completed a database reset through migration `0037_manage_featured.sql`, seeded personas, regenerated `.e2e-env`, and Featured REST returned HTTP 200. Browser fixture launched the app for the run.
- Port 5218 is free after fixture teardown. Supabase remains running; explicit shutdown: `pwsh -NoProfile -ExecutionPolicy Bypass -File tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1`.
- Optional Vector logging remains as-is by user choice. Core API, auth, storage, and browser journeys worked. No Docker socket mount or logging configuration change was made.
- `graphify update .` ran after test code changes. No production source, shared harness, auth implementation, scripts, dependencies, spec, plan, or unit tests changed in this E2E run.

**Next:** `$theshop-ship 005_homepage-featured`; `$theshop-document` remains optional.

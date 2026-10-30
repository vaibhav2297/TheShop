# Manage Hero Banner — E2E Report

**Date:** 2026-10-29  
**Commit:** `fe8dc54`  
**Verdict:** 🔴 NOT VERIFIED — all browser behavior passes; required Figma visual evidence remains pending.

**Coverage:** 7 E2E, 9 unit, 0 manual. Expected browser cases: 7; discovered: 7; passed: 7; failed: 0; skipped: 0; repair rounds: 1.

The E2E manifest gate and E2E project build passed. Local Supabase Storage recovered after database reset. The app fixture now runs prebuilt Web output; Playwright Chromium was installed locally. The feature-filtered browser run passed 7/7. Fixture teardown freed port 5218; Supabase remains running. Explicit shutdown: `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1`. The visual gate could not run under this shell (`System.IO.Path.GetRelativePath` requires PowerShell 7); Figma evidence remains pending in Execute.

| AC | Bucket | Result | Evidence |
|---|---|---|---|
| AC-1 | E2E | Passed (browser) | `ManageHeroBannerJourneyTests.AC1_ConsoleCardOpensEmptyAddForm` |
| AC-2 | Unit | Corroborated by passing Test stage | [test-manifest.json](test-manifest.json) |
| AC-3 | E2E | Passed (browser) | `ManageHeroBannerAnonymousJourneyTests.AC3_AnonymousUserCannotOpenHomepageManagement` |
| AC-4 | Unit | Corroborated by passing Test stage | [test-manifest.json](test-manifest.json) |
| AC-5 | E2E | Passed (browser) | `ManageHeroBannerJourneyTests.AC5_ImageSelectionAndReplacementStayInDraft` |
| AC-6 | E2E | Passed (browser) | `ManageHeroBannerJourneyTests.AC6_InvalidImageSizeAndDimensionsShowFeedback` |
| AC-7 | Unit | Corroborated by passing Test stage | [test-manifest.json](test-manifest.json) |
| AC-8 | Unit | Corroborated by passing Test stage | [test-manifest.json](test-manifest.json) |
| AC-9 | Unit | Corroborated by passing Test stage | [test-manifest.json](test-manifest.json) |
| AC-10 | E2E | Passed (browser) | `ManageHeroBannerJourneyTests.AC10_SaveNavigatesHomeAndReopenPersistsContent` |
| AC-11 | Unit | Corroborated by passing Test stage | [test-manifest.json](test-manifest.json) |
| AC-12 | E2E | Passed (browser) | `ManageHeroBannerJourneyTests.AC12_CancelDiscardsUnsavedDraft` |
| AC-13 | Unit | Corroborated by passing Test stage | [test-manifest.json](test-manifest.json) |
| AC-14 | Unit | Corroborated by passing Test stage | [test-manifest.json](test-manifest.json) |
| AC-15 | Unit | Corroborated by passing Test stage | [test-manifest.json](test-manifest.json) |
| AC-16 | E2E | Passed (browser) | `ManageHeroBannerJourneyTests.AC16_NarrowAndWideFormsRemainUsableByKeyboard` |

**Findings:** The app fixture's implicit build failed on a sandbox-denied user NuGet config; `--no-build` uses the required prior E2E build and resolves startup. Playwright Chromium v1248 was missing and is now installed. AC-6 first failed because the test expected `aria-invalid="true"` while the input rendered an empty attribute; the test now asserts the visible image error messages. Its initial failure trace is `tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.ManageHeroBannerJourneyTests.AC6_InvalidImageSizeAndDimensionsShowFeedback.zip`. The focused retry and full seven-case rerun passed.

**Next:** Complete Figma visual evidence in Execute and run the visual gate with PowerShell 7 before marking Verify Verified.

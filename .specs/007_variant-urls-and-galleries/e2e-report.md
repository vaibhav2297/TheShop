# E2E report — Variant URLs and Galleries

**Date:** 2026-11-17  
**Commit:** `e9ebf2b1c343fd16126fc98b47abf42591bb1605` (working tree changes under test)  
**Verdict (2026-11-17 baseline):** ✅ VERIFIED

The 18/18 result below predates the 2026-11-18 dialog correction. See its focused follow-up result below.

## Scope and result

| Bucket | ACs | Result |
|---|---:|---|
| E2E | 18 | 18 passed |
| Unit | 6 | Corroborated by [23/23 passing lower-level tests](test-report.md) |
| Manual | 0 | — |

Expected: **18** browser tests. Discovered: **18**. Latest result per test: **18 passed, 0 failed, 0 skipped**. Focused AC-18 follow-up: **three targeted runs** (two diagnostic, one passing after one test repair). The initial feature run had 8 pass/10 fail; targeted repairs ran 4/10, 6/6, and 3/4. The final AC-18 run passed 1/1. The E2E gate and project build pass. The gate reports 19 unrelated hook warnings.

Evidence names below use `Public` = `TheShop.E2E.Tests.Journeys.VariantGalleryPublicJourneyTests`, `Admin` = `TheShop.E2E.Tests.Journeys.VariantGalleryAdminJourneyTests`; unit names are fully qualified in [e2e-manifest.json](e2e-manifest.json). Browser logs: [repair 1](../../.sdd/.test-work/e2e-repair1.log), [repair 2](../../.sdd/.test-work/e2e-repair2.log), [repair 3](../../.sdd/.test-work/e2e-repair3.log), [AC-18 diagnostic](../../.sdd/.test-work/e2e-ac18-viewport.log), [AC-18 passing rerun](../../.sdd/.test-work/e2e-ac18-fixed.log).

| AC | Bucket | Result | Evidence |
|---|---|---|---|
| AC-1 | E2E | Pass | `Public.AC1_ExactAddressesShowTheirOwnPurchaseDetailsAndPhotos` |
| AC-2 | E2E | Pass | `Public.AC2_SelectionChangesAddressAndGalleryWithoutReloadOrScroll` |
| AC-3 | E2E | Pass | `Public.AC3_DesktopAndMobileGalleriesKeepSavedImageOrder` |
| AC-4 | E2E | Pass | `Admin.AC4_ReorderingSavedImagesChangesVariantCover` |
| AC-5 | Unit | Pass | `VariantImageAssignmentTests.AppendMissing_ExistingAndRepeatedImages_PreservesCoverAndAppendsOnce` |
| AC-6 | Unit | Pass | `ApplyVariantImagesTests.Handle_ReplaceOneVariant_ReordersCoverWithoutChangingProductOrSibling` |
| AC-7 | E2E | Pass | `Admin.AC7_RemovingCoverPromotesNextAndFinalRemovalShowsPlaceholder` |
| AC-8 | E2E | Pass | `Public.AC8_ParentAddressSelectsFirstAvailableOrFirstUnavailable` |
| AC-9 | E2E | Pass | `Public.AC9_HistoryRefreshAndCopiedAddressRestoreExactVariant` |
| AC-10 | E2E | Pass | `Public.AC10_EmptyAndBrokenAssignedImagesNeverShowSiblingPhotos` |
| AC-11 | E2E | Pass | `Public.AC11_LegacyProductLinkAndNonvariantGalleryRemainUsable` |
| AC-12 | Unit | Pass | `VariantUrlGallerySchemaTests.Migration_BackfillsPinAndStableReadableRoutes` |
| AC-13 | E2E | Pass | `Public.AC13_UnknownWrongParentAndUnpublishedAddressesAreUnavailable` (also checks deleted variant) |
| AC-14 | E2E | Pass | `Public.AC14_UnavailableExactVariantKeepsIdentityAndGallery` |
| AC-15 | Unit | Pass | `VariantUrlGallerySchemaTests.SaveProduct_GuestCannotChangeAssignments` |
| AC-16 | E2E | Pass | `Admin.AC16_ForeignProductImagesCannotBeSelectedAndDuplicatesStaySingle` (includes forged save request) |
| AC-17 | E2E | Pass | `Public.AC17_RenamedLabelsKeepSavedVariantAddress`; collision covered by same-AC unit evidence |
| AC-18 | E2E | Pass | `Public.AC18_KeyboardAndTouchKeepSelectedIdentityAndGalleryPosition` |
| AC-19 | E2E | Pass | `Public.AC19_RapidSelectionsLeaveLatestVariantAligned` |
| AC-20 | Unit | Pass | `ProductTests.RemoveImage_AssignedToMultipleVariants_PrunesBothWithoutDeletingVariants` |
| AC-21 | E2E | Pass | `Admin.AC21_FailedSavePreservesStagedGalleryForRetry` |
| AC-22 | E2E | Pass | `Public.AC22_NonexistentCombinationCannotCommitPurchaseState` |
| AC-23 | Unit | Pass | `VariantImageDialogTests.Apply_ReorderedImages_UsesFirstAsCover` |
| AC-24 | E2E | Pass | `Admin.AC24_VariantUploadJoinsLibraryWithoutChangingSiblingOrPrimary` |

## 2026-11-18 dialog correction

Restored the existing Apply To checkbox UI. Removed the extra target list, order list, move buttons, and action hint. Selected tiles display gallery order; deselecting and reselecting moves an image to the end. The shared option value scope was restored in the later correction below.

Focused Web tests: **20/20 passed**. E2E project build and design rules passed. The restored checkbox UI passed **2/2** desktop/mobile browser checks. The six affected browser journeys each passed across focused runs: AC-4 passed alone; the other five passed together. The combined run had one intermittent sign-in fixture timeout before AC-4 began, so it ended 5/6 despite AC-4's separate pass. The full 18-test feature suite was not rerun after this correction.

## 2026-11-23 selection-dialog tile order

Dialog image tiles now remain in product-library order during selection. Variant gallery order and cover still follow selection order. Focused Web tests: **20/20 passed**; E2E project build and design rules passed. AC-4 browser rerun stopped in sign-in setup before opening the dialog because the local E2E services were not listening. The full feature browser suite was not rerun for this correction.

## 2026-11-23 shared option value scope

Restored the prior Apply To choice, “All {count} variants with {option} = {value}”. The card now passes only variants sharing the first eligible option value to the dialog; unrelated variants remain unchanged. Dialog tiles still remain in product-library order. Focused Web tests: **20/20 passed**; E2E project build and design rules passed. Browser journeys were updated but not rerun because the local E2E services were not listening in the preceding focused run.

## 2026-11-23 shared-scope image deselection

Reproduced the bulk removal defect with a failing bUnit test: after deselecting one of two images and applying to the shared option value, reopening still showed both selected. Bulk preview now removes explicitly deselected image IDs from matching variants before appending missing selected IDs. Existing sibling-only images and unrelated variants remain untouched. Focused Web tests: **21/21 passed**; Application tests: **4/4 passed**; Domain tests: **4/4 passed**. Browser journey not rerun for this correction.

## 2026-11-23 shared-scope choice retention

Reproduced the checkbox reset with a failing bUnit test. The variants card now remembers the last successfully applied Apply To choice for each shared option value during the current edit session. A sibling dialog starts with that choice; unrelated option groups remain independent. Cancel leaves the remembered choice unchanged. Focused Web tests: **22/22 passed**; E2E project build and design rules passed. Browser journey not rerun for this correction.

## 2026-11-23 Add images tile geometry

Fixed the collapsed Add images tile by changing its container from full size containment to inline size containment. The square aspect ratio now gives the grid row its height when the tile wraps. Focused native dialog browser checks passed **2/2** at 1440px and 390px, asserting tile width equals height; screenshots were inspected at both widths. E2E project build and design rules passed.

## Earlier AC-18 finding

- **FQN:** `TheShop.E2E.Tests.Journeys.VariantGalleryPublicJourneyTests.AC18_KeyboardAndTouchKeepSelectedIdentityAndGalleryPosition`.
- **Class:** Test targeting; resolved. No product defect found.
- **Symptom:** A raw coordinate touch tap on Blue did not change the URL, while keyboard selection passed.
- **Trace and proof:** [Original timeout](../../.sdd/.test-work/e2e-repair3.log); [viewport diagnostic](../../.sdd/.test-work/e2e-ac18-viewport.log) measured the button center at **y=867** in an **844px** viewport. The raw tap was outside the viewport. The original Playwright trace is `tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.VariantGalleryPublicJourneyTests.AC18_KeyboardAndTouchKeepSelectedIdentityAndGalleryPosition.zip` (primary context only).
- **Fix and result:** Use the option locator's `TapAsync()`, which scrolls the element into view and sends a real touch action. [Focused rerun](../../.sdd/.test-work/e2e-ac18-fixed.log): **1 passed, 0 failed**; exact Blue URL, selected options, purchase details, and image scope asserted.

## Environment and next step

Local Supabase was reset, migrated through `0039_variant_urls_and_galleries.sql`, and seeded by `tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1`. The Playwright fixture stopped the app; port **5218 is free**. Supabase remains running; `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1` stops it explicitly.

The E2E evidence supports Verify. Next optional stage: Document; otherwise `$theshop-ship 007_variant-urls-and-galleries`.

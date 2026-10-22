# Product Detail — E2E Report

**Feature:** `001_product-detail`
**Date:** 2026-10-22
**Commit:** `1c175fb2a15129cfb56a628382be832fc3f06783` · uncommitted feature working tree tested.
**Verdict:** ✅ VERIFIED

## Results

28 expected,28 discovered,28 unique methods passed. Final failed0, skipped0, manual/unclassified0. Repair rounds3, all environment-only; no assertion changes or production fixes.

| Evidence | Expected/exercised | Passed | Final failed | Skipped |
|---|---:|---:|---:|---:|
| Round1 completed methods, console evidence | 17 | 17 | 0 | 0 |
| Round3 filtered remainder, TRX evidence | 11 | 11 | 0 | 0 |
| Unique final coverage | 28 | 28 | 0 | 0 |

The full run was interrupted by disk exhaustion, so this is aggregated evidence, not a single28-case green TRX. Seventeen completed pass records retained from round1; round3 runs exactly the other11 methods. Sets are disjoint and cover every manifest FQN. A separate filtered `--list-tests` discovers exactly the same28 methods. Historical environment failures below remain recorded; none count as a final pass until superseded by completed evidence.

Buckets: e2e28, unit0, manual0. Existing Test row remains Passing with70/70 lower-level tests. No unrelated journeys run.

## Static and visual gates

- Unit manifest gate clean before authoring.
- E2E static gate clean before build and after final run. Other-feature missing-hook warnings remain external debt.
- `dotnet build tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --nologo`:0errors,4 xUnit1051 warnings (3existing,1new cancellation recommendation).
- Final visual gate: `[sdd-gates] visual:001_product-detail - clean.` All34 planned targets retain current, hash-bound reviewed evidence.
- Prior Figma references and reviews reused because production/spec/plan unchanged. Phone no-image inspection sheet opened again: all four comparison panes checked; accepted48px absent-announcement offset retained, centered Subtitle1 fallback matches reference. No reference or baseline overwritten.

## Commands

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1
dotnet build tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --nologo
dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --filter "Category=E2E&Feature=001_product-detail" --logger "console;verbosity=normal" --nologo --no-build

# Final repair: only11 methods without completed pass evidence.
$env:TEMP = "D:\Temp\TheShop-ProductDetail-E2E-20261022"
$env:TMP = $env:TEMP
$env:E2E_HEADLESS = "1"
$remainingFilter = Get-Content -Raw "$env:TEMP/remaining-filter.txt"
dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --filter $remainingFilter --logger "console;verbosity=normal" --logger "trx;LogFileName=product-detail-e2e-round3.trx" --results-directory "$env:TEMP/results" --nologo --no-build

dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --filter "Category=E2E&Feature=001_product-detail" --list-tests --nologo --no-build
pwsh -NoProfile -File .sdd/scripts/check-sdd-gates.ps1 e2e -Feature 001_product-detail
pwsh -NoProfile -File .sdd/scripts/check-sdd-gates.ps1 visual -Feature 001_product-detail
```

## Acceptance evidence

Spec order preserved, including AC-25 first. Each AC belongs to exactly one bucket and one journey. PASS below means completed automated journey; Figma fidelity additionally relies on the independent current visual gate.

| AC | Bucket | Result | Method FQN | Evidence |
|---|---|---|---|---|
| AC-25 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC25_BuyBoxScrollsNaturallyThenSticksWithClearanceAndReleasesAtBoundary` | `results/product-detail-e2e-round3.trx Passed` |
| AC-1 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC1_OpensWithUnselectedVariantsRangeAndPrimaryFirst` | `round1.log completed pass` |
| AC-2 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailCustomerJourneyTests.AC2_GuestsAndCustomersHaveIdenticalPublicViewingAndSelection` | `round1.log completed pass` |
| AC-3 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC3_DesktopInitiallyShowsFourPrimaryFirstWithShowMore` | `round1.log completed pass` |
| AC-4 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC4_ManualNarrowScrollUpdatesIndicatorWithoutChangingSelectionOrPrice` | `results/product-detail-e2e-round3.trx Passed` |
| AC-5 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC5_GroupedSelectionUpdatesExactPriceAndImageOnlyWhenComplete` | `round1.log completed pass` |
| AC-6 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC6_VariantWithoutImageFallsBackToProductPrimary` | `round1.log completed pass` |
| AC-7 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC7_VariantFreeProductUsesOwnSalePriceAndBagIsNoOp` | `results/product-detail-e2e-round3.trx Passed` |
| AC-8 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC8_DisclosuresStartCollapsedAndExpandIndependently` | `results/product-detail-e2e-round3.trx Passed` |
| AC-9 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC9_BlankContentOmitsBothOptionalSections` | `round1.log completed pass` |
| AC-10 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC10_ActionButtonsStayEnabledWithoutPersistenceOrSuccessFeedback` | `results/product-detail-e2e-round3.trx Passed` |
| AC-11 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC11_KeyboardControlsExposeNamesFocusAndState` | `round1.log completed pass` |
| AC-12 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC12_SingleImageHasNoFalseNavigationAtAllBreakpoints` | `round1.log completed pass` |
| AC-13 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC13_MissingPrimaryEmptyAndBrokenImagesUseNamedSubtitlePlaceholder` | `round1.log completed pass` |
| AC-14 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC14_MissingUnpublishedAndPrivilegedRoutesShowUnavailableWithoutLeakingChildren` | `round1.log completed pass` |
| AC-15 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC15_MissingSelectionFocusesRevealsAndBrieflyShakesWithoutVisibleError` | `round1.log completed pass` |
| AC-16 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC16_CompleteSelectionClearsAttentionAndLaterBagDoesNothing` | `round1.log completed pass` |
| AC-17 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC17_NarrowGalleryExposesEveryImageWithPrimaryAndPositionIndicator` | `results/product-detail-e2e-round3.trx Passed` |
| AC-18 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC18_ShowMoreAndShowLessPreserveSelectionOrderAndToggleFocus` | `round1.log completed pass` |
| AC-19 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC19_VisibleDesktopVariantImageDoesNotScrollOrHighlight` | `results/product-detail-e2e-round3.trx Passed` |
| AC-20 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC20_HiddenDesktopTargetExpandsAndRevealsWithoutReordering` | `round1.log completed pass` |
| AC-21 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC21_AtMostFourDesktopImagesHaveNoExpansionControl` | `round1.log completed pass` |
| AC-22 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC22_EqualVariantPricesShowOneAmountWithNoInitialChoice` | `round1.log completed pass` |
| AC-23 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC23_SelectionUsesSalePriceThenOriginalPrice` | `round1.log completed pass` |
| AC-24 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC24_ReducedMotionUsesStaticOutlineAndFocusWithoutShake` | `results/product-detail-e2e-round3.trx Passed` |
| AC-26 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC26_LoadingSkeletonHasResponsiveStaticGalleryAndMeaningfulStatus` | `results/product-detail-e2e-round3.trx Passed` |
| AC-27 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC27_CatalogueUsesGalleryThumbnailAndDetailsUseBodyTypographyAndEightPixelGridGap` | `results/product-detail-e2e-round3.trx Passed` |
| AC-28 | e2e | PASS | `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC28_TileTypographyHoverNativeNavigationAndIndependentActionsMatchContract` | `results/product-detail-e2e-round3.trx Passed` |

## Findings and environment repairs

No product defect found. Source assertions unchanged after initial static gate. Browser page objects use native route navigation, real Supabase reads/storage, existing signed-in personas, semantic controls and product-detail hooks. Generated rows uniquely named; no app DOM/compositions injected. Horizontal navigation uses native scroll/keyboard; layout/motion read from the running browser.

### Initial host: stale asset mappings

**Class:** environment. **Symptom:** shell never boots; route/auth locators timeout. Trace shows404 for `_framework/TheShop.Web.bvczs0ni16.pdb` and `_framework/TheShop.Web.uaejagvxnm.wasm`, SRI failures, `Failed to start platform`. **Hypothesis confirmed:** existing Visual Studio dev server had cached pre-build asset mappings despite serving current plain JS. Initial host PID26452 stopped only after confirming its command points to this repository. Subsequent hosts launched by the existing AppHostFixture, not manually.

Recorded failure FQNs:

- `TheShop.E2E.Tests.Journeys.ProductDetailCustomerJourneyTests.AC2_GuestsAndCustomersHaveIdenticalPublicViewingAndSelection`
- `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC13_MissingPrimaryEmptyAndBrokenImagesUseNamedSubtitlePlaceholder`

Exact output: `System.TimeoutException : Timeout 30000ms exceeded.` Waiting for `main.shop-auth-layout` or `main[data-shop-scroll-root], main.shop-auth-layout`.

Trace: `tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC13_MissingPrimaryEmptyAndBrokenImagesUseNamedSubtitlePlaceholder.zip`. Historical environment trace; AC-13 subsequently passes. Auth initialization failed before the shared tracing context existed, so no separate AC-2 trace.

### Round1: C drive exhausted during tracing

**Class:** environment. **FQN:** `TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC7_VariantFreeProductUsesOwnSalePriceAndBagIsNoOp`. Seventeen methods completed successfully before C: reached0bytes free. Failure arose in fixture teardown `Tracing.StopAsync`, not a product assertion. Exact output: `Microsoft.Playwright.PlaywrightException : ENOSPC: no space left on device, write`. No completed full-run TRX; round1 console retained and hashed below.

Moved only this session's two Playwright artifact directories to the task runtime folder on D:. Old third-party/user temporary files left alone. D: has roughly142GB free; C: remains near90MB free. Further reruns should retain D: TEMP/TMP until disk capacity is addressed.

### Round2: interrupted-process leftovers

**Class:** environment. Same AC-7 FQN. Exact output: `System.TimeoutException : Timeout 30000ms exceeded.` Navigation waited for `load` against an orphan host while interrupted test/driver processes remained active. Round2 stopped without completed pass evidence. Trace: `tests/TheShop.E2E.Tests/bin/Debug/net10.0/playwright-traces/TheShop.E2E.Tests.Journeys.ProductDetailJourneyTests.AC7_VariantFreeProductUsesOwnSalePriceAndBagIsNoOp.zip`.

Stopped40 processes only from verified feature-test trees, including orphan drivers/hosts; unrelated browser/Visual Studio/MCP processes retained. Confirmed port5218 free before round3. Final run uses D: TEMP/TMP, fresh fixture-owned app and exactly11 remaining methods; all pass, exit0. No fixed sleeps, blind retries or weakened assertions.

## Environment and teardown

- Canonical environment script ran successfully; local Supabase reset/migrations/seed authorized by this invoked E2E skill.
- Stale generated admin auth state moved to a private temp backup after reset; existing persona factory minted valid states through real OTP UI. No credentials printed or committed.
- ProductDetailData seeds only local loopback Supabase, using unique product IDs/names/SKUs and Storage object keys. Fixtures do not call production.
- Successful journeys remove their rows/objects. Two exact fixture rows/objects left by interrupted runs cleaned explicitly; final query reports0 `e2e-detail-*` rows.
- Final fixture stopped app/browser. Port5218 listener count0 after teardown. No owned orphan remains. Original stale Visual Studio host was stopped during environment repair.
- Supabase remains running. Explicit shutdown: `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1`.

## Artifacts

Runtime directory: `D:\Temp\TheShop-ProductDetail-E2E-20261022`. Artifacts may expire; AC counts, FQNs and failure excerpts retained in this report.

| Artifact | SHA256 |
|---|---|
| `D:\Temp\TheShop-ProductDetail-E2E-20261022\initial.log` | `BC247AD7A336DE59DAE68B073A220AE1BF8B72F8415A2C1CF493EDCF7F6AFEC8` |
| `D:\Temp\TheShop-ProductDetail-E2E-20261022\round1.log` | `F7AF23858AE0B12BDB3D4CC8925BA4FFD8F65581E96145E61D404C2CCF1E752D` |
| `D:\Temp\TheShop-ProductDetail-E2E-20261022\completed-round1.json` | `2E7A833653097F8AF83E91CDFA7A3D03D5F0E6B9F9C83034BCB498F7D82BF7A4` |
| `D:\Temp\TheShop-ProductDetail-E2E-20261022\round2.log` | `763DEA1DEA41B96328C813B6CB0594DDAF1DCC7C039DBC0B8F4FECA053804EF5` |
| `D:\Temp\TheShop-ProductDetail-E2E-20261022\round3.log` | `20E08B2F35C674EB07E32346BF0E1206845C9D4DDC49E0C43EC5103613D07092` |
| `D:\Temp\TheShop-ProductDetail-E2E-20261022\remaining-filter.txt` | `796EB7086F360190521D908637A0C1340BA5CBEA75DB83A8284C706EA59D560A` |
| `D:\Temp\TheShop-ProductDetail-E2E-20261022\discovery.log` | `E5F3C0E8EE332569D0193E09DA53A03D64B236AEE69C501DF9ED2C30F0F267E7` |
| `D:\Temp\TheShop-ProductDetail-E2E-20261022\results\product-detail-e2e-round3.trx` | `CF707F3D1015C8DBD946E3DEA4C1FC7FD05C87FC167AD6325CCA9100AC58141E` |
| `D:\Temp\TheShop-ProductDetail-E2E-20261022\e2e-gate.log` | `64C976AB2D0B6D0212C4AF411B42E4E2BFC91B0768BD5D086A31428D16138922` |

Graph update completed, exit0:10463nodes,25742edges,645communities. AST-only, no API cost. Tool limitations:48 JSON/config/evidence inputs produced no nodes;33 SQL files skipped because tree_sitter_sql is absent;173 community labels refreshed from hubs. SQL behavior is exercised by the real local Supabase journeys independently. Graph log: `D:\Temp\TheShop-ProductDetail-E2E-20261022\graphify.log`.

## Next step

`$theshop-ship 001_product-detail`. Optional: `$theshop-document 001_product-detail`. This E2E helper satisfies the Verify ledger row; Test remains Passing.

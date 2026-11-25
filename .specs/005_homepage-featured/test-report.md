# Featured — Test Report

**Feature:** `005_homepage-featured`
**Date:** 2026-11-01
**Commit:** `72d2c99ec1070e5d3642195c535d1d4a4e55a084` (feature/test changes in working tree)
**Verdict:** ✅ PASSING

## Result

55 expected / 55 discovered / 55 passed / 0 failed / 0 skipped. Exact FQN sets match the manifest; no missing, extra, aborted, or unexecuted tests. All 20 ACs mapped to passing non-browser evidence and/or explicit browser boundaries.

| Project | Expected | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| `tests/TheShop.Domain.Tests/TheShop.Domain.Tests.csproj` | 6 | 6 | 6 | 0 | 0 |
| `tests/TheShop.Application.Tests/TheShop.Application.Tests.csproj` | 21 | 21 | 21 | 0 | 0 |
| `tests/TheShop.Infrastructure.Tests/TheShop.Infrastructure.Tests.csproj` | 13 | 13 | 13 | 0 | 0 |
| `tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj` | 15 | 15 | 15 | 0 | 0 |
| **Total** | **55** | **55** | **55** | **0** | **0** |

## Commands and evidence

Manifest gate:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 manifest -Feature 005_homepage-featured
```

Result: clean, including final check after repairs.

Each of the four manifested projects built with `dotnet build <project> --no-restore --nologo -v quiet`; final builds: zero errors. Application/Infrastructure analyzer warnings remain; Web reports the existing AngleSharp NU1902 warning. No package changes.

Each project ran with:

```powershell
dotnet test <project> --filter "Feature=005_homepage-featured" --logger "console;verbosity=normal" --nologo --no-build
```

Added TRX logger and results directory for exact-name/count reconciliation. Final evidence files:

```text
C:\Users\vaibh\AppData\Local\Temp\theshop-featured-tests\Domain-initial.trx
C:\Users\vaibh\AppData\Local\Temp\theshop-featured-tests\Application-final.trx
C:\Users\vaibh\AppData\Local\Temp\theshop-featured-tests\Infrastructure-final.trx
C:\Users\vaibh\AppData\Local\Temp\theshop-featured-tests\Web-final.trx
```

Domain passed on its first run; only the three affected projects reran after test repairs. No full suite or browser run. PostgreSQL tests used the existing Testcontainers harness with isolated `postgres:17-alpine`; no Dev/shared database touched.

## Failures and repairs

**Unresolved failures: none. No production defects identified by this scoped run.**

Test-only repairs completed:

- Split 55 combined Fact/Trait attributes into standalone trait stamps accepted by the manifest scanner.
- Made four validator request constructor types explicit to resolve overload ambiguity; encoded simulated SQL state in `PostgrestException.Message` because `Content` is read-only. Both affected projects rebuilt successfully.
- `TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.Authorization_CreateAndEditRequireViewAndMatchingActionBeforeValidation`: initial `NullReferenceException` at `FeaturedImageValidation.ErrorAsync` caused by an unconfigured inspector substitute. Supplied valid PNG inspection; matrix now passes.
- `TheShop.Infrastructure.Tests.Persistence.FeaturedSchemaTests.DirectTableWritesCannotBypassCombinedPermissions`: initial expected `42501`, actual `22023: image unavailable`. PostgreSQL BEFORE trigger runs before INSERT policy and cannot read artwork without View. Assert exact trigger rejection plus zero inserted rows and preserved saved row; now passes.
- All 15 `TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests` methods initially failed in fixture construction with `System.ArgumentException: Use one of the SetupModule() methods instead to set up an invocation handler that returns an IJSObjectReference.` Replaced generic object-return setup with nested bUnit `SetupModule("mount", ...)`; lifecycle assertions inspect module invocations. All 15 now pass, including pending-image Save/duplicate submission and disposal.

## Acceptance-criterion evidence

Passing evidence below covers non-browser behavior only. `e2e` rows retain the stated browser remainder; this report does not claim visual approval or completion of scrolling/layout journeys. Exact class/method inventory: [test-manifest.json](test-manifest.json).

| AC | Passing test evidence | Pending browser boundary |
|---|---|---|
| AC-1 | `TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Add_DefaultsToFeaturedVisibleAndExactlyTwoDistinctAccessibleTiles`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Heading_UsesTrimmedLiveTitleAndBlankFallbackWithoutChangingInput` | — |
| AC-2 | `TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Edit_HydratesEveryFieldAndHiddenStatusAndUsesLiveEditHeading`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.Read_AbsentSuccessfulNullAndHiddenRowSignsBothPrivatePreviews` | — |
| AC-3 | `TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.Authorization_ReadAndImageSelectionRequireViewEvenForCreateOrEditStaff`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedSchemaTests.Rpc_CreateAndEditRequireViewPlusMatchingAction`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedSchemaTests.AnonymousCannotReadOrWriteConfigurationOrPrivateObjects` | Guest redirect and authenticated route denial require browser routing/authentication. |
| AC-4 | `TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.ViewOnly_SavedContentHasNoMutationControlsAndAbsentHasNoAddForm`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedSchemaTests.ViewOnly_CanReadAbsentAndHiddenButCannotRpcOrDirectlyMutate`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedSchemaTests.DirectTableWritesCannotBypassCombinedPermissions`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.Authorization_CreateAndEditRequireViewAndMatchingActionBeforeValidation` | — |
| AC-5 | `TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.ImageReplacementAndRemovalOnlyChangeTheSelectedTile`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.RejectedImagePreservesPriorPreviewAndDisplaysLocalizedError` | OS picker cancellation and drag/drop require browser checks; empty selection, tile isolation and rejection are covered here. |
| AC-6 | `TheShop.Application.Tests.Features.Homepage.FeaturedValidationTests.Selection_ThreeSupportedTypesAcceptMinimumAndLargerSquare`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedValidationTests.Selection_ExactFiveMegabytesAcceptsAndLargerRejectsBeforeDecoding`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Add_DefaultsToFeaturedVisibleAndExactlyTwoDistinctAccessibleTiles` | Browser decoder integration, proportional rendering and guidance appearance require E2E; inspector contract decisions are covered here. |
| AC-7 | `TheShop.Application.Tests.Features.Homepage.FeaturedValidationTests.Selection_ExactFiveMegabytesAcceptsAndLargerRejectsBeforeDecoding`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedValidationTests.Selection_EmptyUnsupportedMismatchedAndCorruptImagesReject`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedValidationTests.Selection_UndersizedOrNonSquareRejectsWithoutRoundingTolerance`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.RejectedImagePreservesPriorPreviewAndDisplaysLocalizedError` | Actual PNG/JPEG/WebP and corrupt-byte decoding require browser runtime; validator errors and preserved preview are covered here. |
| AC-8 | `TheShop.Application.Tests.Features.Homepage.FeaturedValidationTests.Create_MissingRequiredFieldsReportsBothTilePathsAndSectionTitle`<br>`TheShop.Domain.Tests.Entities.FeaturedSectionTests.Tile_RequiredTitleAndImageRejectBlankValues`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Heading_UsesTrimmedLiveTitleAndBlankFallbackWithoutChangingInput`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.FieldErrors_MapToNestedImageAndTextFieldsAndClearOnlyChangedField` | — |
| AC-9 | `TheShop.Application.Tests.Features.Homepage.FeaturedValidationTests.Buttons_BlankPairsOptionalAndEachIncompletePairTargetsMissingField`<br>`TheShop.Domain.Tests.Entities.FeaturedSectionTests.Tile_OptionalLabelAndButtonPairAllowBothAbsentAndRejectHalfPair` | — |
| AC-10 | `TheShop.Application.Tests.Features.Homepage.FeaturedValidationTests.Links_AllowRootRelativeOrHttpAndRejectUnsafeOrMalformedDestinations`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedValidationTests.Commands_TrimRequiredAndOptionalTextWithoutInventingDefaults`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedSchemaTests.CompleteSave_RoundTripsPlainTextBothTilesAndVisibility` | — |
| AC-11 | `TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.Create_UploadsBothCanonicalImagesBeforeOneCompleteCommit`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.Update_OnlyChangedImageUploadsAndSavedAggregateRemainsUntouchedUntilCommit`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Save_WaitsForAcceptedImageAndCommitBeforeSuccessAndSuppressesDuplicateSubmit`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Save_AddDispatchesBothDraftTilesAndSelectedVisibility`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedSchemaTests.CompleteSave_RoundTripsPlainTextBothTilesAndVisibility`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedSchemaTests.MissingSecondImageRejectsWholeSaveAndPreservesSavedRow`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedRepositoryTests.Save_UsesOneParameterizedRpcContainingEntireOrderedConfiguration` | Reopen after navigation with real API/storage requires E2E; database roundtrip and notification timing are covered here. |
| AC-12 | `TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Cancel_AddAndEditProvideHomepageNavigationWithoutSaveOrUploadCommand` | Actual Cancel navigation and reopening unchanged saved state require E2E; href and absence of mutation dispatch are covered here. |
| AC-13 | `TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.Create_ExistingHiddenSectionRejectsDuplicateBeforeUpload`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.Update_MissingSectionRejectsBeforeWriting`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedSchemaTests.DuplicateAddAndMissingEditReturnConflictAndLastSuccessfulCompleteSaveWins`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.ConflictOrUnconfirmedSaveBlocksRetryUntilExplicitReload`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Save_WaitsForAcceptedImageAndCommitBeforeSuccessAndSuppressesDuplicateSubmit`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedRepositoryTests.Save_MapsDatabaseFailuresAndUnknownCommitToDistinctResourceKeys` | Simultaneous browser sessions require E2E; singleton conflicts, complete-row last-save-wins and duplicate-submit guard are covered here. |
| AC-14 | `TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Loading_ShowsSkeletonAndNoEditableAddUntilReadCompletes`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.LoadFailure_ShowsLocalizedAlertAndRetryRestoresSavedEdit`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.LoadFailuresAndPermissionLossReturnLocalizedKeysWithoutWrites` | — |
| AC-15 | `TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.SaveFailureOrPermissionLossRetainsTextImagesAndStatusWithoutSuccess`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.Update_SecondUploadFailureLeavesSavedRowAndRetainsFirstUpload`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.Create_DefiniteAndUncertainSaveFailuresNeverDeleteUploads`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedSchemaTests.MissingSecondImageRejectsWholeSaveAndPreservesSavedRow`<br>`TheShop.Application.Tests.Features.Homepage.FeaturedHandlerTests.LoadFailuresAndPermissionLossReturnLocalizedKeysWithoutWrites`<br>`TheShop.Infrastructure.Tests.Persistence.FeaturedRepositoryTests.Save_MapsDatabaseFailuresAndUnknownCommitToDistinctResourceKeys` | — |
| AC-16 | Browser-only geometry criterion. | Tall Section scrolling to its bottom, stopping with 24px gap, continued left scrolling and editor containment require real layout. |
| AC-17 | Browser-only geometry criterion. | Short Section stickiness and lower action accessibility require real layout. |
| AC-18 | `TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.ScrollLifecycle_MountsOnceRefreshesAfterEditsAndDisposesOnNavigation` | Reverse scroll, resize, zoom, validation height changes and focus without draft loss require real layout; JS mount/refresh/dispose lifecycle is covered here. |
| AC-19 | Browser-only geometry criterion. | Tablet tile pair with Section below, phone stacking and normal scrolling require real viewports. |
| AC-20 | `TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.Add_DefaultsToFeaturedVisibleAndExactlyTwoDistinctAccessibleTiles`<br>`TheShop.Web.Tests.Pages.Admin.ManageFeaturedTests.FieldErrors_MapToNestedImageAndTextFieldsAndClearOnlyChangedField` | Keyboard traversal, visible focus, assistive announcements and English overflow require browser checks; IDs, labels, hints and field errors are covered here. |


## Scope and checks

- Production source, migrations, project/package files, shared harness, spec and plan unchanged in this invocation.
- Feature tests cover domain invariants/value equality, image limits and inspector results, required/button/link validation, normalization, permission pipeline, uploads before atomic commit, retained immutable files after failures, signed preview DTOs, parameterized RPC writes/error mapping, real PostgreSQL RPC/RLS/storage, and editor render/event/error/lifecycle behavior.
- `git diff --check`: clean.
- `graphify update .`: completed; 11,747 nodes / 29,510 edges. Extraction limitations: 40 metadata files yielded no nodes; 38 SQL files omitted because the optional SQL parser is absent. No dependency changes.

**Next:** `$theshop-e2e 005_homepage-featured` — browser journeys, actual decoding, routes, scrolling, responsive layout and accessibility boundaries.

# Variant Filters — Test Report

**Feature:** `002_variant-filters`
**Date:** 2026-10-22
**Commit:** `7846b5f1a372492d6f1043f6d2f5cda3afcac614` plus current feature changes.
**Verdict:** ✅ PASSING

## Results

| Project | Expected | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| TheShop.Application.Tests | 30 | 30 | 30 | 0 | 0 |
| TheShop.Infrastructure.Tests | 22 | 22 | 22 | 0 | 0 |
| TheShop.Web.Tests | 90 | 90 | 90 | 0 | 0 |
| **Total** | **142** | **142** | **142** | **0** | **0** |

Expected/discovered counts reconcile per project, class, and method. 76 new feature cases; 66 existing catalogue/management page cases retain assertions and legacy traits, with added feature traits. Existing page fixtures adapt to listing-wrapper dispatch.

## Commands and environment

Manifest gate passes before builds and after test repair:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 manifest -Feature 002_variant-filters
```

For each manifested Application, Infrastructure, and Web test project:

```powershell
dotnet build tests/TheShop.{Layer}.Tests/TheShop.{Layer}.Tests.csproj --nologo --no-restore -m:1 -p:UseSharedCompilation=false -v:q
dotnet test tests/TheShop.{Layer}.Tests/TheShop.{Layer}.Tests.csproj --filter "Feature=002_variant-filters" --logger "console;verbosity=normal" --nologo --no-build
```

Final builds: Application 2 warnings/0 errors; Infrastructure 49 warnings/0 errors; Web 1 warning/0 errors. Warnings include existing analyzer/dependency warnings and obsolete Testcontainers constructors. No package/dependency changes.

PostgreSQL tests use Docker 29.4.3, `postgres:17-alpine`, Npgsql, and existing `RbacTestSchema`. Fixture applies actual migrations 0022, 0028, and 0033 over minimal catalogue prerequisites. Tests isolate data through transaction rollback; RBAC functions and child RLS run unmodified. Grants precede feature migration so tests preserve its execution restrictions. No live project writes or browser journeys.

## Acceptance evidence

Full FQNs, files, methods, expanded theory counts, and AC mapping: [test-manifest.json](test-manifest.json).

| Criterion | Passing test evidence | Remaining boundary |
|---|---|---|
| AC-1 | `VariantFilterSchemaTests.Facets_NormalizedNames_MergeCaseAndSpacesButKeepDifferentSpellings`; `VariantFilterPanelTests.Render_MerchantLabels_AreEncodedAndAssociatedWithNativeCheckboxes` | — |
| AC-2 | `VariantFilterSchemaTests.Facets_UnassignedValuesAndLaterPages_UsesAllActualAssignmentsOnly`; `GetProductCatalogueListingHandlerTests.Handle_ChangedMetadata_ReloadsAllChoicesBeforeEveryPage` | — |
| AC-3 | `VariantFilterSchemaTests.Facets_SavedActivationAndDeletion_RefreshesChoicesAndRemovesEmptyGroups`; `GetProductCatalogueListingHandlerTests.Handle_ChangedMetadata_ReloadsAllChoicesBeforeEveryPage` | — |
| AC-4 | `VariantFilterSchemaTests.Catalogue_MultipleValuesAndGroups_RequiresOneVariantToMatchEveryGroup`; `VariantCatalogueTests.Toggle_TwoValues_ReturnsToFirstPageAndRetainsChoicesWhenPaging` | — |
| AC-5 | `VariantFilterSchemaTests.Catalogue_MultipleValuesAndGroups_RequiresOneVariantToMatchEveryGroup` | — |
| AC-6 | `VariantFilterSchemaTests.Catalogue_DuplicateMatchesAndNoVariants_CountsProductsOnceBeforePagination` | — |
| AC-7 | `VariantFilterSchemaTests.PublicScope_InactiveAndUnpublished_RemainsRestrictedEvenForStaff`; `VariantFilterSchemaTests.Facets_SavedActivationAndDeletion_RefreshesChoicesAndRemovesEmptyGroups`; `VariantFilterSchemaTests.DeletedProduct_RemovesChoicesAndMatchesFromBothScopes` | — |
| AC-8 | `VariantFilterSchemaTests.Catalogue_ExistingCriteriaAndPrices_FiltersBeforePagingWithoutChangingDisplayPrice`; `VariantCatalogueTests.Toggle_TwoValues_ReturnsToFirstPageAndRetainsChoicesWhenPaging` | — |
| AC-9 | `VariantFilterSchemaTests.Admin_ExistingCriteriaAndRollups_PreservesFullRangeAndLegacyCall`; `VariantManageProductsTests.Toggle_VariantCriteria_PreservesSearchStatusFixedFiltersAndClearsRowSelection` | — |
| AC-10 | `VariantManageProductsTests.Toggle_VariantCriteria_PreservesSearchStatusFixedFiltersAndClearsRowSelection`; `VariantCatalogueTests.Toggle_TwoValues_ReturnsToFirstPageAndRetainsChoicesWhenPaging`; `VariantCatalogueTests.SupersededResponse_AfterClearCannotRestoreResultsSelectionsNoticeOrUrl`; `VariantCatalogueTests.DisposedPage_CancelsPendingReadAndIgnoresLateResponse` | — |
| AC-11 | `VariantQueryStateTests.Toggle_OneValueThenFinalValue_RemovesOnlyThatRestrictionAndResetsPage`; `VariantFilterPanelTests.Toggle_MultiSelect_ReportsOnlyChangedOptionAndKeepsOtherSelections` | — |
| AC-12 | `VariantCatalogueTests.EmptyFilteredResults_ClearActionRemovesVariantsAndRestoresListing`; `VariantManageProductsTests.NoMatch_ClearRemovesVariantCriteriaAndRestoresResults` | — |
| AC-13 | `VariantQueryStateTests.QueryState_DynamicRepeatedKeys_RoundTripsWithExistingCriteria` | URL serialization covered here; real refresh, shared navigation, browser Back/Forward require theshop-e2e. |
| AC-14 | `VariantFilterMapperTests.FacetRecords_MissingOptionalFields_DefaultToEmptyChoices`; `VariantFilterPanelTests.Render_NoVariantGroups_PreservesFixedGroupLabels` | — |
| AC-15 | `VariantFilterSchemaTests.PublicScope_InactiveAndUnpublished_RemainsRestrictedEvenForStaff`; `VariantFilterSchemaTests.AdminScope_UnauthorizedCaller_DeniesAccess`; `GetAdminProductListingHandlerTests.Authorization_WithoutProductsView_DeniesBeforeMetadataOrPage` | — |
| AC-16 | `VariantFilterSelectionTests.Reconcile_UnknownGroupAndRemovedValue_KeepsValidChoicesAndReportsRemoval`; `VariantCatalogueTests.StaleLink_RemovesInvalidSelectionWithNoticeAndPreservesOtherCriteria`; `VariantManageProductsTests.StaleLink_DropsUnknownValuesAndKeepsValidFiltersWithNotice` | — |
| AC-17 | `GetProductCatalogueListingHandlerTests.Handle_MetadataFailure_DoesNotDispatchPageOrPretendEmptySuccess`; `VariantCatalogueTests.LoadFailure_ShowsRetryWithoutNoMatchAndRecovers`; `VariantManageProductsTests.LoadFailure_OffersRetryWithoutNoMatch` | — |
| AC-18 | `VariantFilterPanelTests.Render_MerchantLabels_AreEncodedAndAssociatedWithNativeCheckboxes`; `VariantCatalogueTests.DrawerAndSidebar_ExposeSameGroupsAndSelections`; `VariantManageProductsTests.DrawerAndSidebar_PreserveSameVariantChoicesAndSelectedState` | Native names/states, merchant encoding, English recovery text, drawer/sidebar parity covered here; keyboard traversal, visible focus and viewport behavior require theshop-e2e. |
| AC-19 | `VariantFilterSchemaTests.PublicScope_InactiveAndUnpublished_RemainsRestrictedEvenForStaff` | — |

All 19 criteria have non-browser evidence. AC-13 real refresh/shared navigation/Back/Forward and AC-18 keyboard/focus/viewport checks remain explicit E2E boundaries, not claimed as browser passes.

## Class reconciliation

| Test class | Expected/discovered/passed |
|---|---:|
| `TheShop.Application.Tests.Features.Products.Queries.GetAdminProductListingHandlerTests` | 5/5/5 |
| `TheShop.Application.Tests.Features.Products.Queries.GetProductCatalogueListingHandlerTests` | 5/5/5 |
| `TheShop.Application.Tests.Features.Products.Queries.VariantListingValidationTests` | 9/9/9 |
| `TheShop.Application.Tests.Features.Products.VariantFilterSelectionTests` | 11/11/11 |
| `TheShop.Infrastructure.Tests.Persistence.VariantFilterMapperTests` | 4/4/4 |
| `TheShop.Infrastructure.Tests.Persistence.VariantFilterSchemaTests` | 18/18/18 |
| `TheShop.Web.Tests.Components.Common.VariantFilterPanelTests` | 3/3/3 |
| `TheShop.Web.Tests.Pages.Admin.ManageProductsTests` | 52/52/52 |
| `TheShop.Web.Tests.Pages.Admin.VariantManageProductsTests` | 7/7/7 |
| `TheShop.Web.Tests.Pages.Products.ProductCatalogueTests` | 14/14/14 |
| `TheShop.Web.Tests.Pages.Products.VariantCatalogueTests` | 8/8/8 |
| `TheShop.Web.Tests.Pages.Products.VariantQueryStateTests` | 6/6/6 |

## Findings and corrections

No unresolved production or environment failures.

Initial parallel builds collided on shared `TheShop.Application.dll` output (`CS2012`); sequential builds resolved collision. Test compilation corrections: explicit validator request types (`CS0121`), expression-tree-compatible link assertion (`CS8072`), and raw SQL interpolation (`CS9006`).

First test run: Application 28/30; Infrastructure 22/22; Web 88/90. One focused test-defect repair, then affected projects rebuilt/re-run:

- `TheShop.Application.Tests.Features.Products.Queries.GetProductCatalogueListingHandlerTests.Handle_StaleSelections_ReconcilesBeforePagingAndPreservesFixedCriteria`: “differs at index 0.” Fresh record instances contain separate list references. Compare complete DTO graphs by value.
- `TheShop.Application.Tests.Features.Products.Queries.GetAdminProductListingHandlerTests.Handle_AdminChoices_ReconcilesUsingManagementMetadataAndPreservesCriteria`: same collection-reference comparison defect; same correction.
- `TheShop.Web.Tests.Components.Common.VariantFilterPanelTests.Render_MerchantLabels_AreEncodedAndAssociatedWithNativeCheckboxes`: `AngleSharp.Dom.DomException: The string did not match the expected pattern.` Quote ID selector containing dynamic key characters.
- `TheShop.Web.Tests.Pages.Products.VariantCatalogueTests.DisposedPage_CancelsPendingReadAndIgnoresLateResponse`: “Expected token.IsCancellationRequested to be True, but found False.” Await actual renderer disposal through `DisposeComponentsAsync`; rendered-wrapper disposal alone does not dispose component.

Final run: 142 passed. No assertion weakened; no production, migration, project, package, spec, plan, or shared E2E harness edits during Test.

## Next stage

Knowledge graph updated after test edits: 10,515 nodes, 26,313 edges, 535 communities. AST-only; SQL extraction remains unavailable because `tree_sitter_sql` is missing. 34 configuration/manifest files emitted no graph nodes. Direct PostgreSQL tests provide SQL evidence above. `git diff --check` passes.

`$theshop-e2e 002_variant-filters`

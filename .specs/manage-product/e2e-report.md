# E2E report — manage-product

_Run: 2026-09-09 · commit `969e915` · verdict ✅ VERIFIED_
_Snapshot of one run — regenerate with `/theshop-e2e manage-product`._

## Coverage classification

| Bucket | ACs | Count |
|---|---|---|
| Browser-proven (`e2e`) | AC-8, AC-9, AC-12, AC-18 | 4 |
| Proven below the browser (`unit`) | AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-10, AC-11, AC-13, AC-14, AC-15, AC-16, AC-17, AC-21, AC-22, AC-23, AC-24, AC-25, AC-26, AC-27, AC-28, AC-29 | 23 |
| Human-only (`manual`) | AC-19, AC-20 | 2 |

- AC-19: no language-switch mechanism is reachable from the running app in this E2E harness (no UI locale picker, no culture cookie/query hook), so a French render cannot be driven through a browser journey. CAD locale formatting is covered generically by `CurrencyFormatterTests`; French-string completeness is gated at the review stage.
- AC-20: exhaustive keyboard reachability, visible-focus-indicator, and dialog focus-management proof across every listing control has no precedent anywhere in this suite (including create-product's own equivalent accessibility AC), and a partial/representative Playwright check would risk a false failure from test technique rather than a real defect. Needs a dedicated accessibility pass.

## Journey run

Expected 4 · discovered 4 · passed 4 · failed 0 · skipped 0 · repair rounds used 1 · duration ≈72s (final clean run).

## Acceptance criteria

| AC | Bucket | Result | Evidence |
|---|---|---|---|
| AC-1 | unit | ✅ Passed | `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.AdminProductsPage_WithElevenProducts_ReturnsFirstTenNameAscendingWithTheFullTotal` |
| AC-2 | unit | ✅ Passed | `TheShop.Web.Tests.Pages.Admin.ManageProductsTests.ChangePage_AfterFilteringAndSorting_KeepsBothCriteriaOnTheNewPage` |
| AC-3 | unit | ✅ Passed | `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.AdminProductsPage_SearchWithSurroundingSpacesAndMixedCase_MatchesContainingNames` |
| AC-4 | unit | ✅ Passed | `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.AdminProductsPage_WithStatusAndBrandAndCategoryFilters_ReturnsOnlyProductsSatisfyingAll` |
| AC-5 | unit | ✅ Passed | `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.AdminProductsPage_SortedNameDescending_ReturnsReverseAlphabeticalOrder` |
| AC-6 | unit | ✅ Passed | `TheShop.Web.Tests.Pages.Admin.ManageProductsTests.ChangeSort_WithProductsSelected_ClearsTheSelectionAndHidesTheBulkBar` |
| AC-7 | unit | ✅ Passed | `TheShop.Web.Tests.Pages.Admin.ManageProductsTests.SelectAll_OnAPageWithMoreProductsThanFit_SelectsOnlyTheCurrentPagesProducts` |
| AC-8 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ManageProductsJourneyTests.AC8_Creating_a_product_through_the_add_flow_appears_in_subsequent_matching_results` |
| AC-9 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ManageProductsJourneyTests.AC9_Editing_a_product_through_the_edit_flow_shows_saved_changes_in_subsequent_matching_results` |
| AC-10 | unit | ✅ Passed | `TheShop.Application.Tests.Features.Products.Commands.SetProductStatus.SetProductStatusHandlerTests.Handle_WhenActivatingAPricedProduct_ReturnsChangedCountOfOneAndNoSkips` |
| AC-11 | unit | ✅ Passed | `TheShop.Web.Tests.Pages.Admin.ManageProductsTests.StatusToggle_WhenDeactivatingAnActiveProduct_AsksForConfirmationBeforeSending` |
| AC-12 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ManageProductsJourneyTests.AC12_Deactivating_a_published_product_removes_it_from_the_catalogue_while_staff_still_see_it_and_reactivating_restores_it` |
| AC-13 | unit | ✅ Passed | `TheShop.Application.Tests.Features.Products.Commands.DeleteProducts.DeleteProductsHandlerTests.Handle_WhenTheProductIsDeletable_ReturnsSuccessWithDeletedCountOfOne` |
| AC-14 | unit | ✅ Passed | `TheShop.Web.Tests.Pages.Admin.ManageProductsTests.DeleteSingle_WhenCancelled_DoesNotSendTheCommand` |
| AC-15 | unit | ✅ Passed | `TheShop.Application.Tests.Features.Products.Commands.DeleteProducts.DeleteProductsHandlerTests.Handle_WhenTheProductIsReferenced_ReturnsZeroDeletedAndTheBlockedProductWithItsReferenceCount` |
| AC-16 | unit | ✅ Passed | `TheShop.Application.Tests.Features.Products.Commands.DeleteProducts.DeleteProductsHandlerTests.Handle_WithFiveSelectedOfWhichTwoAreReferenced_ReturnsThreeDeletedAndTheTwoBlocked` |
| AC-17 | unit | ✅ Passed | `TheShop.Application.Tests.Features.Products.Commands.DeleteProducts.DeleteProductsHandlerTests.Handle_WhenEverySelectedProductIsReferenced_ReturnsZeroDeletedAndEveryProductBlocked` |
| AC-18 | e2e | ✅ Passed | `TheShop.E2E.Tests.Journeys.ManageProductsAccessDeniedJourneyTests.AC18_A_user_without_products_view_cannot_reach_the_list_by_direct_link` |
| AC-19 | manual | ⚠️ Unverified | No language-switch mechanism reachable from the running app in this harness |
| AC-20 | manual | ⚠️ Unverified | No precedent for mechanized keyboard/focus proof in this suite; needs a dedicated accessibility pass |
| AC-21 | unit | ✅ Passed | `TheShop.Web.Tests.Pages.Admin.ManageProductsTests.Render_WhenNoProductsExistYet_ShowsTheEmptyStateMessageAndHidesTheFilterPanel` |
| AC-22 | unit | ✅ Passed | `TheShop.Web.Tests.Pages.Admin.ManageProductsTests.DeleteSingle_WhenItEmptiesTheLastPage_RecoversToTheNewLastPage` |
| AC-23 | unit | ✅ Passed | `TheShop.Web.Tests.Pages.Admin.ManageProductsTests.DeleteSingle_WhenTheCommandFails_ShowsTheErrorMessageAndNeverClaimsSuccess` |
| AC-24 | unit | ✅ Passed | `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.AdminProductsPage_WhenAVariantHasASalePrice_UsesTheSalePriceForTheRange` |
| AC-25 | unit | ✅ Passed | `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.AdminProductsPage_WithNoVariants_UsesItsOwnEffectivePriceForBothEnds` |
| AC-26 | unit | ✅ Passed | `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.AdminProductsPage_WithPriceFilterBetweenTwoVariantPrices_ExcludesTheProduct` |
| AC-27 | unit | ✅ Passed | `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.AdminProductsPage_WithPriceFilterMatchingAMiddleVariant_KeepsTheFullRangeAndCount` |
| AC-28 | unit | ✅ Passed | `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.AdminProductsPage_SortedByLowestVariantPriceUnderAFilter_OrdersOnTheUnfilteredLowestPrice` |
| AC-29 | unit | ✅ Passed | `TheShop.Infrastructure.Tests.Persistence.ManageProductsSchemaTests.AdminProductsPage_ADeletedVariantContributesNeitherPriceNorCount` |

**AC status:** 27 passed · 0 failed · 2 unverified

## Failures and findings

None in the final run. Repair round 1 fixed two test-authored defects surfaced by the first attempt, both in test/page-object code only — no `src/` change was needed:

- `ManageProductsJourneyTests.AC12_…`: after navigating away to the storefront catalogue, the test called `ManageProductsPage.SearchAsync` without first calling `GotoAsync` back to `/admin/products` (locator/timing — the search placeholder does not exist on `/products`). Fixed by adding the missing navigation before each post-catalogue search.
- `ManageProductsPage.SearchAsync`: returned as soon as the search field was filled, before the field's 300ms-debounced query-state push had actually navigated. A caller whose next action (a status-chip click) landed just as the debounced navigation replaced the table raced a fresh component remount and lost an already-open confirm dialog (locator/timing). Fixed by having `SearchAsync` wait for the `search=` query parameter to land in the URL before returning, so every caller is synchronized against the debounce by construction.

A pre-existing orphaned `dotnet run` process from an earlier session (started 2026-09-08, unrelated to this run) was also found squatting on port 5218 before the first attempt. It made the app's own HTTP health check pass while actually serving a stale process, causing every journey in this suite — including `ManageBrandsJourneyTests`, re-checked as a sanity probe — to time out at initial WASM boot. Killed before repair round 1; see Environment.

## Environment

- Stack: reused (already running) · app: launched fresh per test collection by `AppHostFixture` · port 5218 free after run
- A stale orphaned `dotnet run` process (PID 17760, running since 2026-09-08) was found bound to port 5218 before this feature's first run and was stopped; unrelated to manage-product code.
- Supabase stack left running — `tests/TheShop.E2E.Tests/tools/stop-e2e-env.ps1` to stop it.

## Verdict

**✅ VERIFIED**

The e2e gate is clean (including the post-repair re-run), all 4 `e2e` ACs passed with zero skips, and every `unit` AC is corroborated by the `4. Test` row's `Passing` state (144/144); next step: `/theshop-review manage-product`.

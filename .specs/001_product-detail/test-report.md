# Product Detail — Test Report

**Feature:** `001_product-detail`
**Date:** 2026-10-22
**Commit:** `1c175fb2a15129cfb56a628382be832fc3f06783` · feature working tree includes uncommitted implementation/tests.
**Verdict:** ✅ PASSING

## Results

| Project | Expected | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| Application | 30 | 30 | 30 | 0 | 0 |
| Infrastructure | 7 | 7 | 7 | 0 | 0 |
| Web | 33 | 33 | 33 | 0 | 0 |
| **Total** | **70** | **70** | **70** | **0** | **0** |

All nine class counts and mapped method FQNs reconciled against TRX. No unrelated tests run. No production files changed during Test. No new Domain behavior exists in this feature, so no Domain test project included.

## Commands and gates

Only the three projects listed in [test-manifest.json](test-manifest.json) built. Each built successfully.

```powershell
dotnet build tests/TheShop.Application.Tests/TheShop.Application.Tests.csproj --nologo
dotnet build tests/TheShop.Infrastructure.Tests/TheShop.Infrastructure.Tests.csproj --nologo
dotnet build tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --nologo

# Run separately for each manifested project:
dotnet test {project} --filter "Feature=001_product-detail" --logger "console;verbosity=normal" --logger "trx;LogFileName=product-detail-{layer}.trx" --results-directory "$env:TEMP/product-detail-test-results" --nologo --no-build

pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 manifest -Feature 001_product-detail
```

Manifest gate: `[sdd-gates] manifest:001_product-detail - clean.` Initial and final runs clean. Final filtered test runs exit0. Diff whitespace check clean.

Build warnings: Application2 xUnit1051 cancellation recommendations; Infrastructure50 package/obsolete-constructor/xUnit warnings, including four in the new visibility fixture; Web2 existing AngleSharp NU1902 package warnings. No package/project changes.

## Test inventory

Theory cases count individually. Fixture IDs, timestamps, file storage and application dependencies deterministic. Infrastructure uses isolated disposable PostgreSQL Testcontainers and the existing RBAC harness; no external database or product network calls. Child migrations0022/0029 applied from repository files. Parent visibility policies mirror0002/0023 with a minimal parent table. Tests verify database visibility contracts; they do not call the Supabase repository or replace PostgREST/browser coverage.

| Class FQN | File | Cases |
|---|---|---:|
| `TheShop.Web.Tests.Pages.Products.ProductDetailTests` | `tests/TheShop.Web.Tests/Pages/Products/ProductDetailTests.cs` | 19 |
| `TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests` | `tests/TheShop.Web.Tests/Components/Common/ShopSwipeCarouselTests.cs` | 12 |
| `TheShop.Web.Tests.Components.Products.ProductDetailCardTests` | `tests/TheShop.Web.Tests/Components/Products/ProductDetailCardTests.cs` | 2 |
| `TheShop.Infrastructure.Tests.Persistence.ProductDetailImageMapperTests` | `tests/TheShop.Infrastructure.Tests/Persistence/ProductDetailImageMapperTests.cs` | 4 |
| `TheShop.Infrastructure.Tests.Persistence.ProductDetailVisibilityTests` | `tests/TheShop.Infrastructure.Tests/Persistence/ProductDetailVisibilityTests.cs` | 3 |
| `TheShop.Application.Tests.Features.Products.ProductDetailVariantResolverTests` | `tests/TheShop.Application.Tests/Features/Products/ProductDetailVariantResolverTests.cs` | 10 |
| `TheShop.Application.Tests.Features.Products.Mappers.ProductDetailDtoMapperTests` | `tests/TheShop.Application.Tests/Features/Products/Mappers/ProductDetailDtoMapperTests.cs` | 14 |
| `TheShop.Application.Tests.Features.Products.Queries.GetProductDetailHandlerTests` | `tests/TheShop.Application.Tests/Features/Products/Queries/GetProductDetailHandlerTests.cs` | 4 |
| `TheShop.Application.Tests.Features.Products.Queries.GetProductDetailQueryValidatorTests` | `tests/TheShop.Application.Tests/Features/Products/Queries/GetProductDetailQueryValidatorTests.cs` | 2 |

## AC evidence and browser boundaries

Every AC classified. Listed methods pass. Browser portions remain unverified by this Test stage; AC-25 is entirely browser-owned. JS interop doubles assert commands/state only, never real scrolling, focus, animation, hover, layout or accessibility announcements.

Full method FQNs below also appear in the manifest.

| AC | Passing unit/component/integration evidence | Required E2E boundary |
|---|---|---|
| AC-1 | `TheShop.Application.Tests.Features.Products.Queries.GetProductDetailHandlerTests.Handle_PublishedProduct_ReturnsPublicDetailAndPropagatesCancellation`<br>`TheShop.Application.Tests.Features.Products.Mappers.ProductDetailDtoMapperTests.ToDto_VariantProductWithoutOwnPrice_UsesEffectiveVariantRangeAndPinnedOrPrimaryImages`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_PublicProduct_StartsUnselectedWithRangeAndPrimaryFirst` | Desktop/narrow visibility, geometry and primary image at real breakpoints. |
| AC-2 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_PublicProduct_StartsUnselectedWithRangeAndPrimaryFirst`<br>`TheShop.Infrastructure.Tests.Persistence.ProductDetailVisibilityTests.Read_GuestOrCustomer_SeesOnlyPublishedProductAndEveryChild` | Guest/customer route and PostgREST session parity; unit page has no authentication dependency. |
| AC-3 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_PublicProduct_StartsUnselectedWithRangeAndPrimaryFirst`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.ToggleGallery_ExpandedThenCollapsed_PreservesSelectionAndPrice` | CSS first-four visibility and native focus behavior. |
| AC-4 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Browse_ManualImageChange_PreservesVariantPriceAndChoices`<br>`TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests.Navigate_ValidSettledIndex_ReportsOnlyIndexWithoutMutatingItems` | Real swipe/scroll settling, indicator synchronization and keyboard navigation. |
| AC-5 | `TheShop.Application.Tests.Features.Products.ProductDetailVariantResolverTests.Resolve_CompleteConfiguredChoices_ReturnsExactVariantRegardlessOfOrder`<br>`TheShop.Application.Tests.Features.Products.ProductDetailVariantResolverTests.Resolve_IncompleteDuplicateUnknownOrUnconfiguredChoices_DoesNotSelectVariant`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Select_PartialThenCompleteCombination_KeepsRangeUntilExactPriceAndImageResolve` | Real breakpoint response and viewport-dependent reveal; incomplete grouped selection preserves current image. |
| AC-6 | `TheShop.Application.Tests.Features.Products.Mappers.ProductDetailDtoMapperTests.ToDto_StalePinnedImage_FallsBackToPrimary`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Select_PartialThenCompleteCombination_KeepsRangeUntilExactPriceAndImageResolve` | Actual viewport image navigation. |
| AC-7 | `TheShop.Application.Tests.Features.Products.Mappers.ProductDetailDtoMapperTests.ToDto_NoVariants_UsesProductSalePrice`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Actions_VariantFreeProduct_AreEnabledAndHaveNoSideEffects` | Native pointer/keyboard activation and absence of persisted bag changes. |
| AC-8 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Disclosures_ExpandIndependently_AndStartCollapsed` | Keyboard activation and focus. |
| AC-9 | `TheShop.Application.Tests.Features.Products.Mappers.ProductDetailDtoMapperTests.ToDto_EmptyOrUnsafeLegacyDescription_OmitsContent`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_AbsentOptionalContent_OmitsOnlyEmptySection` | Browser layout after omitted sections. |
| AC-10 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Actions_VariantFreeProduct_AreEnabledAndHaveNoSideEffects`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.AddToBag_IncompleteSelection_RequestsAttentionThenCompleteSelectionClearsIt` | Favourite before/after selection; no persistence, navigation or success UI in a real session. |
| AC-11 | `TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests.Render_MultipleImages_UsesSharedImagesAndPerceivablePosition`<br>`TheShop.Web.Tests.Components.Products.ProductDetailCardTests.Render_LinkedTile_PreservesNativeDestinationAndSeparateActions`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Disclosures_ExpandIndependently_AndStartCollapsed` | Visible focus, native keyboard operation, screen-reader announcement and all control names. |
| AC-12 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_AtMostFourImages_OmitsGalleryToggle`<br>`TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests.Render_NoNavigation_OmitsIndicatorAndKeyboardStop` | Real single-slide geometry and absence of scrollable overflow. |
| AC-13 | `TheShop.Application.Tests.Features.Products.Mappers.ProductDetailDtoMapperTests.ToDto_NoPrimary_UsesFirstImageByPositionThenIdentity`<br>`TheShop.Application.Tests.Features.Products.Mappers.ProductDetailDtoMapperTests.ToDto_EmptyGallery_DoesNotInventImageOrVariantTarget`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_FailedGalleryImage_ShowsProductNamePlaceholder` | Subtitle1 computed typography and native failed network image event. |
| AC-14 | `TheShop.Application.Tests.Features.Products.Queries.GetProductDetailHandlerTests.Handle_MissingOrUnpublishedProduct_ReturnsSameUnavailableKey`<br>`TheShop.Application.Tests.Features.Products.Queries.GetProductDetailQueryValidatorTests.Validate_EmptyId_ReturnsUnavailableFieldFailure`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_MissingProductAfterNavigation_ClearsDetailsAndVariants`<br>`TheShop.Infrastructure.Tests.Persistence.ProductDetailVisibilityTests.Read_GuestOrCustomer_SeesOnlyPublishedProductAndEveryChild`<br>`TheShop.Infrastructure.Tests.Persistence.ProductDetailVisibilityTests.Read_PrivilegedStaff_PublicationFilterStillExcludesUnpublishedParent` | Repository PostgREST publication predicate with staff/guest/customer sessions; route races and no stale responses. |
| AC-15 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.AddToBag_IncompleteSelection_RequestsAttentionThenCompleteSelectionClearsIt` | Focus, viewport reveal, 300ms repeated shake, announcement, no visible error. |
| AC-16 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.AddToBag_IncompleteSelection_RequestsAttentionThenCompleteSelectionClearsIt` | Browser removal of static outline and pending animation. |
| AC-17 | `TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests.Render_MultipleImages_UsesSharedImagesAndPerceivablePosition`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_PublicProduct_StartsUnselectedWithRangeAndPrimaryFirst` | Horizontal reachability and responsive one-image layout. |
| AC-18 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.ToggleGallery_ExpandedThenCollapsed_PreservesSelectionAndPrice` | Real grid collapse, preserved keyboard focus and responsive visibility. |
| AC-19 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Select_DesktopHiddenTarget_ExpandsWithoutReorderingAndRequestsReveal` | Partially/fully visible target must not scroll or highlight; JS visibility is browser-owned. |
| AC-20 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Select_DesktopHiddenTarget_ExpandsWithoutReorderingAndRequestsReveal` | Hidden target reveal scrolling only when outside viewport; no highlight. |
| AC-21 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_AtMostFourImages_OmitsGalleryToggle` | Desktop CSS and no false navigation. |
| AC-22 | `TheShop.Application.Tests.Features.Products.Mappers.ProductDetailDtoMapperTests.ToDto_EqualEffectivePrices_ProvidesEqualRangeEndpoints`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_EqualVariantPrices_ShowsSingleAmountWithoutSelection` | Responsive display. |
| AC-23 | `TheShop.Application.Tests.Features.Products.Mappers.ProductDetailDtoMapperTests.ToDto_VariantProductWithoutOwnPrice_UsesEffectiveVariantRangeAndPinnedOrPrimaryImages`<br>`TheShop.Web.Tests.Pages.Products.ProductDetailTests.Select_PartialThenCompleteCombination_KeepsRangeUntilExactPriceAndImageResolve` | Responsive display and image targeting. |
| AC-24 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.AddToBag_IncompleteSelection_RequestsAttentionThenCompleteSelectionClearsIt` | prefers-reduced-motion disables shake and applies focus/static outline. |
| AC-25 | None; browser-only criterion. | Entire criterion requires real layout: natural scrolling, end-of-content sticky offset, 24px clearance, header, containment, resizing and narrow static scrolling. |
| AC-26 | `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_PendingRequest_ShowsNamedStaticSkeletonWithoutControls` | Figma skeleton geometry across desktop/tablet/phone. |
| AC-27 | `TheShop.Infrastructure.Tests.Persistence.ProductDetailImageMapperTests.ToDomain_GalleryPrimary_TakesPrecedenceOverPositionAndLegacy`<br>`TheShop.Infrastructure.Tests.Persistence.ProductDetailImageMapperTests.ToDomain_GalleryWithoutPrimary_UsesPositionThenIdentity`<br>`TheShop.Infrastructure.Tests.Persistence.ProductDetailImageMapperTests.ToDomain_NoGallery_RetainsLegacyThumbnail`<br>`TheShop.Application.Tests.Features.Products.Mappers.ProductDetailDtoMapperTests.ToDto_PopulatedContent_PreservesSafeHtmlAndSpecificationOrder` | Actual catalogue image rendering, body1 typography, text color, 8px desktop/0 narrow gap. |
| AC-28 | `TheShop.Web.Tests.Components.Products.ProductDetailCardTests.Render_LinkedTile_PreservesNativeDestinationAndSeparateActions`<br>`TheShop.Web.Tests.Components.Products.ProductDetailCardTests.Render_LoadingTile_HasNoNavigationOrActions` | Computed typography, 1px hover outline at 8px, hit testing across whole tile, native modified clicks, keyboard focus and touch. |

## Failures and repairs

Final failures: none. Final skips: none. Count drift: none. No product defect found by these tests.

One Application compile repair: explicit `new GetProductDetailQuery(...)` disambiguates FluentValidation `ValidateAsync` overloads. Original compiler output: `error CS0121: The call is ambiguous between ... ValidateAsync(GetProductDetailQuery, CancellationToken) ... ValidateAsync(ValidationContext<GetProductDetailQuery>, CancellationToken)`. Rebuild and30 filtered cases pass.

One Web test-setup repair: bUnit rejects `Setup<IJSObjectReference>` for returned controller objects. Initial run:33discovered,2passed,31failed before component execution. Replaced invalid setup with injected NSubstitute `IJSRuntime`/module/controller doubles. No assertions removed or weakened. Rebuild and33 filtered cases pass.

Initial affected FQNs (theory cases grouped):

- `TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests.Dispose_LateBrowserCallback_DoesNotNotifyCaller`
- `TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests.Navigate_InvalidUnchangedOrStaticIndex_IgnoresCallback`
- `TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests.Navigate_ValidSettledIndex_ReportsOnlyIndexWithoutMutatingItems`
- `TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests.Render_MultipleImages_UsesSharedImagesAndPerceivablePosition`
- `TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests.Render_NoNavigation_OmitsIndicatorAndKeyboardStop`
- `TheShop.Web.Tests.Components.Common.ShopSwipeCarouselTests.Render_OutOfRangeIndex_ClampsAndReportsBoundary`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Actions_VariantFreeProduct_AreEnabledAndHaveNoSideEffects`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.AddToBag_IncompleteSelection_RequestsAttentionThenCompleteSelectionClearsIt`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Browse_ManualImageChange_PreservesVariantPriceAndChoices`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Disclosures_ExpandIndependently_AndStartCollapsed`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_AbsentOptionalContent_OmitsOnlyEmptySection`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_AtMostFourImages_OmitsGalleryToggle`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_EqualVariantPrices_ShowsSingleAmountWithoutSelection`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_FailedGalleryImage_ShowsProductNamePlaceholder`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_MissingProductAfterNavigation_ClearsDetailsAndVariants`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_PendingRequest_ShowsNamedStaticSkeletonWithoutControls`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_PublicProduct_StartsUnselectedWithRangeAndPrimaryFirst`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Render_UnpricedProduct_ShowsUnavailableInsteadOfZero`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Resize_ToNarrowGallery_UsesSelectedTargetAndIgnoresOldGeneration`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Select_DesktopHiddenTarget_ExpandsWithoutReorderingAndRequestsReveal`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.Select_PartialThenCompleteCombination_KeepsRangeUntilExactPriceAndImageResolve`
- `TheShop.Web.Tests.Pages.Products.ProductDetailTests.ToggleGallery_ExpandedThenCollapsed_PreservesSelectionAndPrice`

Exact shared output:

```text
System.ArgumentException : Use one of the SetupModule() methods instead to set up an invocation handler that returns an IJSObjectReference.
```

## Run artifacts

TRX files in local temporary storage (counts/FQNs copied into this report; artifacts may expire):

- `C:\Users\vaibh\AppData\Local\Temp\product-detail-test-results\product-detail-Application.trx`
- `C:\Users\vaibh\AppData\Local\Temp\product-detail-test-results\product-detail-Infrastructure.trx`
- `C:\Users\vaibh\AppData\Local\Temp\product-detail-test-results\product-detail-Web.trx`

Console/build logs: `$env:TEMP/product-detail-{Application,Infrastructure,Web}-{test,build}.log`. Initial Web failure retained as `product-detail-Web-test-initial.log` and `product-detail-Web-initial.trx`.

Graph update completed after test code changes:10383nodes,25469edges,642communities; AST-only, no API cost. Tool limitations:47 JSON/config/evidence inputs produced no nodes;33 SQL files skipped because tree_sitter_sql is absent;183 community names refreshed from hubs. Database integration tests still apply the SQL directly. No screenshot/visual gate rerun: production/spec/plan unchanged by this stage. Prior Execute evidence remains separate from current automated Test results.

## Next step

`$theshop-e2e 001_product-detail` — browser journeys, responsive layout, focus, motion, sticky buy box, native navigation and visual fidelity.

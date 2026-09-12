# Feature: reusable-image-treatments

State: Done
Updated: 2026-09-13

## Expected outcome

Provide a reusable MudBlazor image component whose display width is configurable and whose named treatment controls proportional height plus predictable `contain` or centered `cover` behavior.

Adopt the component in representative product and administration image surfaces so existing image presentation uses the shared treatment.

Excluded: image upload behavior, image storage or transformation, new design tokens, new image assets, and authentication/header logo presentation.

## Acceptance

| ID | Concrete example and expected result | Proof | Result / evidence |
|---|---|---|---|
| AC-1 | Given a product-card or product-gallery treatment, when an image renders, then its frame is square and the whole product remains visible with empty space allowed. | component + browser inspection | Pass: `ShopImageTests`; product-card E2E; `evidence/product-catalogue.png` |
| AC-2 | Given a thumbnail treatment, when a cart, order, or admin image renders, then its frame is square and the whole image remains visible within its allocated space. | component + browser inspection | Pass: preset component theory; admin-list E2E |
| AC-3 | Given a category-tile, category-banner, desktop-hero, mobile-hero, or editorial treatment, when an image renders, then it uses the preset ratio and fills the frame with predictable centered edge cropping. | component + browser inspection | Pass: preset component theory verifies `Cover` and center; browser E2E verifies compiled ratios |
| AC-4 | Given a brand-logo treatment, when a logo renders, then it keeps its original ratio and remains fully visible within allocated space. | component + browser inspection | Pass: `BrandLogo` component case; admin brand-list E2E verifies `Contain` and original-ratio preset |
| AC-5 | Given a social-sharing treatment, when the prepared image renders, then it uses a 40:21 frame and remains fully visible without distortion. | component | Pass: `SocialSharing` component case; browser E2E verifies compiled 40:21 ratio |
| AC-6 | Given a caller supplies a valid CSS display width, when any preset renders, then the frame uses that width and derives its height from the preset ratio where applicable. | component + browser inspection | Pass: custom/default width component tests; product-card and compiled-ratio browser E2E |
| AC-7 | Given alt text, fallback source, caller class/style, or arbitrary HTML attributes, when the component renders, then it preserves those values and applies caller class/style last at the root. | component | Pass: forwarding component test |
| AC-8 | Given existing product-card and admin-list images, when those surfaces render, then product cards use `Product`, brand logos use `BrandLogo`, and category/product admin images use `Thumbnail` without changing surrounding behavior. | component + focused regression tests + browser inspection | Pass: focused Web suite, product-card E2E, admin-list E2E |

## Decisions

Expectation confirmation: Confirmed by user on 2026-09-13.

Confirmed component API: `ShopImage` with `Src`, `Alt`, optional `FallbackSrc`, CSS-length `DisplayWidth`, a required `ShopImageTreatment` preset, and inherited `Class`, `Style`, and `UserAttributes`.

Confirmed presets:

| Preset | Ratio | Fit |
|---|---:|---|
| `Product` | 1:1 | Contain |
| `Thumbnail` | 1:1 | Contain |
| `CategoryTile` | 1:1 | Cover |
| `CategoryBanner` | 16:5 | Cover |
| `HeroDesktop` | 16:9 | Cover |
| `HeroMobile` | 4:5 | Cover |
| `Editorial` | 4:3 | Cover |
| `BrandLogo` | Original | Contain |
| `SocialSharing` | 40:21 | Contain |

For `HeroMobile`, the caller supplies the dedicated mobile-crop source while selecting the mobile preset. Responsive source switching remains outside this component unless separately confirmed.

Confirmed adoption: product cards plus brand, category, and product administration list images. Upload previews and authentication/header logo imagery remain unchanged.

## Implementation

- [x] Add the reusable Web component and shared SCSS treatment.
- [x] Adopt it in confirmed existing image call sites.
- [x] Add bUnit coverage for every preset, custom display width, crop behavior, fallback/accessibility values, and attribute forwarding.
- [x] Update affected call-site and E2E tests.
- [x] Format owned files, build, run focused tests, run the pilot design checker, inspect the running UI, self-review, and refresh Graphify.

## Verification

Revision / dirty files: `87ee04e30fb9` on branch `refactor/sdd-flow`, verified with uncommitted feature files at 2026-09-13T00:58:39+05:30. Pre-existing staged pilot additions and legacy `.specs/shop-image/*` deletions remain untouched.

| Check / exact command | Result / exit code / test counts | Evidence |
|---|---|---|
| `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --nologo --no-build --no-restore --filter "FullyQualifiedName~ProductCardTests\|FullyQualifiedName~ManageBrandsTests\|FullyQualifiedName~ManageCategoriesTests\|FullyQualifiedName~ManageProductsTests"` | Pass, exit 0; 139 passed, 0 failed, 0 skipped | Baseline before behavior edits |
| `dotnet format TheShop.slnx --no-restore --include src/TheShop.Web/Components/Common/ShopImage.razor src/TheShop.Web/Components/Common/ShopImage.razor.cs src/TheShop.Web/Components/Common/ShopImageTreatment.cs src/TheShop.Web/Components/Products/ProductCard.razor src/TheShop.Web/Pages/Admin/ManageBrands.razor src/TheShop.Web/Pages/Admin/ManageCategories.razor src/TheShop.Web/Pages/Admin/ManageProducts.razor tests/TheShop.Web.Tests/Components/Common/ShopImageTests.cs tests/TheShop.Web.Tests/Components/Products/ProductCardTests.cs tests/TheShop.Web.Tests/Pages/Admin/ManageBrandsTests.cs tests/TheShop.Web.Tests/Pages/Admin/ManageCategoriesTests.cs tests/TheShop.Web.Tests/Pages/Admin/ManageProductsTests.cs tests/TheShop.E2E.Tests/Fixtures/AppHostFixture.cs tests/TheShop.E2E.Tests/Journeys/CatalogueJourneyTests.cs tests/TheShop.E2E.Tests/Journeys/ManageProductsJourneyTests.cs tests/TheShop.E2E.Tests/Pages/CataloguePage.cs tests/TheShop.E2E.Tests/Pages/Admin/ManageBrandsPage.cs tests/TheShop.E2E.Tests/Pages/Admin/ManageCategoriesPage.cs tests/TheShop.E2E.Tests/Pages/Admin/ManageProductsPage.cs` | Pass, exit 0 | Final invocation covered every feature-owned C#/Razor file |
| `dotnet build TheShop.slnx --nologo` | Pass, exit 0; 0 errors, 4 existing dependency-vulnerability warnings | Final formatted sources |
| `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --nologo --no-build --no-restore --filter "FullyQualifiedName~ShopImageTests\|FullyQualifiedName~ProductCardTests\|FullyQualifiedName~ManageBrandsTests\|FullyQualifiedName~ManageCategoriesTests\|FullyQualifiedName~ManageProductsTests"` | Pass, exit 0; 152 passed, 0 failed, 0 skipped | Component and affected-page regression proof |
| `dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --nologo --no-build --no-restore --filter "Feature=reusable-image-treatments"` | Pass, exit 0; 3 passed, 0 failed, 0 skipped | Product frame, every compiled ratio, and all adopted admin surfaces |
| `pwsh -NoProfile -Command "& { & '.sdd-next/scripts/check-design-rules.ps1' -Path @('src/TheShop.Web/Components/Common/ShopImage.razor','src/TheShop.Web/Components/Common/ShopImage.razor.cs','src/TheShop.Web/Components/Common/ShopImageTreatment.cs','src/TheShop.Web/Components/Products/ProductCard.razor','src/TheShop.Web/Pages/Admin/ManageBrands.razor','src/TheShop.Web/Pages/Admin/ManageCategories.razor','src/TheShop.Web/Pages/Admin/ManageProducts.razor') }"` | Pass, exit 0; no violations | Pilot design checker |
| Browser screenshot inspection | Pass: square frame, full controlled image visible, empty space retained, actions remain positioned | `evidence/product-catalogue.png` |
| `git diff --check` | Pass, exit 0 | No whitespace errors |
| `graphify update .` | Pass, exit 0; 12,054 nodes and 28,080 edges | Derived project graph refreshed |

Review: Self-review passed against pilot architecture, UI/resource, styling, test, and documentation rules. The component uses only MudBlazor primitives, forwards root attributes, keeps caller class/style last, uses shared SCSS, adds no resource keys, and changes no security or persistence behavior. Browser proof uses a controlled local image because seeded external placeholder images are unavailable in the isolated environment. Existing `AngleSharp` and `SSH.NET` vulnerability warnings remain outside this feature. E2E hosting now uses already-built output with `--no-build --no-restore`, avoiding unauthorized user-profile NuGet access.

## Resume / delivery

Completed: Added nine confirmed image presets, flexible display width, shared contain/cover behavior, product/admin adoption, component regression tests, browser proof, and refreshed graph.

Remaining: None.

Blocker / next action: None.

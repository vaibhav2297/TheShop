# Shop Image — Test Report

**Feature:** `shop-image`
**Date:** 2026-09-30
**Commit:** `0a81859` + uncommitted Implement/Test changes on `feature/shop-image`

## Run

- Project: `tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj`
- Filter: `Feature=shop-image`
- Command: `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --filter "Feature=shop-image" --logger "console;verbosity=normal" --nologo --no-build`
- Manifest gate: `check-sdd-gates.ps1 manifest -Feature shop-image` clean.

| Expected | Discovered | Passed | Failed | Skipped |
|---|---|---|---|---|
| 64 | 64 | 64 | 0 | 0 |

Regression check on touched classes (no trait filter, not full suite): 318 passed, 0 failed, 0 skipped across ProductCard, ShopFooter, ShopImageUpload, ShopImage, ManageBrands/Categories/Products, EditBrand, EditCategory, AddBrand, ProductForm, ProductVariantsCard, SignIn, SignUp tests.

## Classes

| Class | Tests |
|---|---|
| `Components.Common.ShopImageTests` (new) | 48 |
| `Components.Common.ShopImageUploadTests` | 3 |
| `Components.Common.ShopFooterTests` | 1 |
| `Components.Products.ProductCardTests` | 4 |
| `Pages.Admin.ManageBrandsTests` | 3 |
| `Pages.Admin.ManageCategoriesTests` | 2 |
| `Pages.Admin.ManageProductsTests` | 2 |
| `Pages.Admin.EditBrandTests` | 1 |

## AC → evidence

Component tests prove preset class, fit, branch source, placeholder, semantics, and forwarding. Rendered geometry (ratios, crops, overflow, breakpoint switch, focus ring, real network failure, denied page) is marked `e2e` in the manifest.

| AC | Component evidence | Browser boundary |
|---|---|---|
| AC-1 | preset class; contain + center fit; ProductCard uses `ProductCard` preset | 1:1 frame, tall/wide sources whole |
| AC-2 | admin product/category thumbnails and upload previews use `Thumbnail`, contain | 1:1 inside row space |
| AC-3 | photography presets cover + center; source URL unchanged | centered 1:1 crop |
| AC-4 | banner/hero preset classes; cover + center | 16:5 and 16:9 frames |
| AC-5 | desktop/mobile branch sources; branch failure isolation | 4:5 branch shown below 600 px |
| AC-6 | mobile branch reuses desktop URL; non-responsive presets ignore `MobileSrc` | centered 4:5 crop |
| AC-7 | editorial preset class; cover + center | 4:3 frame |
| AC-8 | `BrandLogo` contain in avatar, edit page (120×120), upload preview, footer (120×120); placeholder keeps reserved space | wide/tall logos whole at varying space |
| AC-9 | social preset class; contain | 40:21 frame |
| AC-10 | preset classes; Class/Style/attribute forwarding | 375/768/1440 px, orientation resize |
| AC-11 | blank/failed source placeholder in unchanged frame; stale failure ignored; replacement recovers; branch isolation; JS observe/unobserve; callers show names | real network/cached failure; adjacent positions stable |
| AC-12 | meaningful alt; decorative empty alt; placeholder `role="img"` + label or `aria-hidden` | — |
| AC-13 | no focusable elements added; wishlist callback; footer logo link keeps href + name; remove actions remain | visible focus, keyboard activation |
| AC-14 | Manage Brands/Categories keep view policies; `ShopImage` carries no authorization | guest/customer redirect and denied page |

## Changes to existing tests

- `ProductCardTests.Render_WithoutImageUrl_ShowsPlaceholderImage` (trait `product-catalogue`): assertion changed from the storefront-logo `src` to the named placeholder. shop-image FR-5 supersedes the logo fallback; name kept so the product-catalogue manifest mapping stays valid.

## Repairs during run

Two test defects fixed once, no production change:

1. `ShopImageUploadTests.Render_ByDefault_…` expected `width: 48px`; `StyleBuilder` emits `width:48px`.
2. `ShopImageTests.Render_WithAMissingDecorativeImage_…` expected no `role`; MudStack always emits `role="group"`. The test now asserts the role is not `img` and there is no `aria-label`; the element stays `aria-hidden="true"`.

## Failures

None.

## Verdict

✅ PASSING — non-browser coverage complete. Rendered geometry, breakpoint, focus, and denied-access journeys still need `$theshop-e2e shop-image`.

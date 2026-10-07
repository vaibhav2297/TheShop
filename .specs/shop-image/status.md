# Shop Image — SDD Status

**Feature:** `shop-image`
**Last updated:** 2026-09-30

| Stage | State | Gate | Evidence | Date |
|---|---|---|---|---|
| 1. Spec       | Confirmed | ✅ spec-gate pass | 7 FRs · 14 ACs · 3 resolved · 0 open assumption(s) | 2026-09-11 |
| 2. Plan       | Resolved | ✅ plan-gate pass | 14 ACs mapped · 6 tasks · 2 assumptions resolved · 0 open · 1 accepted risk | 2026-09-30 |
| 3. Implement  | Done | ✅ solution build + layer scope gates | Domain/Application/Infrastructure skipped (no impact) · Web: ShopImage + ShopImagePreset + _image.scss + shopImage.js; ProductCard, ManageProducts/Brands/Categories, EditBrand/EditCategory, ShopImageUpload (PreviewPreset), VariantImageDialog, app bar/footer/auth logos · web build ✅ · scope:web clean · design-rules clean · solution build 0 errors · deviations: TASK-001–005 test/browser-geometry parts deferred to Test; AddBrand Rule 17 Label→Placeholder fixed · no Figma, no visual evidence required | 2026-09-30 |
| 4. Test       | Passing | ✅ manifest + filtered tests pass | [test-report.md](test-report.md) · 65/65 `Feature=shop-image` · 0 failed · 0 skipped · 14 ACs mapped; geometry/focus/denied journeys marked e2e | 2026-09-30 |
| 5. Verify     | Verified | ✅ e2e + visual gates clean · 14/14 journeys pass | [e2e-report.md](e2e-report.md) · 14 e2e ACs · 0 failed · 0 skipped · AC-13 product defect (ShopImage root `role="group"`) fixed in src + bUnit guard · AddBrand Rule 17 fixed | 2026-09-30 |
| 6. Review     | — | — | — | — |
| 7. Document   | — | — | — | — |

**Amendment 2026-10-08:** presets consolidated 10 → 8, named by geometry/fit: `ProductCard`/`ProductDetail`/`Thumbnail` → `SquareContain`; `CategoryTile` → `SquareCover`; `CategoryBanner` → `Banner`; `MobileBanner` → `PortraitCover`. Spec FR-1, plan, code, tests updated. Build 0 errors · 61/61 `Feature=shop-image` unit (4 duplicate theory rows removed) · E2E 6/14 pass; 8 blocked by local Supabase not running (`127.0.0.1:54324` refused) — rerun pending.

**Next step:** optional `$theshop-document shop-image`, otherwise `$theshop-ship shop-image`

**Shipped:** 2026-09-30 → dev (via PR)

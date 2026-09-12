# Test report — product-description

_Run: 2026-09-11 · commit `1b51324` · verdict ✅ Ready for E2E — deferred proof remains_
_Snapshot of one run — regenerate with `/theshop-test-merged product-description`._

## Tests written

- `tests/TheShop.Domain.Tests/Entities/ProductSpecificationTests.cs` — 9 cases (new)
- `tests/TheShop.Domain.Tests/Entities/ProductTests.cs` — 8 cases (extended; other features' cases fixed for the new `ProductDescription`/`Specifications` signatures, not counted here)
- `tests/TheShop.Domain.Tests/ValueObjects/ProductDescriptionTests.cs` — 58 cases (new)
- `tests/TheShop.Application.Tests/Features/Products/Commands/CreateProduct/CreateProductCommandValidatorTests.cs` — 18 cases (extended)
- `tests/TheShop.Application.Tests/Features/Products/Commands/CreateProduct/CreateProductHandlerTests.cs` — 2 cases (extended)
- `tests/TheShop.Application.Tests/Features/Products/Commands/UpdateProduct/UpdateProductCommandValidatorTests.cs` — 5 cases (extended)
- `tests/TheShop.Application.Tests/Features/Products/Commands/UpdateProduct/UpdateProductHandlerTests.cs` — 2 cases (extended)
- `tests/TheShop.Application.Tests/Features/Products/Mappers/AdminProductDtoMapperTests.cs` — 2 cases (extended)
- `tests/TheShop.Application.Tests/Features/Products/Queries/GetProductForEditHandlerTests.cs` — 1 case (extended)
- `tests/TheShop.Web.Tests/Components/Common/ShopRichTextEditorTests.cs` — 6 cases (new)
- `tests/TheShop.Web.Tests/Components/Products/ProductContentCardTests.cs` — 12 cases (new)
- `tests/TheShop.Web.Tests/Components/Products/ProductFormTests.cs` — 8 cases (extended)
- `tests/TheShop.Web.Tests/Resources/ProductDescriptionLocalizationTests.cs` — 18 cases (new)
- `tests/TheShop.Infrastructure.Tests/Persistence/ProductChildMapperTests.cs` — 1 case (new)
- `tests/TheShop.Infrastructure.Tests/Persistence/ProductMapperTests.cs` — 2 cases (extended)
- `tests/TheShop.Infrastructure.Tests/Persistence/SupabaseProductDescriptionSchemaTests.cs` — 24 cases (new, Testcontainers/Postgres)

**Total: 176 feature cases across 16 classes, 4 projects.**

Mechanical compile fixes only (no new assertions), required by this feature's signature changes — `Product.Create`/`UpdateDetails`/`Rehydrate` now take `ProductDescription`; `CreateProductCommand`/`UpdateProductCommand`/`AdminProductDto` gained `Specifications`: `ProductDtoMapperTests.cs`, `GetProductForEditHandlerTests.cs` ctor, `CreateProductHandlerPinTests.cs`, `UpdateProductHandlerPinTests.cs`, `GetProductCataloguePageHandlerTests.cs`, `SetProductStatusHandlerTests.cs`, `EditProductTests.cs`. Untagged, unweakened. Same assertions, one extra constructor argument.

## Tests run

| Project | Expected | Discovered | Reconciled | Passed | Failed | Skipped | Pass rate | Status |
|---|---|---|---|---|---|---|---|---|
| TheShop.Domain.Tests | 75 | 75 | ✅ | 75 | 0 | 0 | 100% | ✅ |
| TheShop.Application.Tests | 30 | 30 | ✅ | 30 | 0 | 0 | 100% | ✅ |
| TheShop.Web.Tests | 44 | 44 | ✅ | 44 | 0 | 0 | 100% | ✅ |
| TheShop.Infrastructure.Tests | 27 | 27 | ✅ | 27 | 0 | 0 | 100% | ✅ |
| **Total** | **176** | **176** | **✅** | **176** | **0** | **0** | **100%** | **✅** |

Full unfiltered project runs, confirming no regression outside this feature's trait:

- Domain: 358/358 passed.
- Application: 599/599 passed.
- Web: 787/788 passed. 1 pre-existing failure, unrelated: `RbacLocalizationTests.AdminPanelString_ForEveryKey_IsAvailableInEnglishAndFrench(key: "ManageProducts_ShellNotice")`. `ManageProducts_ShellNotice` has no English `Strings.resx` entry — gap from already-merged `manage-product` feature, present before this run, out of scope here. No fix applied (skill scope rules).
- Infrastructure: 282/282 passed. Includes the 24 `SupabaseProductDescriptionSchemaTests` above, run against a real Postgres container via Testcontainers.

## Failures and warnings

None. No skip, vacuous assertion, improper async signature, timing sleep, swallowed exception, nondeterministic time, or placeholder name in any manifest-listed file.

**Finding — plan/migration defect, not a test defect, surfaced by this suite:** plan §10's exact SQL for `products_description_markup_allowed` —
`CHECK (NOT EXISTS (SELECT 1 FROM regexp_matches(description, '<[^>]*>', 'g') AS m(tag) WHERE ...))` — is invalid PostgreSQL. Run against a real Postgres container, it throws `0A000: cannot use subquery in check constraint`. Postgres forbids a sub-`SELECT` inside a `CHECK` expression, categorically. `SupabaseProductDescriptionSchemaTests` wraps the identical predicate in a helper function (`public.description_markup_allowed(description)`) and checks `CHECK (public.description_markup_allowed(description))` instead. A `CHECK` may call a function; the restriction is on the constraint's own top-level SQL text, not on what a called function does. **Migration 0030 (`products_description_bounds`) does not exist in `supabase/migrations/` yet** — only 0029 (`product_specifications`) and 0031 (`save_product_specifications`) do. When authored, it must use the function-wrapped form, or applying it fails identically. This also means the database boundary for AC-11 — the *only* real trust boundary per plan Decision 4, since Domain/Application/Infrastructure all run client-side under Blazor WebAssembly — is not yet applied to the actual schema. Route to `/theshop-implement product-description` (or a manual migration pass) before shipping. Independent of this Test stage: every unit/component/schema test against the *planned* contract passes.

## Acceptance criteria

| AC | Status |
|---|---|
| AC-1 | ✅ Passed |
| AC-2 | ✅ Passed |
| AC-3 | ✅ Passed |
| AC-4 | ✅ Passed |
| AC-5 | ✅ Passed |
| AC-6 | ✅ Passed |
| AC-7 | ✅ Passed |
| AC-8 | ✅ Passed |
| AC-9 | ✅ Passed |
| AC-10 | 🟡 Deferred — E2E |
| AC-11 | ✅ Passed |
| AC-12 | ✅ Passed |
| AC-13 | 🟡 Deferred — E2E |
| AC-14 | ✅ Passed |
| AC-15 | ✅ Passed |

**AC status:** 13 Passed · 2 Deferred — E2E (AC-10, AC-13) · 0 Failed · 0 Not Covered.

AC-10 (pasted styled text: supported emphasis kept, unsupported styling dropped, notice shown) and AC-13 (keyboard/screen-reader operability, visible focus, AT announcements) need a real browser. bUnit's virtual DOM (AngleSharp) neither runs the JS clipboard-inspection path in `shop-rich-text-editor.js` nor renders real focus or an accessibility tree. Every mapped supporting unit/component test for both — grammar backstop, C#-side paste callback, accessible-name markup, announcement text — passed.

## Verdict

**✅ Ready for E2E — deferred proof remains**

176/176 reconciled and passing, 0 skips/warnings, 13/15 ACs Passed and 2/15 validly Deferred — E2E with every supporting test green. Next: `/theshop-e2e product-description` to close AC-10 and AC-13; separately, route the plan §10 `CHECK`-constraint defect above to implementation before the description-bounds migration is authored/shipped.

# Product Detail — Implementation Plan

Specification: [spec.md](spec.md)

## 1. Implementation Approach

**Approach:** Add a public, ID-keyed read use case that loads one published product aggregate with its ordered children and maps it to a customer-safe immutable detail DTO. Render it in a thin Blazor page using existing MudBlazor, localization, busy-state, breadcrumb, currency, image, and style conventions. Keep page-specific markup inline; add no reusable component without a second real consumer. Wire the existing catalogue card selection callback to the new centralized route.

**Affected components and boundaries:**

| Component / actual path | Change and responsibility | Applicable principle IDs |
| --- | --- | --- |
| `src/TheShop.Application/Common/Interfaces/IProductRepository.cs` | Add a cancellable public-detail read contract that returns only a published product or no result. | ARCH-01, ARCH-03, ARCH-08, DOC-30 |
| `src/TheShop.Application/Features/Products/DTOs/ProductDetailDto.cs` (proposed) | Define immutable customer-detail DTOs for product, variant, option, image, and specification data; exclude admin row-version and unnecessary SKU data. | ARCH-07, ARCH-09, DOC-30 |
| `src/TheShop.Application/Features/Products/Queries/GetProductDetail/` (proposed) | Add query and handler; map repository result to the detail DTO and return `ProductErrorKeys.NotFound` for absent/unpublished products. | ARCH-04, ARCH-05, ARCH-08, ARCH-09, TEST-29, DOC-30 |
| `src/TheShop.Infrastructure/Persistence/Repositories/SupabaseProductRepository.cs` | Load the product plus ordered images, option types/values, specifications, variants, and variant-option links. Explicitly require `is_published = true`, even for callers whose staff claims permit broader RLS reads. | ARCH-01, ARCH-03, ARCH-08, ARCH-09 |
| `src/TheShop.Web/Common/Routes.cs` | Add `ProductDetailPattern` and `ProductDetail(Guid id)` under centralized storefront routes. | WEB-21, DOC-30 |
| `src/TheShop.Web/Common/BusyKeys.cs` | Add a product-detail busy key. | WEB-22, DOC-30 |
| `src/TheShop.Web/Pages/Products/ProductDetail.razor` and `.razor.cs` (proposed) | Own route, query dispatch, presentation-only gallery/variant selection, optional accordions, inert actions, not-found state, breadcrumb, and localized page title. | ARCH-04, ARCH-10, TEXT-11, DESIGN-14–DESIGN-16, DESIGN-18, DESIGN-19, WEB-20–WEB-22, COMP-25, STYLE-26, STYLE-27 |
| `src/TheShop.Web/Pages/Products/ProductCatalogue.razor` and `.razor.cs` | Pass product-card selection into centralized detail navigation, completing the catalogue's existing deferred detail link. | ARCH-10, WEB-21 |
| `src/TheShop.Web/Resources/Strings.resx` and `Strings.fr.resx` | Add only missing product-detail labels, state text, and accessible names in both languages. | TEXT-11 |
| `src/TheShop.Web/Styles/components/_productdetail.scss` and `Styles/TheShop.scss` (proposed if utilities/components cannot express the frame) | Add centralized reusable selectors needed for the page layout; no Razor style block or page-scoped CSS. | DESIGN-15, DESIGN-18, STYLE-26–STYLE-28 |
| Matching `tests/TheShop.Application.Tests`, `tests/TheShop.Infrastructure.Tests`, `tests/TheShop.Web.Tests`, and `tests/TheShop.E2E.Tests` paths | Cover read behavior, public visibility, mapping/order, interaction, accessibility, navigation, and visual states. | TEST-29 |

**Core decisions:**

- Add a dedicated customer DTO and query instead of reusing `AdminProductDto`/`GetProductForEditQuery`; the admin contract includes edit-only fields and can load unpublished products.
- Use `Guid` route identity because the current product contract has no slug. Add both route pattern and URL builder to `Routes`.
- Keep initial selection deterministic: first available variant by saved position, otherwise first saved variant. Resolve selection from option-value IDs already stored on variants.
- Keep variant selection as Web presentation state. Domain already owns valid variant combinations, prices, pins, and availability; no new domain behavior is needed.
- Sort images defensively as primary first, then saved position. Selecting a pinned variant updates only the hero image. Manual thumbnail selection remains until another variant choice is made.
- Render description from the validated persisted HTML contract inside a MudBlazor text container. Do not decode, concatenate, or accept arbitrary client HTML.
- Keep Add to Bag and Favourite callbacks inert. Do not introduce cart, wishlist, authentication, storage, or network integration.
- Keep the page inline under COMP-25. Extract a component only if implementation reveals a second actual call site.

**Technical flow — when helpful:** Catalogue card or direct URL → `Routes.ProductDetail(id)` → `ProductDetail` dispatches `GetProductDetailQuery` through `BusyState.RunAsync` → handler calls `IProductRepository.GetPublishedDetailAsync` → Infrastructure loads published aggregate children under existing RLS → handler maps customer DTO → page initializes gallery and variant selection → user interaction changes local presentation state only. Missing/unpublished result → localized not-found state.

## 2. Data & Access Design — If Applicable

**Model / schema changes:** No schema, Domain entity, or migration change is planned. Add immutable Application read DTOs only. Reuse `products`, `product_images`, `product_option_types`, `product_option_values`, `product_variants`, `product_variant_option_values`, and `product_specifications`.

**Access and RLS:** Guests and authenticated shoppers may read only published products and their children through existing public-select policies. The repository query also filters `products.is_published = true` so a staff user's broader `products.view` policy cannot reveal unpublished data through the storefront route. No write access is added. Infrastructure verification must confirm every child table retains its published-parent condition.

**Migration and compatibility:** No migration is expected. The new route is additive. The catalogue's existing empty `OnSelect` callback gains navigation without changing filter, sort, pagination, or action-button behavior. Existing Add to Cart/Wishlist card buttons must continue to stop card selection.

**Recovery — when needed:** Revert the additive query, route, page, navigation binding, resources, styles, and tests. No data rollback is required.

## 3. Development Checklist

- [ ] Resolve spec Q-01/Q-02 or record owner decisions before affected implementation.
- [ ] Re-fetch Figma node `2263:5259`; record viewport, responsive states, accordion defaults, assets, and any conflicts or approved deviations in `spec.md` (AC-08).
- [ ] Add the published-detail repository contract, customer DTO mapping, query/handler, and focused Application/Infrastructure tests (FR-01–FR-10; AC-01–AC-04).
- [ ] Add centralized route/busy key and the product-detail Razor/code-behind page with loading, not-found, gallery, pricing, variant, availability, optional-content, localization, and accessibility states (AC-01–AC-07).
- [ ] Wire catalogue card selection to the new route while preserving independent Add to Cart/Wishlist button behavior (AC-01 and AC-05).
- [ ] Add or reuse centralized styles, resources, icons, and assets needed to match the verified Figma frame without adding custom UI primitives (AC-07 and AC-08).
- [ ] Add bUnit and Playwright coverage for direct/public access, unpublished not-found, catalogue navigation, gallery browsing, variant price/image fallback, inert actions, optional accordions, localization, and keyboard/state semantics.
- [ ] Capture matched-viewport screenshots, compare against Figma, fix discrepancies, rerun affected functional checks, and obtain developer visual review (AC-08).
- [ ] Review final diff for scope, public-data exposure, rule compliance, and unintended changes; update both SDD documents with actual evidence.
- [ ] Run `graphify update .` after code changes.

## 4. Verification

**Commands and prerequisites:** Commands below are repository-grounded from `TheShop.slnx` and test project files. Run from `D:\Vaibhav\Code\Projects\TheShop\Git-TheShop`. The E2E fixture expects `tests/TheShop.E2E.Tests/.e2e-env`, Chromium, and the app at `http://localhost:5218`; its referenced `tools/start-e2e-env.ps1` is absent and must be resolved before E2E execution. Figma MCP access is required for design fidelity.

### Behavior and appearance

| Criteria | Check | Command + working directory, or procedure | Actual result / evidence |
| --- | --- | --- | --- |
| AC-01–AC-04 | Application/Infrastructure unit and schema tests | `dotnet test tests/TheShop.Application.Tests/TheShop.Application.Tests.csproj --filter Feature=product-detail`; `dotnet test tests/TheShop.Infrastructure.Tests/TheShop.Infrastructure.Tests.csproj --filter Feature=product-detail` | Not run; tests not implemented. |
| AC-01–AC-07 | bUnit page/component tests | `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --filter Feature=product-detail` | Not run; tests not implemented. |
| AC-01–AC-07 | Playwright functional E2E | Start the local Supabase/app environment, ensure Chromium is installed, then run `dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --filter Feature=product-detail` | Blocked at planning: repository references missing `tools/start-e2e-env.ps1`. |
| AC-08 | Design fidelity | Fetch exact Figma node; compare running page screenshots for required viewport/state combinations side by side or by overlay. | Blocked: Figma MCP returned HTTP 429 on 2026-09-19. |
| AC-08 | Playwright visual regression, where useful | Add stable screenshot assertions only after Figma comparison and developer approval of the browser baseline. | Not run; baseline not created or approved. |
| AC-05–AC-07 | Manual | Verify inert controls, keyboard traversal, focus, selected/expanded/disabled semantics, gallery alt text, and EN/FR interface text. | Not run. |
| Project gates | Build and full test suite | `dotnet build TheShop.slnx --nologo`; `dotnet test TheShop.slnx --nologo` | Not run; planning only. |

### Principle compliance

| Applicable rule IDs | Check performed | Outcome and evidence / approved exception reference |
| --- | --- | --- |
| ARCH-01, ARCH-03, ARCH-04, ARCH-05, ARCH-07, ARCH-08, ARCH-09, ARCH-10 | Inspect project references, DTO/query/repository boundaries, published filtering, cancellation, error path, page dependencies, and code-behind ownership. | Unverified; implementation not started. |
| TEXT-11 | Inspect both resource files and UI/resource-key usage; verify EN/FR tests. | Unverified; implementation not started. |
| DESIGN-14, DESIGN-15, DESIGN-16, DESIGN-18, DESIGN-19 | Inspect MudBlazor use, text markup, semantic colors, typography utilities, `ShopIcons`, and rendered design. | Unverified; Figma frame unavailable. |
| WEB-20, WEB-21, WEB-22 | Inspect route attribute/helper, code-behind, busy-state wrapper/key, and loading UI. | Unverified; implementation not started. |
| COMP-25, STYLE-26, STYLE-27, STYLE-28 | Confirm page stays inline absent demonstrated reuse; inspect style priority, builders, and SCSS placement. | Unverified; implementation not started. |
| TEST-29, DOC-30 | Map every new handler/repository behavior to meaningful tests; inspect XML summaries on new public contracts. | Unverified; implementation not started. |

**UI visual review — if applicable:** Pending. Exact Figma data could not be fetched because the authorized MCP returned HTTP 429.

**Remaining failures / blockers / unverified items:** Figma node data and viewport are unavailable; Q-01 and Q-02 remain open; E2E setup references a missing script. No implementation or verification has occurred.

**Verified code state:** None; planning documents only, 2026-09-19.

**Release evidence — after shipping:** Not shipped.

## 5. Technical Assumptions & Open Questions

**Technical assumptions:**

- Existing public RLS policies on product child tables remain correct and need no migration; verify before implementation.
- `ProductDescription` plus database constraints provide the trusted supported-format contract needed for storefront rendering; verify the final rendering path cannot execute active content.
- Existing `CurrencyFormatter`, `ShopIcons.ImageAssets.LogoPrimary`, `BreadcrumbState`, `ProductErrorKeys.NotFound`, and `Strings.ProductNotFound` can be reused.
- Existing test trait filtering accepts `--filter Feature=product-detail`; confirm when the first focused test is added.

| ID | Technical question / dependency | Blocks what? | Resolution / owner |
| --- | --- | --- | --- |
| TQ-01 | What is the supported replacement for the missing `tools/start-e2e-env.ps1` referenced by `E2EEnvironment`? | Running product-detail Playwright tests and recording E2E evidence. | Pending repository/tooling resolution; does not block specification review. |
| TQ-02 | Does the current MudBlazor version expose all Figma-required gallery/variant/accordion semantics without a custom primitive? | Final control composition and any principle-conflict decision. | Resolve after Figma access and API inspection; no custom primitive is authorized. |


# Implementation Plan — Shop Image

## 1. Objective

Introduce `ShopImage` for ten confirmed treatments, responsive frames, and named placeholders. Reuse MudBlazor rendering and existing storage URLs. Isolate placeholder-provider details from Web; preserve existing DTOs, access gates, and upload behavior.

## 2. Tech Stack

- **Application:** C# provider-neutral URL contract; existing MediatR flows unchanged.
- **Infrastructure:** Pure URI construction for existing Placehold service; existing DI registration.
- **Web:** .NET 10 Blazor WebAssembly, MudBlazor 9.7.0, existing SassCompiler, typed `Strings` resources.
- **Verification:** Existing xUnit, FluentAssertions, bUnit 2.7.2, and Playwright projects. No new production dependency.

## 3. High-level Architecture

Existing page queries still supply image URLs and names. `ShopImage` resolves presentation locally; its injected Application interface supplies placeholder URLs. Infrastructure contains provider-specific parsing and formatting. Browser downloads images directly; no new handler, repository call, or database round trip.

```text
Existing page query / DTO
  ProductCard or existing image caller
    ShopImage + ShopImagePresets + shared SCSS
      IImagePlaceholderProvider
        PlaceholdImagePlaceholderProvider (Infrastructure)
      MudImage (source plus named fallback)
```

Verified reuse: `Components/Products/ProductCard.razor`, `Styles/components/_producttile.scss`, `Components/Common/ShopImageUpload.*`, `Infrastructure/Persistence/Mappers/PlaceholderImage.cs`, and `Infrastructure/Storage/SupabaseFileStorage.cs`. Graph query: `ShopImageUpload ProductCard PlaceholderImage`.

## 4. Data Model

### Domain entities & value objects

Unchanged. No image-layout entities, persistence fields, or domain exceptions.

### DTOs and contracts

Existing `ProductSummaryDto.ImageUrl`, admin product URLs, brand/category URLs, and `ShopUploadedImage` remain compatible.

New `Application/Common/Interfaces/IImagePlaceholderProvider.cs`:

```csharp
public interface IImagePlaceholderProvider
{
    string CreateUrl(string label, int width, int height);
    bool IsPlaceholderUrl(string? url);
}
```

Both operations synchronous and pure; no asynchronous boundary or cancellation token needed. Existing `IFileStorage` remains unchanged.

New Web types, one per file under `Theme/`:

- `ShopImagePreset`: ten enum members listed below.
- `ShopImagePresetDefinition`: immutable record containing `int Width`, `int Height`, `ObjectFit Fit`, and nullable `ShopImagePreset MobilePreset`. Dimensions describe generated placeholder canvas, never rendered width or actual image export resolution.
- `ShopImagePresets`: static `Get(ShopImagePreset preset)` registry; derives ratio from placeholder canvas dimensions, formats numbers invariantly.

Original export examples remain content guidance, not new upload validation limits. Smaller placeholder canvases are proposed URL-generation defaults only. Both use the same preset proportions; actual uploaded image resolution remains unchanged. Display width always follows parent layout.

| Preset | Original example export | Proposed placeholder canvas | Fit | Mobile preset |
|---|---|---|---|---|
| `ProductCard` | 800×800 | 400×400 | Contain | Same |
| `ProductDetail` | 1600×1600 | 800×800 | Contain | Same |
| `Thumbnail` | 160×160 | 160×160 | Contain | Same |
| `CategoryTile` | 600×600 | 400×400 | Cover | Same |
| `CategoryBanner` | 1920×600 | 640×200 | Cover | `MobileBanner` |
| `Hero` | 1920×1080 | 640×360 | Cover | `MobileBanner` |
| `MobileBanner` | 800×1000 | 400×500 | Cover | Same |
| `Editorial` | 1200×900 | 640×480 | Cover | Same |
| `BrandLogo` | SVG preferred; original ratio | 400×400 fallback canvas only | Contain | Same |
| `SocialShare` | 1200×630 | 1200×630 | Contain | Same |

`BrandLogo` retains source proportions inside a caller-reserved slot. Its fallback canvas does not impose square logo proportions. `SocialShare` supports prepared artwork; generating social metadata or new sharing flows remains outside scope.

### Database tables and indexes

None added or modified.

## 5. Core Design Decisions

1. **Shared MudBlazor composition.** `ShopImage : MudComponentBase` composes `MudPaper` and `MudImage`. Existing product cards, upload previews, and admin rows justify extraction under constitution Rules 14 and 23–25. Reject custom native image primitives or a new rendering library.
2. **Parent owns available width.** Shared frame uses `width: 100%`, `min-width: 0`, selected ratio, and a positioned image filling its content area. Reference dimensions populate `MudImage.Width/Height`; stylesheet controls actual display dimensions. Logos fill an explicitly reserved parent slot. Reject fixed product-card widths and source-dependent frame resizing.
3. **One ratio definition.** C# registry owns reference dimensions; calculated ratio passes through a component-local CSS custom property composed by `StyleBuilder`. Static layout rules live in `_image.scss`; no duplicate numeric ratio map in SCSS. Consumer `Class`, `Style`, and root attributes remain forwarded. Use MudBlazor parameters for fit/position and utilities for color/spacing; dynamic ratio value is the remaining styling input under Rules 26–28.
4. **Separate frame background from asset.** Use existing project styling; user confirmed no feature-specific Figma on 2026-09-12. Product-card frame uses `mud-tertiary-bg` and existing 16px inset expressed as `pa-4`, with square ratio and confirmed `Contain`. Keep action overlays owned by `ProductCard`; no tinting, background removal, or rewriting uploaded image pixels. Figma inspection is not an implementation prerequisite.
5. **Provider isolation with compatibility.** Add `Infrastructure/Storage/PlaceholdImagePlaceholderProvider.cs`, implementing the new interface. Centralize existing URL syntax, `E8E8E8`, `7A7A7A`, and `raleway` there. Existing internal `PlaceholderImage.For(label)` delegates to shared formatter with 400×400 defaults; its three current consumers remain unchanged. Web regenerates recognized legacy placeholders at active preset dimensions. Reject provider URL parsing in Razor and unrelated repository/DTO refactoring. Constitution Rules 1–3 apply.
6. **Mobile source selection through MudBlazor.** Only `Hero` and `CategoryBanner` subscribe to existing `IBrowserViewportService`; unsubscribe on disposal. Select source, preset, fallback dimensions, and intrinsic attributes together. CSS reserves desktop/mobile frame using corresponding variables before image arrival. Proposed mobile threshold: below 600 CSS pixels; see Section 11. No raw `<picture>` or separate hidden desktop/mobile image pair.
7. **Source changes reset image failure state.** Resolve blank or recognized placeholder sources to named placeholder. Otherwise set real `Src` and generated `FallbackSrc`. Key inner `MudImage` by effective source/preset/label so reused rows, source changes, and language changes cannot retain an old fallback. Keep outer frame stable. MudImage's built-in fallback prevents repeated reassignment to the same failing URL.
8. **Preserve content and authorization.** Never mutate stored source URLs, selection IDs, upload bytes, permissions, handlers, routes, or busy keys. Apply presets inside existing permission boundaries. Existing upload `PreviewSize` still controls parent slot; file-reading, validation, and removal semantics stay unchanged.

API evidence: installed `MudBlazor.xml` for 9.7.0 verifies `MudImage` dimensions/fit/fallback and viewport subscription APIs. No MudBlazor MCP tool is exposed in this session. Official [MudImage source](https://github.com/MudBlazor/MudBlazor/blob/v9.7.0/src/MudBlazor/Components/Image/MudImage.razor.cs) verifies built-in fallback handling; [markup](https://github.com/MudBlazor/MudBlazor/blob/v9.7.0/src/MudBlazor/Components/Image/MudImage.razor) forwards attributes. [Placehold documentation](https://placehold.co/) supports dimensions, labels, colors, and SVG default output. Keep existing SVG placeholder URL format; no raster conversion required.

## 6. Core Functional Flow

### Flow 1: Browse images

1. Existing caller passes URL, localized description, name/label, and preset.
2. `ShopImage` obtains registry definition and generates placeholder through `IImagePlaceholderProvider`.
3. Blank/legacy-placeholder source uses generated URL; actual source uses `MudImage` with that URL as fallback.
4. Parent sets available space; frame owns ratio/background; image uses preset fit and `ObjectPosition.Center`.

### Flow 2: Resize or rotate

1. CSS responds to parent width without measuring every image in JavaScript.
2. Responsive banners receive viewport breakpoint changes. Use `MobileSrc` on mobile when supplied; otherwise reuse `Src`.
3. Active canvas and fallback change together. Frame ratio comes from CSS immediately; no source-load event sets frame height.

### Flow 3: Unavailable imagery, language, accessibility

1. `MudImage` switches failed real source to prepared fallback once; frame stays mounted.
2. Caller localizes description and generic labels using `Strings`; actual product/brand names remain untouched.
3. Decorative mode sets empty alternative text; no image-added `tabindex`. Existing links/buttons retain their own accessible names and focus treatment.

## 7. Development Plan

### Step 1 — Domain (`shop-domain-implementer`)

Skip: no Domain changes.

### Step 2 — Application (`shop-application-implementer`)

**Depends on:** resolved plan.

- [ ] **TASK-001** — Add `IImagePlaceholderProvider` with Section 4 signatures. No commands, DTO edits, validators, or persistence work.

**Completion gate:** `dotnet build src/TheShop.Application/TheShop.Application.csproj`; report literal interface. No Web or provider types in contract.

### Step 3 — Contract freeze

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `IImagePlaceholderProvider` | Application | Infrastructure, Web | Blocked until TASK-001 builds and literal API is handed off |
| Existing DTO image URLs and `IFileStorage` | Existing Application | Existing Infrastructure/Web | Stable; unchanged |

Infrastructure and Web start only after interface row becomes Stable during implementation.

### Step 4 — Infrastructure (`shop-infra-implementer`) — parallel with Step 5

**Depends on:** frozen interface.

- [ ] **TASK-002** — Add `Storage/PlaceholdImagePlaceholderProvider.cs`; register in `Infrastructure/DependencyInjection.cs`; delegate `Persistence/Mappers/PlaceholderImage.cs` to same formatter, preserving legacy output. Encode labels once. Recognize placeholders through parsed HTTPS host equality, not substring matching; leave unrelated URLs unchanged.
- [ ] **TASK-003** — Test specialist adds `tests/TheShop.Infrastructure.Tests/Storage/PlaceholdImagePlaceholderProviderTests.cs` and extends existing `PlaceholderImageTests.cs`: dimensions, encoded Unicode/punctuation, strict host recognition, invalid inputs, and unchanged legacy 400×400 URL.

**Completion gate:** Infrastructure builds; existing placeholder tests and new provider tests pass before overall Implement completion. No external image downloads in unit tests.

### Step 5 — Web (`shop-ui-implementer`) — parallel with Step 4

**Depends on:** frozen interface; resolved plan.

**Design baseline**

- User confirmed no feature-specific Figma on 2026-09-12; use existing project styling. Earlier product-card node was not inspected and is not a required design source.
- Product frame baseline: `ProductCard.razor`, `_producttile.scss`, `ShopColors.Tertiary`, and confirmed square/contained treatment. Apply Section 5 decision 4; preserve other callers' existing surrounding layouts. No Figma parity claim or reconnect requirement.

- [ ] **TASK-004** — Add `Theme/ShopImagePreset.cs`, `ShopImagePresetDefinition.cs`, `ShopImagePresets.cs`; add `Styles/components/_image.scss`, import through `Styles/TheShop.scss`. Implement all ten registry entries, frame rules, mobile variables, and caller-sized logo slot. No fixed responsive image width.
- [ ] **TASK-005** — Add `Components/Common/ShopImage.razor` and `.razor.cs`; add Section 9 resource entries to English/French resources before caller adoption. Parameters: `ShopImagePreset Preset`, `string? Src`, `string? MobileSrc`, required `string Label`, required `string Alt`, `bool Decorative`, and `bool Lazy`. Default `Lazy = false`; callers opt in below fold. Inherit/forward `Class`, `Style`, `UserAttributes`; separate `ImageAttributes` for leaf image attributes. Methods: `OnParametersSet()`, `OnAfterRenderAsync(bool firstRender)`, private `Task OnViewportChangedAsync(BrowserViewportEventArgs args)`, `ValueTask DisposeAsync()`. Register viewport subscription only for adaptive presets. Implement Section 5 fallback reset and existing project frame styling.
- [ ] **TASK-006** — Adopt `ShopImage` in `Components/Products/ProductCard.razor` and remove competing image width/height/padding/object-fit rules from `_producttile.scss`. Preserve overlay positions and callbacks. Frame owns inset once; no double padding. Remove store-logo fallback in this caller.
- [ ] **TASK-007** — Adopt `Thumbnail` in `ShopImageUpload.razor`, `VariantImageDialog.razor`, `Pages/Admin/ManageProducts.razor`, `ManageCategories.razor`, and `EditCategory.razor`. Retain existing avatar/preview/dialog slot widths. Use `BrandLogo` for `ManageBrands.razor` and `EditBrand.razor`. Keep existing conditional upload/edit sections and selection behavior. Replace pointer-only variant-image selection wrapper with a MudBlazor button exposing selection, keyboard activation, and focus under AC-13; preserve callback and overlay. Pass resource-formatted descriptions and associated names/labels.
- [ ] **TASK-008** — Adopt `BrandLogo` in `ShopAppBar.razor`, `ShopFooter.razor`, `Pages/Auth/SignIn.razor`, `SignInVerify.razor`, `SignUp.razor`, `SignUpVerify.razor`. Reserve existing caller slot sizes before load; cap them to available width. Retain links and surrounding text. Reuse `Strings.AppName`, `Strings.Product_ImageAlt`, existing upload labels, and Section 9 resources.
- [ ] **TASK-009** — Test specialist adds `Theme/ShopImagePresetsTests.cs`, `Components/Common/ShopImageTests.cs`, and resource coverage under `tests/TheShop.Web.Tests/`; extend affected `ProductCardTests`, `ShopImageUploadTests`, admin-page tests, and `ShopFooterTests`. Cover all ten treatments, source errors, source changes, mobile selection, attributes, localization, inherited parameters, and unchanged keyboard/access behavior. Mock provider and viewport service.

**Completion gate:** Web builds with Sass; all changed Razor uses MudBlazor and typed resources; product frame matches recorded project styling and confirmed spec; component tests pass before overall Implement completion. Run design-rule script against owned production files. Do not add unrequested screens.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4–5 complete.

- [ ] **TASK-010** — Orchestrator formats after editing workers finish, builds solution, reruns affected tests and design checks, verifies DI, then runs `graphify update .`. Preserve unrelated working-tree changes; current workspace also contains product-description work.
- [ ] **TASK-011** — Test specialists reconcile feature manifest and AC evidence before Implement completion. Existing-screen browser proof covers real product cards, admin slots, resize, image failure, focus, and access. Unmounted presets receive explicit unit-proven classification; do not claim unrun gallery/banner/social page journeys. `$theshop-test`, `$theshop-e2e`, and `$theshop-review` remain separately invoked. Document is manual only.

**Completion gate:** solution build and affected tests pass after `.sdd/scripts/format-changes.ps1`; `.sdd/scripts/check-design-rules.ps1 -Path <changed-production-files>` passes; scoped implementation and evidence gates pass. Browser proof is recorded only when executed.

### Deviation procedure

- Accept only behavior-preserving, architecture-compliant simplifications within named files/tasks.
- Reject product changes, invented screen routes, authorization weakening, and unrelated refactoring.
- Contract changes stop dependent work. Record revised signature and affected TASK IDs; rebuild/refreeze before resuming.
- Any design conflict with confirmed whole-product visibility must be resolved explicitly; never silently replace `Contain` with `Cover`.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: whole products | TASK-004, TASK-005, TASK-006, TASK-009 |
| AC-2: thumbnails | TASK-004, TASK-007, TASK-009 |
| AC-3: category tile | TASK-004, TASK-005, TASK-009 |
| AC-4: desktop banners | TASK-004, TASK-005, TASK-009 |
| AC-5: mobile artwork | TASK-005, TASK-009 |
| AC-6: mobile fallback | TASK-005, TASK-009 |
| AC-7: editorial | TASK-004, TASK-005, TASK-009 |
| AC-8: logos | TASK-004, TASK-007, TASK-008, TASK-009 |
| AC-9: social composition | TASK-004, TASK-005, TASK-009 |
| AC-10: responsive sizing | TASK-004, TASK-005, TASK-009, TASK-011 |
| AC-11: named fallback and stability | TASK-001, TASK-002, TASK-003, TASK-005, TASK-009, TASK-011 |
| AC-12: localization | TASK-005, TASK-008, TASK-009 |
| AC-13: keyboard/focus | TASK-006, TASK-007, TASK-008, TASK-009, TASK-011 |
| AC-14: access preservation | TASK-007, TASK-009, TASK-010, TASK-011 |

FR coverage: FR-1/2 tasks 004–009; FR-3 task 005; FR-4 tasks 004/005/009; FR-5 tasks 001–003/005/009; FR-6 tasks 005/007–009; FR-7 tasks 006–011.

## 9. Validation & Error Handling Strategy

No Application validators, Domain exceptions, or new `Result.Fail` keys: this feature introduces no business submission. Existing page errors and permission-denied keys remain intact.

| Input / edge / rule | Handling |
|---|---|
| Tall/wide products; RULE-2 | `ObjectFit.Contain`; stable frame; source unchanged. |
| Logo proportions | Caller-reserved slot; contained source; no load-derived outer height. |
| Photography mismatch; RULE-3 | `ObjectFit.Cover`, `ObjectPosition.Center`; no destructive crop. |
| Missing mobile artwork | Reuse desktop source, mobile ratio and fallback canvas. |
| Blank source / known legacy placeholder | Generate active preset placeholder with caller label. |
| Real source fails | MudImage fallback; keyed reset on source/preset/label changes. |
| Narrow parent / orientation; RULE-1 | Width bounded by parent; CSS ratio; responsive banner source follows breakpoint. |
| Undefined enum / invalid canvas | Developer misuse: `ArgumentOutOfRangeException`; no user toast. Provider dimensions 10–4000 per axis. |
| Empty meaningful description/label | Caller contract validation in component tests; use typed localized labels, never fabricated product names. |
| Decorative image | Empty alternative text; no extra keyboard stop. |
| Access restriction; RULE-4 | Existing policies, commands, and RLS unchanged; no new bypass. |
| Absent destination | Registry and component coverage only; no production demonstration routes. |
| Placeholder provider also unavailable | External dependency risk in Section 11; never retry same failing fallback indefinitely. |

New UI resources, Web-owned:

| Key | English | French |
|---|---|---|
| `Strings.ShopImage_Description` | Image of {0} | Image de {0} |
| `Strings.ShopImage_BrandDescription` | Logo of {0} | Logo de {0} |

Existing `Strings.Product_ImageAlt` remains product description template. `Strings.AppName` remains site-logo label. New upload previews use localized supplied label rather than exporting local filenames into placeholder requests. No changes to price, tax, date, or currency formatting.

## 10. Database Schema & RLS Policies

No schema, migrations, indexes, buckets, or RLS changes. Existing storage paths and URL-bearing DTOs remain compatible. No image metadata backfill or source transformation. Existing product/brand/category permissions continue to govern their pages and records.

## 11. Open Questions, Risks & Assumptions

- **📌 Assumption:** Mobile banner presentation applies below 600 CSS pixels; tablet/desktop uses desktop ratio. Keep CSS threshold aligned with existing MudBlazor breakpoint service. Ratify before TASK-004/005.
- **⚠️ Risk:** Remote Placehold outage can prevent fallback label rendering. TASK-005 bounds retries and preserves frame; offline local fallback is not confirmed product scope. Raise any proposed local fallback through spec clarification before adding behavior.
- **⚠️ Risk:** Several presets lack existing destination screens. TASK-009 proves registry/component contracts; TASK-011 distinguishes unit-proven cases from actual browser layout evidence. No claim of browser-rendered geometry from bUnit alone.

---
**Status:** Draft · **Spec:** `.specs/shop-image/spec.md` · **Created:** 2026-09-11

# The Shop: MudBlazor to Native Blazor UI Migration

## 1. Purpose and execution authority

This is a standalone technical implementation handoff for replacing MudBlazor with semantic HTML, project-owned SCSS, and reusable Blazor components in The Shop. It records the architecture selected by the project owner and the implementation and verification needed to preserve the application.

**This migration does not require the SDD stage flow.** Do not require Start, Spec, Clarify, Plan, Resolve, Execute, Test, Verify, or Ship skill invocations, a numbered feature branch, `.specs` artifacts, or stage approvals as prerequisites. The phases below are technical implementation batches, not SDD stages. Existing specifications and tests may be consulted as behavior references when relevant; they are not execution gates for this handoff.

Repository instructions still matter for preserving architecture, resources, authorization, and user changes. Existing Mud-only design rules conflict with the owner's selected migration. Reconcile those specific rules and their checks in their canonical `.sdd/` locations as part of implementation. Do not duplicate or replace the repository's general workflow in root instructions, adapters, or this document. Do not disable all hooks or unrelated checks to bypass a conflict.

When another AI receives this document **as an implementation request**, it should execute the migration, validate each batch, and maintain the progress record in section 18. The creation of this document alone does not mean implementation has started.

This document reduces ambiguity; it cannot guarantee an error-free migration. Completion requires actual build, test, browser, and dependency-removal evidence. Never report a skipped or unavailable check as passed.

### Approved destination

- Keep .NET Blazor WebAssembly and the four existing application projects.
- Use native semantic HTML for layout and content.
- Use CSS Grid, Flexbox, project-owned CSS variables, and SCSS for presentation.
- Use Blazor's built-in forms and inputs before creating custom inputs.
- Implement small reusable `Shop*` components for repeated behavior and consistent controls.
- Remove both `MudBlazor` and `CodeBeam.MudBlazor.Extensions` from the final dependency graph.
- Do not replace them with Bootstrap, Tailwind, another broad UI suite, or a new JavaScript application framework.
- Keep the existing specialized rich-text editor dependency unless a separate requirement authorizes replacing it.
- Preserve the current design and behavior. This is not a visual redesign or a feature expansion.

### Actions outside this handoff

Do not deploy, push, open a PR, send messages, change production data, rotate credentials, change database schemas, replace authentication, or implement missing product features solely because this guide exists. Follow the implementing user's authorization for commits and publication. Routine local implementation and verification are the intended work when implementation is requested.

## 2. Inspected baseline and mandatory rediscovery

The source was inspected on 2026-10-02 on branch `refactor/ui-refactoring`, at commit `dd747d9df0137842be5077bd7f2b90f99e6b92a6`. The working tree was clean when the guide was started. This is a historical reference, not a branch-switch or reset instruction.

Observed baseline:

| Concern | Observed implementation |
|---|---|
| Runtime | `Microsoft.NET.Sdk.BlazorWebAssembly`, `net10.0` |
| UI packages | `MudBlazor` 9.11.0; `CodeBeam.MudBlazor.Extensions` 9.1.0 |
| CSS compiler | `AspNetCore.SassCompiler` 1.101.0 |
| Sass configuration | Source `Styles`, target `wwwroot/css`, scoped CSS generation disabled |
| Razor footprint | 50 Razor files; 45 contain Mud tags or Mud inheritance |
| Behavior-heavy controls | 9 `MudForm` tags, 4 `MudTable` tags, plus date picker, range slider, menu, dialogs, uploads, OTP inputs |
| Tests | xUnit, bUnit 2.7.2, Playwright; existing tests often reference Mud types or selectors |
| Shared loading | `Common/BusyState.cs`, `BusyKeys.cs`, `Components/Common/BusyFor.*` |
| URL state | `Common/QueryStatePageBase.cs`, feature query-state records |
| Theme | `Theme/ShopColors.cs`, `ShopTypography.cs`, `ShopTheme.cs`, `ShopIcons.cs` |
| UI registration | `DependencyInjection.AddPresentation()` |
| HTML host | `wwwroot/index.html` loads both Mud packages' CSS and JavaScript |
| Existing CSS | `Styles/TheShop.scss` plus hand-authored `wwwroot/css/app.css` |

**Product-details branch difference:** earlier discussion referenced `ProductDetails.*`, `ShopImageGallery.*`, `_productdetails.scss`, and `shopImageGallery.js`. Those files were absent from the inspected branch. Do not recreate that feature from discussion or invent its routes. If these files exist in the implementation checkout, add them to the migration inventory and preserve their existing contracts. Otherwise mark their migration rows not applicable.

Before editing, inspect the actual checkout again:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
dotnet --info
rg --files src/TheShop.Web tests/TheShop.Web.Tests tests/TheShop.E2E.Tests
rg -n 'MudBlazor|MudExtensions|CodeBeam|AddMudServices|AddMudExtensions' src tests -g '*.cs' -g '*.razor' -g '*.csproj' -g '*.html'
rg -n '<Mud|@inherits Mud' src/TheShop.Web -g '*.razor'
rg -n 'mud-|--mud-' src/TheShop.Web/Styles src/TheShop.Web/wwwroot/js tests/TheShop.E2E.Tests
```

If `graphify-out/graph.json` exists, use Graphify to locate dependencies before broad source exploration. Confirm graph findings against current source; the graph can contain older branches or historical rules. Do not rebuild the graph merely to start the migration.

Produce a short inventory of components, consumers, styling dependencies, services, and affected tests. Preserve pre-existing edits. If another process changes the checkout during implementation, recheck the relevant files before continuing; do not overwrite or reset its work.

## 3. Scope and preservation requirements

### Preserve

1. Domain behavior, Application commands and queries, DTOs, repository interfaces, persistence, and external integrations.
2. Centralized routes, query parameters, deep links, browser Back/Forward, and cancellation of superseded loads.
3. Authentication, OTP journeys, return URLs, authorization policies, role/permission checks, and existing database enforcement.
4. The English-only resource model. Static UI strings use typed `Strings` accessors; runtime error keys use the localizer. Add new accessibility labels to `Strings.resx`.
5. Product/brand/category create, edit, listing, selection, filtering, sorting, pagination, confirmation, and bulk operations.
6. Product variants, image pins, specification editing, draft/publish rules, field errors, and conflict handling.
7. Image presets, loading/failure behavior, reserved geometry, uploads, previews, and object-URL cleanup.
8. Rich-text editing, its existing sanitization/validation boundaries, and supported formats.
9. Shared loading, notifications, navigation focus, breadcrumb behavior, and responsive layouts.
10. Existing feature tests' behavioral assertions, even when their component queries must change.

### Explicit non-goals

- Do not introduce SSR, change hosting, or claim that removing Mud fixes SEO.
- Do not build unfinished cart, checkout, wishlist, account, or product-details features.
- Do not implement dark mode or a theme switcher. Current layouts explicitly use light mode.
- Do not replace Supabase, MediatR, resource generation, or the Sass compiler.
- Do not build a general-purpose data grid, component library, global state framework, CSS utility framework, or dynamic form engine.
- Do not move every file to a new folder just to match a diagram.
- Do not upgrade unrelated packages as part of UI removal.
- Do not claim bundle-size or performance improvements without comparable release measurements.

Removing Mud changes presentation ownership. It must not weaken business validation or authorization. Client-side WebAssembly validation is never a security boundary; preserve existing remote enforcement.

## 4. Target architecture and ownership

### Project boundaries

| Project | Responsibility after migration |
|---|---|
| `TheShop.Domain` | Pure business entities, values, and invariants; no UI dependencies |
| `TheShop.Application` | Use cases, DTOs, validators, dependency interfaces, results; no UI types |
| `TheShop.Infrastructure` | Database and external-system implementations |
| `TheShop.Web` | Pages, UI components, forms, styles, state, UI services, browser integrations |

Keep the existing dependency direction. `Program.cs` remains the composition root. No new Razor class library is needed for one application.

### Web ownership

- **Pages** own routes, loading, navigation, screen coordination, permissions, and Application dispatch.
- **Feature components** may own established feature workflows. `ProductForm` already owns its shared create/edit dispatch; retain that ownership rather than duplicating it in both pages.
- **Common components** are generic presentation and interaction. They receive values, render content, and emit callbacks. They must not fetch products or dispatch business commands.
- **Form models** are mutable Web-only editing data beside their feature owner. Map them to existing Application commands on submit.
- **State stores** contain state genuinely shared by multiple consumers. They do not become repository wrappers or new business services.
- **Common UI services** own their small presentation concern, such as notifications or dialog requests. They remain in Web.
- **SCSS** owns appearance; Razor/C# owns application state and UI decisions; JavaScript owns narrowly scoped browser integration.

### Representative target tree

Existing names should normally stay. New examples are not instructions to create unused files. Components with behavior have sibling `.razor.cs` partials even where omitted below.

```text
src/TheShop.Web/
  Pages/
    Home.razor / Home.razor.cs
    NotFound.razor / NotFound.razor.cs
    Products/
      ProductCatalogue.razor / ProductCatalogue.razor.cs
      CatalogueQueryState.cs
      ProductDetails.razor / ProductDetails.razor.cs     [only if already present]
    Auth/
      SignIn.razor / SignIn.razor.cs
      SignInVerify.razor / SignInVerify.razor.cs
      SignUp.razor / SignUp.razor.cs
      SignUpVerify.razor / SignUpVerify.razor.cs
    Admin/
      _Imports.razor
      ManageProducts.razor / ManageProducts.razor.cs
      AddProduct.razor / AddProduct.razor.cs
      EditProduct.razor / EditProduct.razor.cs
      [existing brand/category pages and query-state records]
  Components/
    Layout/
      MainLayout.razor / MainLayout.razor.cs
      AuthLayout.razor / AuthLayout.razor.cs
      ShopUiHost.razor
    Common/
      ShopButton.razor / ShopButton.razor.cs
      ShopIconButton.razor / ShopIconButton.razor.cs
      ShopIcon.razor / ShopIcon.razor.cs
      ShopFieldLabel.razor
      ShopMoneyField.razor / ShopMoneyField.razor.cs
      ShopDialog.razor / ShopDialog.razor.cs
      ShopDialogHost.razor / ShopDialogHost.razor.cs
      ShopConfirmDialog.razor / ShopConfirmDialog.razor.cs
      ShopNotificationHost.razor / ShopNotificationHost.razor.cs
      ShopNotification.razor / ShopNotification.razor.cs
      ShopImage.razor / ShopImage.razor.cs
      ShopImagePreset.cs
      ShopImageUpload.razor / ShopImageUpload.razor.cs
      ShopUploadedImage.cs
      ShopPagination.razor / ShopPagination.razor.cs
      ShopBreadcrumbs.razor / ShopBreadcrumbs.razor.cs
      ShopSortSelect.razor / ShopSortSelect.razor.cs
      ShopFilterPanel.razor / ShopFilterPanel.razor.cs
      ShopBulkActionBar.razor / ShopBulkActionBar.razor.cs
      ShopRichTextEditor.razor / ShopRichTextEditor.razor.cs
      ShopLoadingOverlay.razor / ShopLoadingOverlay.razor.cs
      BusyFor.razor / BusyFor.razor.cs
      OtpInput.razor / OtpInput.razor.cs
      [existing app bar, footer, profile menu, access views]
    Products/
      ProductCard.razor / ProductCard.razor.cs
      ProductForm.razor / ProductForm.razor.cs
      ProductFormModel.cs
      ProductVariantsCard.razor / ProductVariantsCard.razor.cs
      ProductVariantsState.cs
      ProductContentCard.razor / ProductContentCard.razor.cs
      ProductSpecificationsState.cs
      VariantImageDialog.razor / VariantImageDialog.razor.cs
      VariantImagePinResult.cs
    Admin/
      AdminModuleCard.razor / AdminModuleCard.razor.cs
  Common/
    Routes.cs
    BusyKeys.cs
    BusyState.cs
    QueryStatePageBase.cs
    BreadcrumbTrail.cs
    ShopBreadcrumbItem.cs
    CurrencyFormatter.cs
    UI/
      ShopComponentBase.cs
      ShopCssClass.cs
      ShopColor.cs
      ShopVariant.cs
      ShopSize.cs
      ShopMaxWidth.cs
    Forms/
      FormValidationMessages.cs
    Dialogs/
      IShopDialogService.cs
      ShopDialogService.cs
      ShopDialogRequest.cs
      ShopConfirmationOptions.cs
      ShopDialogResult.cs                             [future typed picker result, only when needed]
    Notifications/
      IShopNotificationService.cs
      ShopNotificationService.cs
      ShopNotificationMessage.cs
      ShopNotificationKind.cs
    Sorting/
      [existing sorting catalogues and definitions]
  State/
    AuthState.cs
    CartState.cs
    BreadcrumbState.cs
    AnnouncementState.cs
    FooterState.cs
    PendingSignUpState.cs
  Auth/
    [existing authentication and authorization plumbing]
  Theme/
    ShopIcons.cs
  Resources/
    Strings.resx
  Styles/
    abstracts/
      _breakpoints.scss
      _mixins.scss
      _text-style.scss
    tokens/
      _colors.scss
      _typography.scss
      _spacing.scss
      _sizing.scss
      _theme.scss
    base/
      _reset.scss
      _document.scss
      _typography.scss
      _accessibility.scss
    components/
      _icon.scss
      _button.scss
      _icon-button.scss
      _field.scss
      _dialog.scss
      _variant-image.scss
      _notification.scss
      _image.scss
      _imageupload.scss
      _otpinput.scss
      _breadcrumbs.scss
      _pagination.scss
      _producttile.scss
      _rich-text-editor.scss
      [other actually used component partials]
    layouts/
      _main.scss
      _auth.scss
      _catalogue.scss
      _manageproducts.scss
      _productdetails.scss                             [only if page exists]
    utilities/
      _spacing.scss
      _sizing.scss
      _typography.scss
    TheShop.scss
  wwwroot/
    css/TheShop.css                                    [generated]
    js/shopDialog.js
    js/shopImageUpload.js
    js/shopOtpInput.js                                 [retain only needed behavior]
    js/shop-rich-text-editor.js
    images/logo/
    lib/quill/
    index.html
  App.razor
  _Imports.razor
  Program.cs
  DependencyInjection.cs
  sasscompiler.json
  TheShop.Web.csproj
```

The existing `.csproj` excludes `Services/**` from compilation and other items. Use the `Common/` locations above. If choosing `Services/` instead, intentionally reconcile all exclusion entries first; do not silently add source that will never compile.

## 5. Design tokens, base styles, and stylesheet ownership

### Figma design authority (provided 2026-10-02)

Use the owner's [Foundations section](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=197-2570) on the Components page as the visual reference. File key: `63Ieb8AduwMHoVHwzZ7UO3`; section: `197:2570`. Inspect live nodes and screenshots before implementing each component. Do not copy Figma's generated CSS or recreate its frame hierarchy as Blazor wrappers.

Confirmed first-batch references: Button `157:24`, Text Field `170:136`, Icon Button `322:6450`. Button variants are Filled/Outlined/Text in Small/Medium/Large; labels use Space Grotesk 14px/500 with 0.25px letter spacing, 12px icon gaps, and square corners. Filled heights are 34/40/46px; outlined heights are 32/38/44px; text heights are 26/40/46px. These are observed Figma component bounds, not arbitrary fixed widths for localized or long content. Text fields use 16px text and a 61px reference height; their filled surface is `#ededed` and the focused state has a 2px bottom stroke.

Two filled icon-button variants are both named Medium: `322:6472` is 36x36 and `322:6475` is 42x42. Batch 9 implements the requested Small/Medium/Large API and explicitly treats the latter as Large, inferred from its third position and 32px icon. This is a documented naming inference, not a corrected Figma label. Product-card actions retain the confirmed 36x36 Filled/Medium treatment. See section 6.2 for all measured dimensions.

Button hover/pressed/disabled/loading/keyboard-focus variants are not explicitly present in the inspected set. Conservative token-based states and visible keyboard focus are implementation decisions, not claimed Figma parity. Visible associated form labels, sufficient error-text contrast, forced-colors visibility, and reduced-motion behavior remain required even when absent from the design. Record discrepancies before expanding to further components. Preserve existing page composition when no corresponding page design has been inspected.

This source and visual comparison requirement do not add an SDD workflow dependency. Store progress and verification in section 18; do not create workflow artifacts solely to consume the design.

### 5.1 One source of visual values

Final authoritative visual tokens live in SCSS and are emitted as `--shop-*` CSS variables. Do not maintain duplicate hand-edited C# and SCSS palettes.

| Current owner | Final owner |
|---|---|
| `Theme/ShopColors.cs` | `Styles/tokens/_colors.scss` |
| `Theme/ShopTypography.cs` | `Styles/tokens/_typography.scss` |
| `Theme/ShopTheme.cs` | Token definitions plus base/component styles |
| `Theme/ShopIcons.cs` | Keep existing trusted SVG and asset registry |
| Mud-generated spacing/display/color classes | Owned component/layout rules; small utilities only where useful |
| `wwwroot/css/app.css` | Relevant rules move to `Styles/base/` or their actual component owner |

Read the actual theme configuration, not only comments. For example, the inspected `ShopTypography` H5 comment and its configured weight/family are not fully consistent. Record the rendered/configured baseline before deciding the destination value. Do not silently redesign typography while moving it.

Preserve existing effective values, including font family, weight, line height, tracking, text transformation, borders, disabled colors, and layout dimensions. Mud-generated defaults absent from C# constants may need computed-style inspection. Capture used hover/focus/active states, not only normal screenshots.

#### Approved Figma color mapping (verified 2026-10-02)

`Styles/tokens/_colors.scss` mirrors the 22 local paint styles in the linked Figma file `63Ieb8AduwMHoVHwzZ7UO3`. These are named paint styles, not a variable collection with theme modes. Values below were read directly from Figma; all are opaque solid colors, converted to 8-bit hex. Figma now names the surfaces `Surface/Default` and `Surface/Subtle`, replacing `Surface/White` and `Surface/Light Gray`.

Naming contract: prepend `--shop-color-`, convert spaces/path separators to lowercase kebab-case, and omit only the leading `Brand/` and `Semantic/` groups. Retain `text-`, `surface-`, and `lines-`. Do not introduce `--shop-color-brand-*`, `--shop-color-semantic-*`, or a new numeric palette naming system for these styles.

| Figma paint style | CSS custom property | Value |
|---|---|---|
| `Brand/Primary` | `--shop-color-primary` | `#171717` |
| `Brand/Primary Contrast` | `--shop-color-primary-contrast` | `#ffffff` |
| `Brand/Secondary` | `--shop-color-secondary` | `#7a7a7a` |
| `Brand/Secondary Contrast` | `--shop-color-secondary-contrast` | `#ffffff` |
| `Brand/Tertiary` | `--shop-color-tertiary` | `#e8e8e8` |
| `Brand/Tertiary Lighter` | `--shop-color-tertiary-lighter` | `#ededed` |
| `Brand/Tertiary Contrast` | `--shop-color-tertiary-contrast` | `#171717` |
| `Semantic/Info` | `--shop-color-info` | `#429dff` |
| `Semantic/Success` | `--shop-color-success` | `#42ff83` |
| `Semantic/Warning` | `--shop-color-warning` | `#ffc042` |
| `Semantic/Error` | `--shop-color-error` | `#ff4242` |
| `Text/Primary` | `--shop-color-text-primary` | `#171717` |
| `Text/Secondary` | `--shop-color-text-secondary` | `#7a7a7a` |
| `Text/Disabled` | `--shop-color-text-disabled` | `#b0b0b0` |
| `Text/On Dark` | `--shop-color-text-on-dark` | `#ffffff` |
| `Surface/Default` | `--shop-color-surface-default` | `#ffffff` |
| `Surface/Subtle` | `--shop-color-surface-subtle` | `#f5f5f5` |
| `Surface/Appbar` | `--shop-color-surface-appbar` | `#ffffff` |
| `Surface/Drawer` | `--shop-color-surface-drawer` | `#ffffff` |
| `Lines/Default` | `--shop-color-lines-default` | `#e0e0e0` |
| `Lines/Input` | `--shop-color-lines-input` | `#e0e0e0` |
| `Lines/Divider` | `--shop-color-lines-divider` | `#e0e0e0` |

Keep each Figma role independently defined even when its current hex matches another role. For example, do not alias `text-primary` to `primary`, or collapse all three `lines-*` styles: those design roles may change independently.

Owner-approved additions (batch 8): `_colors.scss` also declares `--shop-color-info-contrast`, `--shop-color-success-contrast`, `--shop-color-warning-contrast`, and `--shop-color-error-contrast`, each `#ffffff` for now. These four values are application decisions, not inspected Figma paint styles. Existing primary/secondary/tertiary contrast values remain unchanged. A token named "contrast" specifies foreground intent; its name does not guarantee an accessible contrast ratio.

The seven application aliases introduced in batch 2 were removed in batch 3 after migrating every SCSS consumer. Use the Figma-backed token directly; the table below is a historical replacement map, not declarations to recreate:

| Removed alias | Direct Figma-backed replacement |
|---|---|
| `--shop-color-action` | `--shop-color-primary` |
| `--shop-color-on-action` | `--shop-color-primary-contrast` |
| `--shop-color-field` | `--shop-color-tertiary-lighter` |
| `--shop-color-surface` | `--shop-color-surface-default` |
| `--shop-color-surface-muted` | `--shop-color-surface-subtle` |
| `--shop-color-surface-placeholder` | `--shop-color-tertiary` |
| `--shop-color-border` | `--shop-color-lines-default` |

Buttons, fields, images, product cards, auth layout, and theme states now reference the Figma-backed tokens directly. Choose the precise role for new consumers; appbar, drawer, input, and divider roles must not all default to a generic surface/line token merely because their colors currently match. Do not recreate these seven aliases or introduce a second application palette without a demonstrated new requirement.

Button colors consume Figma-backed variables directly. The additional `--shop-button-background`, `--shop-button-foreground`, `--shop-button-hover-background`, and `--shop-button-pressed-background` color overrides were removed in batch 4. Select owned button/icon-button treatments through component parameters instead of redefining their colors in product-card styles. Internal button-size properties remain for the generated size variants; these are not duplicate palette aliases. The existing `--shop-color-action-hover`, `--shop-color-action-pressed`, and `--shop-color-action-disabled` state tokens remain unchanged, as requested. Batch 9 removes the separate form error-text token; native validation consumes `--shop-color-error` directly.

Example excerpt:

```scss
// Styles/tokens/_colors.scss
:root {
    --shop-color-primary: #171717;
    --shop-color-primary-contrast: #ffffff;
    --shop-color-text-primary: #171717;
    --shop-color-surface-default: #ffffff;
}

.shop-button-filled {
    color: var(--shop-color-primary-contrast);
    background-color: var(--shop-color-primary);
}
```

This example is a subset; the table is the complete inspected Figma color-style mapping. Define every token used by the migrated application. Do not map every old `Color.Secondary` to the brand secondary token without checking whether it colors text, a surface, or a border.

Keep inferred hover/pressed/disabled and accessibility-specific values in `_theme.scss`, clearly documented as application decisions. At the owner's request, native validation messages and invalid borders now use Figma's `Semantic/Error` through `--shop-color-error` directly; do not restore a separate error-text alias. Error text on white measures approximately 3.44:1, below the 4.5:1 small-text threshold; resolve this palette limitation before production accessibility sign-off. No dark theme is introduced. Reinspect Figma before future palette changes; this mapping records the verified snapshot, not an automatic synchronization service.

Existing brand colors are not automatically suitable for every text/background pairing. Check actual contrast; document targeted accessibility corrections rather than changing the entire palette. Preserve English-only copy in resources.

#### Figma typography styles — verified 2026-10-02

Source: the 13 local `Typography/*` text styles in the supplied Figma file, displayed in [Typography - New (2001:2)](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2001-2). Actual style metadata takes precedence over stale canvas labels and old `ShopTypography` comments.

| Figma style | CSS name segment | Font family | Size (px at 16px root) | Weight | Tracking (px) |
|---|---|---|---:|---:|---:|
| Typography/H1 | `h1` | Barlow Condensed | 96 | 800 | 3 |
| Typography/H2 | `h2` | Barlow Condensed | 60 | 800 | 3 |
| Typography/H3 | `h3` | Barlow Condensed | 48 | 800 | 3 |
| Typography/H4 | `h4` | Barlow Condensed | 34 | 700 | 2 |
| Typography/H5 | `h5` | Barlow Condensed | 24 | 700 | 2 |
| Typography/H6 | `h6` | Space Grotesk | 20 | 500 | 0.25 |
| Typography/Subtitle 1 | `subtitle-1` | Space Grotesk | 16 | 500 | 0.25 |
| Typography/Subtitle 2 | `subtitle-2` | Space Grotesk | 14 | 500 | 0.25 |
| Typography/Body 1 | `body-1` | Space Grotesk | 16 | 400 | 0.25 |
| Typography/Body 2 | `body-2` | Space Grotesk | 14 | 400 | 0.25 |
| Typography/Button | `button` | Space Grotesk | 14 | 500 | 0.25 |
| Typography/Caption | `caption` | Space Grotesk | 12 | 400 | 0.25 |
| Typography/Overline | `overline` | Space Grotesk | 10 | 400 | 0.25 |

Contract:

- `_typography.scss` generates six CSS custom properties per named style: `--shop-typography-{style}-font-family`, `-font-size`, `-font-weight`, `-line-height`, `-letter-spacing`, and `-text-transform`. Sizes use `rem`; the application root font size is not changed. Tracking preserves Figma's pixel values.
- Every inspected line height is `AUTO`, mapped to CSS `normal`, not the old Mud unitless ratios. Font metrics can vary across platforms; browser line-box comparisons allow 1px rounding tolerance. Do not manufacture explicit ratios and call them Figma values.
- Case is `ORIGINAL` (`none`) except Button's `TITLE` (`capitalize`). CSS capitalization is a display approximation, not editorial title-casing; preserve resource strings and acronyms. Sample headings containing uppercase text do not make uppercase part of the named heading style.
- All styles have no text decoration and zero paragraph/list spacing or indent. Do not emit a blanket decoration reset or zero margins through the typography mixin: semantic `<s>`, link affordances, and component spacing must survive.
- `abstracts/_text-style.scss` provides `text-style.apply(style)` to apply the six token properties. It emits no standalone selectors, creates no aliases, and does not replace semantic HTML. Use direct variables for documented property-level overrides. Do not recreate removed `--shop-font-*`, `--shop-line-height-*`, or `--shop-letter-spacing` application aliases.
- `.shop-native` defaults to Body 1. Buttons use Button; input text uses Body 1; errors and image labels use Caption. Auth title uses H2, subtitle/instruction Subtitle 1, and tagline Subtitle 2.
- Product Tile (2009:5168): brand and original price use Subtitle 1; current price uses H6 (500, not the old 700); stock text uses Body 2. Product title node 2009:5112 is **unbound** Space Grotesk 20px Regular, AUTO, 0.25px: apply H6 with Body 1's weight as an explicit component override, not a fictitious named Figma style. Original price retains semantic strike-through.
- Existing auth-title uppercase treatment and visible input-label medium emphasis remain explicit implementation decisions. They do not alter H2 or Caption globally. Unconverted Mud consumers retain their existing C# theme until their migration; do not synchronize that temporary theme into a second permanent registry.

```scss
@use '../abstracts/text-style';

.shop-product-card-price {
    @include text-style.apply(h6);
}

.shop-product-card-title {
    @include text-style.apply(h6);
    // Measured unbound text node; not a named style.
    font-weight: var(--shop-typography-body-1-font-weight);
}
```

### 5.2 Responsibilities

- `abstracts/`: Sass maps, breakpoint values, functions, and mixins; no emitted selectors.
- `tokens/_colors.scss`: palette/semantic color variables.
- `tokens/_typography.scss`: Figma-named families, sizes, weights, line heights, tracking, and text-case variables.
- `abstracts/_text-style.scss`: non-emitting mixin consuming a named typography style directly.
- `tokens/_spacing.scss`: shared spacing scale variables.
- `tokens/_sizing.scss`: shared maximum-width scale; emits `--shop-max-width-*` tokens.
- `tokens/_theme.scss`: radius, shadow, focus, motion, and other shared appearance variables not owned above.
- `base/_reset.scss`: minimal browser normalization, not an aggressive removal of native behavior.
- `base/_document.scss`: body background, foreground, document sizing, boot/error presentation as appropriate.
- `base/_typography.scss`: applies typography defaults to semantic HTML.
- `base/_accessibility.scss`: shared focus treatment, visually hidden content, reduced-motion rules.
- `components/`: selectors owned by reusable component families, including feature components such as product cards.
- `layouts/`: application shells and page-specific geometry/responsive composition.
- `utilities/`: deliberately small reusable families. Do not recreate all Mud or Bootstrap utilities.

Minimal reset example:

```scss
*,
*::before,
*::after {
  box-sizing: border-box;
}

body {
  margin: 0;
}

button,
input,
select,
textarea {
  font: inherit;
}
```

Never globally remove focus outlines without an equivalent visible replacement. Avoid `all: unset` on every control. Keep native affordances until a specific styled control intentionally supplies them.

Native `.shop-field-input` controls use their `.shop-field-control` wrapper for focus/error indication: a primary bottom stroke on focus, an error bottom stroke for invalid input, and associated error text. Do not add an inner text outline through the shared focus selector. Forced-colors mode uses a visible wrapper outline because box shadows are suppressed. Buttons retain their keyboard-focus outline; decorative icons never receive independent focus.

### 5.3 CSS rules

#### Button, icon-button, and icon ownership

- `components/_icon.scss`: standalone `.shop-icon` SVG size, alignment, and display defaults. No button backgrounds, padding, or interaction states.
- `components/_button.scss`: shared `.shop-button` foundation, text-button sizes, color × filled/outlined/text treatments, disabled/forced-colors behavior, and `.shop-button-icon` start/end adornments. Surface is now a color role available to text and icon-only buttons alike.
- `components/_icon-button.scss`: icon-only geometry, reusing `.shop-button` color/variant treatments without duplicating them. Its private Sass map emits all nine measured variant/size combinations documented in section 6.2. Icon-button dimensions are independent of text-button dimensions.
- `components/_producttile.scss`: product-card layout and cart/wishlist positioning only for those shared actions. The separate product-content selection button retains its card-specific content layout here.

Load `_icon.scss` before `_button.scss`, then `_icon-button.scss` in `TheShop.scss`: standalone icon defaults must not override button adornment sizing. Keep the imports single and explicit. `ShopIconButton` composes `ShopButton` and `ShopIcon` without adding a DOM wrapper; its root marker is `shop-icon-button`. Consumers no longer pass `shop-button-icon-only` themselves.

Product action usage:

```razor
<ShopButton Color="ShopColor.Surface" Class="shop-button-icon-only shop-product-card-action shop-product-card-cart"
            aria-label="@Strings.AddToCart" OnClick="OnAddToCartAsync">
    <ShopIcon Icon="@ShopIcons.Outlined.Shopping_Bag_01" />
</ShopButton>
```

1. Use component-prefixed kebab-case class names: `.shop-button`, `.shop-button-icon`, `.shop-button-primary`. This is the owner's selected convention; do not use BEM double underscores or double hyphens in project-owned class names. CSS custom properties retain their required leading double hyphen, for example `--shop-color-primary`.
2. Keep selectors shallow. Components own their internals; parent pages should not reach into deep child selectors.
3. Use CSS Grid/Flexbox for layout. Use media queries for presentation-only breakpoints; JavaScript resize subscriptions are not the default.
4. Preserve initial breakpoint behavior. For example, the existing image component changes hero/banner treatment below 600 CSS pixels. Do not substitute Bootstrap's breakpoints.
5. Use relative typography units while preserving the current geometry. Do not globally change root font size or convert every dimension without visual proof.
6. Use actual semantic headings independently of their visual size. Preserve `FocusOnNavigate` targeting a page heading.
7. Use pseudo-classes and attributes for native states: `:disabled`, `:focus-visible`, `[aria-expanded='true']`, `[aria-invalid='true']` where applicable.
8. Avoid routine `!important`. Retain exceptions only when justified, especially for third-party editor CSS.
9. CSS class order in HTML does not determine precedence. Do not claim that appending a consumer class makes it win. Use explicit component contracts, CSS variables, and controlled selector specificity.
10. Inline styles are for genuinely dynamic values, ideally constrained CSS custom properties, not a second source of static design rules.
11. Keep centralized SCSS. Do not introduce `.razor.css` isolation or hand-authored page CSS in `wwwroot/` during this migration.
12. Generated CSS is output, never the editing source. Verify the existing compiler's Debug and Release behavior.

#### Required naming convention

Use the component name as the prefix for its elements and variants. Combine the base class with variant classes; a variant does not replace the base class. Avoid generic global classes such as `.title`, `.primary`, or `.active`. Use native pseudo-classes and state attributes rather than duplicating browser state with classes.

```scss
// Styles/components/_button.scss
.shop-button { /* Shared button styles. */ }
.shop-button-icon { /* Icon inside a button. */ }
.shop-button-primary { /* Primary button variant. */ }
.shop-button-small { /* Small button variant. */ }

// Styles/components/_product-card.scss
.shop-product-card { /* Product card root. */ }
.shop-product-card-image { /* Product card image. */ }
.shop-product-card-title { /* Product card title. */ }
```

```razor
<button type="button" class="shop-button shop-button-primary shop-button-small">
    @Strings.AddToCart
</button>
```

CSS custom properties are a separate naming concern. Their leading `--` is required CSS syntax, not a BEM modifier:

```scss
.shop-button-primary {
    background-color: var(--shop-color-primary);
}
```

Keep selectors shallow; do not reconstruct the DOM hierarchy through long class names or nested descendant selectors. These examples define naming, not instructions to create unused classes or rename every existing file. Apply the convention to new and migrated project-owned styles. When converting an existing class, update its Razor/C# consumers, SCSS, JavaScript selectors, and tests together. Do not rename third-party-owned selectors. Legacy class names may coexist until their owning component is migrated.

`TheShop.scss` uses `@use` to load tokens, base, components, layouts, and utilities in a deliberate order. Sass modules have local scope: any partial needing a mixin/map imports its defining module itself. Listing it in the root does not make it globally visible.

Do not add CSS cascade layers solely for this migration. If layers are chosen for a demonstrated need, account for unlayered legacy/vendor styles outranking normal layered styles; verify the mixed period carefully.

### 5.4 Temporary theme compatibility

While Mud consumers remain, keep their theme provider, styles, scripts, and packages. It is acceptable for the new SCSS palette to temporarily duplicate unchanged legacy C# values; record this as a temporary bridge and delete the C# owners at final contraction.

Do not add new `--mud-*` consumers. New controls must use `--shop-*` tokens and work after vendor styles disappear.

Avoid broad resets that alter unconverted forms. During coexistence, scope new control rules to `shop-*` selectors and defer disruptive document defaults until affected shells are migrated. Compare converted and unconverted representative screens after shared CSS changes.

## 6. Common component contracts

### 6.1 Base classes and attribute forwarding

Use `ComponentBase` directly for simple components. A small `ShopComponentBase : ComponentBase` is justified for repeated visual-root forwarding and may expose only `Class`, `Style`, and an unmatched-attribute dictionary, conventionally named `AdditionalAttributes`.

- Migrate explicit `UserAttributes` call sites together with the component. Do not declare two competing unmatched-attribute capture parameters.
- Components derived from `InputBase<TValue>` use its form and attribute facilities; do not attempt multiple inheritance with `ShopComponentBase`.
- `BusyFor` is render-only and already derives from `ComponentBase`; it does not need a visual base.
- Inspect both `@inherits` and the `.razor.cs` partial when changing inheritance. Some existing types declare their base in Razor rather than C#.
- Forward attributes to their documented target. For a field, `id`, `name`, `autocomplete`, `aria-*`, and test identifiers usually belong on the actual input, not its wrapper.
- Expose `ContainerClass` or `InputClass` only where two styling targets are truly needed. Do not add many parameters to every control.
- Define merging precedence for `class`, `style`, `disabled`, and ARIA attributes explicitly. Blazor attribute splatting can overwrite explicit attributes depending on ordering. Test that consumer attributes cannot negate enforced disabled/busy semantics.
- Use a small, tested class-composition helper if current call sites need one. Do not keep Mud installed for `CssBuilder`/`StyleBuilder`, or recreate a large builder library.

`ShopCssClass.Join` trims and joins nonblank classes. `ShopCssClass.Modifier<TEnum>` converts a declared enum member to a component-prefixed kebab-case modifier, reusable by any component:

```csharp
private string ClassName => ShopCssClass.Join(
    "shop-button",
    ShopCssClass.Modifier("shop-button", Variant),
    ShopCssClass.Modifier("shop-button", Color),
    ShopCssClass.Modifier("shop-button", Size),
    Class);
```

For example, `Modifier("shop-button", ShopSize.Small)` returns `shop-button-small`. Undeclared enum values throw with the caller's expression as the parameter name; blank or whitespace-containing prefixes are rejected. The helper only composes class names: each component must provide matching SCSS and expose only supported choices. Keep native behavior validation (such as button type) separate. Do not build a CSS value registry, inline-style framework, or mutable fluent builder for static appearance.

### 6.2 Buttons and links

`ShopButton` owns shared visual treatments, disabled behavior, and loading presentation. Its API includes children, color, variant, size, button type, disabled, `Loading`, click callback, and optional icons. `ShopIconButton` forwards `Loading` to the shared button. Busy state remains caller-owned through `BusyState.RunAsync` and `BusyFor`; neither control injects the service, starts operations, nor owns a second busy flag.

```razor
<BusyFor Key="@BusyKeys.Auth.SignIn" Context="busy">
    <ShopButton Type="submit" Loading="@busy" Disabled="@(!CanSubmit)"
                EndIcon="@ShopIcons.Outlined.Arrow_Right_MD">
        @Strings.Auth_Login_Submit
    </ShopButton>
</BusyFor>
```

Loading contract (batch 16):

- Show only a centered spinner. Keep label and icons mounted in `.shop-button-content`, with `opacity: 0` while loading. This preserves intrinsic width/height and the accessible name; do not use `display: none`, `visibility: hidden`, or `aria-hidden` on the label.
- The spinner is absolutely centered, decorative, and sized from the button's existing icon-size value. Icon-only variants supply their own size. Keep `_spinner.scss` animation and reduced-motion behavior; loading is an owner-approved interaction decision, not a measured Figma state.
- Effective native disabled state and callback guard use `Disabled || Loading`. The component owns `aria-busy`; unmatched attributes cannot override these values. Completion does not clear an independently supplied `Disabled` condition.
- Keep one visually hidden, initially empty `role="status"` sibling per button; populate it with `Strings.Loading` while busy. It remains outside the busy button, so it neither changes the accessible name nor inherits the button's busy announcement deferral. No visible loading text or page-level duplicate status/spinner markup.
- The content span is an internal layout slot, not an outer button wrapper. `Class`, `Style`, unmatched attributes, and events continue to target the native button. `ShopIconButton.Label` remains its accessible name.

- Default the rendered `<button>` to `type="button"`; submit actions explicitly use `type="submit"`.
- Use the shared `ShopVariant`, `ShopColor`, and `ShopSize` contracts below.
- Loading disables duplicate activation and exposes meaningful busy state without losing the accessible label.
- Use `<a>` or `NavLink` for navigation. Preserve `Href` behavior from Mud buttons; do not turn every navigation link into `NavigateTo` click handlers.
- Anchor elements do not support native `disabled`. Do not rely on that attribute to block an action.
- No clickable `div` substitutes for buttons and links. No nested buttons/links in cards.
- Common components receive busy state from their caller; they do not dispatch commands or invent operation keys.

#### Shared visual choices (implemented in batch 7)

Shared vocabulary lives in `Common/UI/`, not `Theme/` or a button-specific namespace:

| Type | Values | Meaning |
|---|---|---|
| `ShopColor` | Primary, Secondary, Tertiary, Info, Success, Warning, Error, Surface | Color role; the component selects base/contrast tokens for its treatment. Check actual contrast separately. |
| `ShopVariant` | Filled, Outlined, Text | Visual treatment independent of color or behavior. |
| `ShopSize` | Small, Medium, Large | Relative component size, not global pixel dimensions. |

`ShopButtonVariant` and `ShopControlSize` were renamed, not retained as parallel enums. All live consumers migrate together; these Web-only types are not persisted or serialized into an external contract. There is no mixed-version enum adapter. `ShopButton` supports all eight colors with all three variants and text-button sizes; defaults remain Primary/Filled/Medium. Undefined enum casts throw rather than silently emitting unstyled classes.

```razor
<ShopButton Color="ShopColor.Error"
            Variant="ShopVariant.Outlined"
            Size="ShopSize.Small">
    @Strings.ManageProducts_BulkDelete
</ShopButton>
```

The output classes are `shop-button shop-button-outlined shop-button-error shop-button-small`. Use parameters to select treatments, not competing classes in `Class`. Destructive confirmation intent maps to `ShopColor.Error`; product image-overlay actions use `ShopColor.Surface`. The former `shop-button-danger` shortcut is removed because it forced a filled background regardless of variant.

Keep `ShopComponentBase` restricted to Class/Style/AdditionalAttributes. Components opt into only meaningful parameters. Do not add unused visual parameters to icons/inputs just to demonstrate enum reuse, force Text onto components that do not support it, or make dialog width share control dimensions. Inputs derive invalid styling from validation, not a caller-selected error color. A genuinely different contract may use its own enum.

Each component owns its CSS mappings. `_button.scss` uses a private Sass map and `@each` to emit component-prefixed color/variant combinations referencing existing tokens directly. This is compile-time repetition removal, not a global palette service, CSS utility framework, or restored runtime alias layer. Do not add a shared Sass abstraction until another real component needs the same recipe.

Button color/state policy (batch 8 supersedes the earlier text-color overrides):

- Filled resting backgrounds use the corresponding Figma primary/secondary/tertiary/status swatch and labels use that role's `*-contrast` token directly. The four new semantic contrast tokens are white. Surface remains the neutral exception: surface-default background with text-primary foreground.
- Outlined/Text labels use the base role token directly (`--shop-color-primary`, `--shop-color-error`, etc.), not contrast tokens and not separate `*-text` overrides. Their resting background remains transparent. Surface retains text-primary. Outlined secondary/status borders use their base role color; primary/tertiary/surface outlined borders retain the existing lines-default treatment. Text has no border/shadow.
- Outlined/Text hover and pressed backgrounds remain surface-subtle and tertiary. Filled Primary retains action-hover/action-pressed; Surface retains surface-subtle/tertiary; Tertiary uses tertiary-lighter/lines-default. Filled Secondary/status roles preserve their darker interaction backgrounds through dedicated `*-hover` tokens, with the role's contrast token for labels in both hover and pressed states. These state tokens do not supply label or border colors. A distinct pressed shade is not claimed as a measured Figma state.
- Color-specific selectors exclude `:disabled`. Disabled foreground/background, focus, and forced-colors behavior remain shared. Color selection does not change activation, busy, form submission, or dialog completion behavior.
- Known contrast gaps are explicitly recorded below. Transparent variants inherit the caller's surface; dark or photographic backgrounds require separate verification. Do not claim that token-contract tests passing means all color combinations satisfy accessibility requirements.

State values live in `_theme.scss`; the 22 Figma paint styles and all existing action tokens remain unchanged:

| Token suffix (`--shop-color-`) | Value | Purpose |
|---|---|---|
| secondary-hover | `#595959` | Filled secondary hover/pressed background. |
| info-hover | `#075ca8` | Filled info hover/pressed background. |
| success-hover | `#146c37` | Filled success hover/pressed background. |
| warning-hover | `#805400` | Filled warning hover/pressed background. |
| error-hover | `#b42318` | Filled error hover/pressed background. |

The separate secondary/info/success/warning `*-text` tokens and `secondary-contrast-accessible` override were removed. Batch 9 also removes error-text: form validation uses error directly. Do not recreate these overrides. State values are inferred interaction decisions, not new Figma styles or one-to-one application aliases. Preserve the previously measured primary text-button and Filled/Medium icon-button geometry.

Known accessibility limitation, accepted by the owner as a temporary palette choice: white resting labels on secondary/info/success/warning/error fills fall below 4.5:1 for 14px text. Base-color outlined/text labels for those roles and tertiary also fall below 4.5:1 on the tested white/subtle/tertiary surfaces. The browser test measures and reports these known gaps rather than claiming AA compliance; other enabled pairs retain the 4.5:1 assertion. Resolve the palette before production accessibility sign-off. The prior batch-7 blanket contrast result is historical and no longer applies to this mapping.

#### Icon-only actions (implemented in batch 9)

Use `ShopIconButton`, not `ShopButton` plus a caller-supplied icon-only class. It composes the shared button and decorative icon, renders one native button without a wrapper, and exposes `Icon`, `Label`, `Color`, `Variant`, `Size`, `Type`, `Disabled`, and `OnClick`, alongside the visual base attributes. `Icon` must be a trusted `ShopIcons` fragment and `Label` a nonblank resource-backed accessible name; missing values throw. Defaults are Primary/Filled/Medium and native type button.

```razor
<ShopIconButton Icon="@ShopIcons.Outlined.Heart_01"
                Label="@Strings.Wishlist_Add"
                Color="ShopColor.Surface"
                Variant="ShopVariant.Filled"
                Size="ShopSize.Small"
                OnClick="OnToggleWishlistAsync" />
```

All three `ShopSize` values work with all three `ShopVariant` values and all eight colors. Size is component-relative, not a shared pixel scale. `_icon-button.scss` owns the following Figma measurements from component set `322:6450`; entries are **square frame / padding / square icon**, in pixels:

| Variant | Small | Medium (default) | Large |
|---|---|---|---|
| Filled | 30 / 5 / 20 (`322:6466`) | 36 / 6 / 24 (`322:6472`) | 42 / 5 / 32 (`322:6475`) |
| Outlined | 28 / 4 / 20 (`322:6457`) | 34 / 5 / 24 (`322:6460`) | 40 / 4 / 32 (`322:6463`) |
| Text | 24 / 3 / 18 (`322:6449`) | 48 / 12 / 24 (`322:6451`) | 56 / 12 / 32 (`322:6454`) |

The 42px Filled node is mislabeled Medium alongside the 36px Medium in Figma. Mapping it to Large is an explicitly recorded inference from the ordered trio and 32px icon, not an exact variant-name match. Figma was not edited. Preserve these nonuniform dimensions rather than deriving them from text-button sizes. Product-card actions remain Filled/Medium at 36px; the dialog close action is Text/Medium and intentionally changes from 36px to 48px.

`Label` controls the rendered aria-label and takes precedence over conflicting aria-label/aria-labelledby attributes. Other attributes, Class, and Style forward to the actual button. Attribute merging does not mutate the caller's dictionary; shared button enforcement still prevents overriding disabled state, type, or click handling. Color, interaction, focus, forced-colors, and disabled behavior remain owned by `_button.scss`; only icon-button geometry belongs in `_icon-button.scss`. Do not duplicate these recipes or place them in product-card styles.

### 6.3 Icons

Keep `ShopIcons` as the trusted application SVG registry. Implement a small `ShopIcon` renderer with correct `viewBox`, dimensions, and `currentColor` behavior. Inspect whether each registry entry is a fragment or complete SVG before wrapping it. Preserve image asset URLs separately from SVG markup.

Decorative icons are hidden from assistive technology. Icon-only controls receive resource-backed accessible names on the control. Never render arbitrary user-supplied SVG/HTML as trusted markup.

`ShopIcon` must not be independently focusable. Explicitly suppress `tabindex` (including unmatched caller overrides), retain `focusable="false"`/`aria-hidden="true"`, and use `pointer-events: none` on `.shop-icon` so clicks target the owning button/link. Do not use `tabindex="-1"` for this decorative contract: it removes sequential Tab access but still permits click focus in Chromium. Preserve the owning control's keyboard-focus indicator.

### 6.4 Text and layout

Use semantic `<h1>` through `<h6>`, `<p>`, `<span>`, `<section>`, `<article>`, `<nav>`, `<main>`, `<header>`, and `<footer>` as appropriate. Apply typography classes when visual hierarchy differs from document hierarchy.

Do not create `ShopText`, `ShopStack`, or `ShopGrid` merely to reproduce Mud's API. CSS supplies basic layout and text styling. Extract components for actual repeated behavior or meaningful compositions.

## 7. Forms and validation architecture

This is a behavior migration, not a tag substitution. Handle validation deliberately before replacing all nine forms.

### 7.1 Form owner

Each form owns a mutable Web model and a stable `EditContext`. Keep the context across normal renders. Recreate it only when intentionally loading/resetting a different model; detach old event handlers and reset associated message stores then.

Use `EditForm` with either `Model` or `EditContext`, not both. Use built-in `InputText`, `InputTextArea`, `InputSelect`, `InputCheckbox`, `InputNumber`, `InputDate`, and `InputFile` where their behavior matches the requirement. Plain HTML inputs used inside a validated form need explicit field-notification/validation integration; `@bind` alone does not reproduce every `InputBase` behavior.

For custom inputs based on `InputBase<TValue>`:

- Preserve `Value`, `ValueChanged`, `ValueExpression`, field identification, parsing errors, and validation CSS.
- Handle null, empty, invalid, and culture-sensitive values deliberately.
- Notify the editing context through supported APIs; do not keep a disconnected private validity flag.
- Verify the installed .NET 10 contract for the chosen input. Non-form catalogue filters can remain simple controlled inputs with callbacks.

### 7.2 Validation responsibilities

1. UI parsing and immediate field feedback belong in Web.
2. Existing Application validators remain authoritative for command/query validation within Application.
3. Domain invariants remain in Domain; existing server/database enforcement is preserved.
4. Do not copy all business rules into UI annotations or move business rules into a generic field.
5. Preserve existing shared validation helpers. Simple presentation checks can use DataAnnotations or focused validators attached to `EditContext`; choose the smallest integration that matches existing forms.
6. Map recognized Application error keys/arguments to fields with `ValidationMessageStore`. Show unmapped errors in a form-level message.

Current limitation: `Application/Common/Behaviors/ValidationBehavior.cs` returns the first failure through `Result.Error` and `ErrorArgs`. It does not return a universal dictionary of all field errors. Preserve this contract initially. Use explicit feature mappings where needed; do not parse localized English messages to infer fields or redesign `Result` just to complete this UI migration.

For asynchronous validation, await it explicitly in the submit path, then populate messages and decide whether to dispatch. Do not put unawaited async work in a synchronous validation event and assume `OnValidSubmit` waits for it. Prevent reentrant submission, clear stale relevant errors after editing, and avoid running the same command twice through both click and submit handlers.

### 7.3 Required UX contracts

- Give each field a stable unique ID and a real `<label for="...">`, or another explicit accessible-name association when a visible label is intentionally absent.
- `ShopFieldLabel` currently renders text and has no `for` contract. Replace that behavior; placeholders are not labels.
- Associate hints/errors with `aria-describedby`, and invalid inputs with `aria-invalid`.
- Preserve required semantics and error messages. A required boolean confirmation must require `true`; merely decorating a non-nullable bool as required is insufficient.
- Decide native browser validation versus custom validation consistently. If using `novalidate` to avoid duplicate browser bubbles, preserve required semantics and equivalent accessible errors.
- Retain invalid entered values where users need to correct them. Do not silently convert invalid money/date input to zero or today.
- Ensure Enter submits once, non-submit buttons do not submit, busy prevents duplicates, and form-level errors are discoverable.
- Preserve navigation-away/dirty-form warnings where currently implemented.

### 7.4 Specialized inputs

**Money:** retain `decimal?`, current currency formatting policy, null/empty behavior, domain constraints, and typed callbacks. A browser number input cannot display grouped currency text like `1,234.50` as its numeric value. Deliberately choose an editable numeric representation or a text input with `inputmode="decimal"` and tested parsing. Test culture, fractional values, zero, invalid text, and round trips. Do not introduce a new rounding rule.

**Date of birth:** preserve current maximum date, required behavior, editable input, age confirmation, and command mapping. Use date-only semantics at the UI boundary; do not introduce timezone conversion. A native `InputDate` can replace the picker when acceptable, but its popup appearance varies by browser and cannot preserve Mud's `OpenTo.Year` interaction exactly. Record this bounded native-control difference and verify practical birth-year entry; do not silently omit the control or build a calendar framework.

**OTP:** keep string representation so leading zeroes survive. Preserve paste, digit replacement, repeated digits, backspace, arrow movement, autofocus policy, disabled state, completion callback, resend behavior, and cleanup. Prefer text inputs with numeric input hints over treating the entire OTP as a number. Re-evaluate existing JavaScript interception because it contains Mud-specific workarounds; do not preserve competing DOM writes and Blazor binding accidentally.

## 8. Dialogs, notifications, loading, and browser modules

### 8.1 Host lifetime

`ShopUiHost` composes `ShopDialogHost`, `ShopNotificationHost`, and the existing loading overlay. It is mounted once in each mutually exclusive Main/Auth layout. Do not also mount duplicate hosts in `App` or every page. If layouts can nest, ensure only the outer owner mounts hosts.

Services are Web-only and registered by `AddPresentation`. In WebAssembly a scoped service generally survives client-side route changes, so define cleanup explicitly; layout disposal does not imply service disposal.

### 8.2 Dialogs

Implement `ShopDialog` with native `<dialog>` and a small ES module for `showModal()` and `close()`. An `open` attribute alone creates a different, non-modal behavior. Avoid inventing a full focus-trap/portal framework when the browser supplies modal mechanics.

Native dialogs support the existing confirmation and variant-image selection use cases. The shared service owns confirmations; `ProductVariantsCard` owns its single picker directly. Do not widen the service into a generic component/parameter framework or put product DTOs in its core API. Preserve `VariantImagePinResult` semantics, including the difference between cancellation and an accepted empty image selection.

Current checkpoint (batch 10): **confirmations and variant-image selection are native**. `IShopDialogService.ConfirmAsync(ShopConfirmationOptions, CancellationToken)` returns a `bool`: true only for explicit confirmation, false for every dismissal/cancellation path or when no host exists. `ProductVariantsCard` injects only that confirmation service and conditionally mounts its feature-owned `VariantImageDialog`, composing the existing `ShopDialog` chrome. No Mud dialog service or provider remains in application consumers.

```csharp
var confirmed = await DialogService.ConfirmAsync(new(
    Strings.ProductForm_UnsavedChangesTitle,
    Strings.ProductForm_UnsavedChangesBody,
    Strings.ProductForm_LeaveAnyway,
    Destructive: true));
```

The scoped service is registered once, with `IShopDialogService` resolving the same instance as `ShopDialogService`. Requests are queued FIFO and completed by identity; stale events cannot confirm the next request. Cancellation tokens remove either active or queued requests. Host disposal, completed navigation (`LocationChanged`, not the pre-navigation guard), and layout ownership transfer cancel outstanding requests. Layout handoff may initialize the new host before disposing the old one; only the current owner renders requests, and late disposal of the old host cannot cancel the new host's requests.

`ShopDialog` imports `shopDialog.js` after rendering, calls `showModal()`, and handles native cancel/close. Initial focus is the safe Cancel action. Backdrop click cancellation preserves Mud's default; Escape is now explicitly supported. JS restores focus, disconnects listeners/observers, and handles element removal; CSS modal-state selectors release scroll locking automatically. Browser initialization failure logs the error and cancels the caller instead of leaving its task pending. No form submission is involved.

The Figma dialog chrome at `2680:14477` supplies 500px width, 24px section padding, dividers, and H5 title styling. Confirmation copy/action ordering is retained from the application. The shared Text/Medium icon-button is 48px after batch 9 (previously 36px), making the header taller than Figma's smaller dialog close icon. This uses the inspected icon-button variant rather than bespoke dialog geometry. Backdrop opacity, destructive-action hover, Cancel-first focus, and responsive/overflow behavior are explicit implementation/accessibility decisions, not additional Figma tokens. Shared destructive button treatment belongs in `_button.scss`, not `_dialog.scss`. Runtime typography and colors consume the existing Figma tokens directly.

Batch 10 removes `LegacyShopDialog` and its `.shop-legacy-dialog` overrides, plus `MudDialogProvider` from both layouts. Keep Mud theme/popover/snackbar providers, registration, packages, and assets for other unmigrated consumers; this is not final package contraction.

#### Shared dialog sizing and slots (batch 11)

`ShopDialog` exposes exactly three content fragments: `TitleContent`, `DialogContent`, and `DialogActions`. The former string `Title`, implicit `ChildContent`, and `Actions` parameters are removed; migrate callers together. `TitleContent` supplies localized, meaningful title markup, normally a semantic heading with `shop-dialog-title`. The component labels the native dialog through a unique wrapper ID, without forcing a nested heading or including the close button in its accessible name. Do not pass untrusted HTML as `MarkupString`.

```razor
<ShopDialog MaxWidth="ShopMaxWidth.Medium" OnDismiss="CancelAsync">
    <TitleContent>
        <h2 class="shop-dialog-title">@Strings.VariantImage_Title</h2>
    </TitleContent>
    <DialogContent>
        @* Feature-owned fields or content. *@
    </DialogContent>
    <DialogActions>
        <ShopButton Variant="ShopVariant.Text" OnClick="CancelAsync"
                    data-dialog-initial-focus="true">@Strings.Cancel</ShopButton>
    </DialogActions>
</ShopDialog>
```

`Common/UI/ShopMaxWidth.cs` is independent of `ShopSize`; other components may opt into it when needed. It carries names only, with no `Description` attributes or numeric CSS values. `ShopCssClass.Modifier` generates `shop-dialog-width-extra-small`, etc.; invalid enum values fail clearly. Do not introduce a Mud width dependency or per-component switch tables.

| `MaxWidth` | Maximum inline size |
|---|---|
| Omitted / `null` | 500px (`--shop-dialog-max-width`, theme default) |
| `ExtraSmall` | 444px |
| `Small` | 600px |
| `Medium` | 960px |
| `Large` | 1280px |
| `ExtraLarge` | 1920px |
| `ExtraExtraLarge` | 2560px |
| `None` | No named cap; viewport gutters remain |

The six named values live only in `tokens/_sizing.scss`, generating `--shop-max-width-{kebab-case-name}`. They are owner-approved sizes, not claimed Figma measurements or responsive breakpoints. `_theme.scss` retains the Figma 500px default. `_dialog.scss` consumes these tokens and separates available width from the cap: `inline-size: calc(100% - 2 * var(--shop-space-4))`. With the current spacing scale, every choice retains 16px per-side gutters; `None` does not mean edge-to-edge. Confirmation omits `MaxWidth`; the owner's subsequent picker change explicitly selects `ShopMaxWidth.Small` (600px). Preserve that choice; the Figma 500px picker measurement is historical, not its current cap.

Only the open dialog uses column flex layout. Header and actions do not shrink or scroll; only `.shop-dialog-content` scrolls, using `min-block-size: 0` and `overflow: auto`. It is a named, keyboard-focusable region so text-only bodies can be scrolled without a mouse. The dialog itself clips overflow and respects the dynamic viewport height; short content remains content-sized. Do not apply `display: flex` unconditionally to a closed native dialog. Keep titles/actions concise enough to fit the viewport: fixed chrome taller than the entire viewport cannot leave useful body space. Preserve modal inertness, safe-action focus, Escape/backdrop/close cancellation, and focus restoration.

#### Variant-image picker ownership

- `VariantImageDialog.OnCompleted` is a typed `EventCallback<VariantImagePinResult?>`: null cancels; a nonnull result with null ImageId explicitly clears the pin. The picker dispatches completion once per mounted instance. Clicking the selected image toggles it off; Save remains available for an empty gallery.
- `ProductVariantsCard` snapshots the selected variant and shared option-value scope in private request state. A request-identity guard ignores stale callbacks after reopening, disables opening a second picker, and rejects saves when disabled, when the variant has disappeared, or when a returned image no longer belongs to the gallery. Only Save invokes the existing state callback. No command or database contract changes.
- Ownership ends when the card unmounts; there is no outstanding dialog-result task to strand. Normal dismissals return null. Existing `ShopDialog` teardown restores focus and releases scroll locking. If another native modal opens, the shared module notifies the previous owner before closing it, so a picker cannot remain logically open behind an unsaved-changes confirmation.
- Native buttons represent gallery tiles with `aria-pressed`, unique resource-backed names (`VariantImage_ImageLabel`), and visible selection/focus cues. Native radios in a fieldset replace the previous mutually exclusive checkboxes. This semantic change preserves the two scope choices and adds arrow-key operation; it is an intentional difference from Figma's checkbox artwork.
- `_variant-image.scss` owns picker layout. Reinspected Figma `2680:14477`: 500px dialog, 24px sections, 32px body gaps, 16px gallery gaps, 120px square thumbnails, 8px label/name gap, and 48px scope rows. The gallery wraps on narrow screens. The existing 48px close control and native radio appearance are documented differences; generic button/icon geometry is not overridden here. A selected border paints above the image via a pseudo-element without shrinking the thumbnail. Shared image fallback and preset behavior remain unchanged.

Confirmation-service contract (the feature-owned picker's lifecycle is described above):

- A request completes exactly once on confirm, cancel, Escape, navigation, host disposal, or cancellation token cancellation.
- Never leave a caller awaiting a task after its host unmounts.
- Handle simultaneous requests explicitly, for example a serialized queue; do not silently replace the active request.
- Open only after the element exists. Synchronize C# state with native `cancel` and `close` events.
- Prevent an unintended form submit when dialog buttons are inside another form. Do not create nested forms.
- Associate title/description; choose appropriate initial focus; support Escape and restore focus to the trigger or a valid fallback.
- Preserve the existing decision about backdrop dismissal for destructive confirmations.
- Retain background inertness, scroll behavior, and cleanup after dismissal.
- Show a stable busy state during confirmed operations where the current behavior needs it; ensure errors do not strand the modal.

### 8.3 Notifications

Implemented in batch 12: `IShopNotificationService.Show(string, ShopNotificationKind)` replaces every application `ISnackbar` call. `Common/Notifications/` owns the scoped service, immutable message record, and Info/Success/Warning/Error enum. Keep resource resolution at existing Web boundaries; this kind expresses message meaning, not a replacement for shared `ShopColor` or `ShopVariant`.

- Render one notification host with appropriate live-region behavior. Use polite announcements for routine status; reserve urgent alerts for actual urgent errors.
- Support dismissal and bounded lifetime. Dispose timers and subscriptions. Do not let messages accumulate indefinitely.
- Preserve success/error/warning messages and important navigation transitions.
- Inline field or form errors remain visible near the failed operation; notifications are not the only form-error channel.
- Plain notification text must remain encoded. Do not add arbitrary HTML message rendering.

```csharp
Notifications.Show(Strings.Auth_CodeSent, ShopNotificationKind.Success);
Notifications.Show(Localizer[result.Error!], ShopNotificationKind.Error);
```

Current contract:

- One layout-owned host renders a persistent polite live region. All current callers report routine operation results, so errors also use polite announcements; no new urgent-alert API is needed. Publishing never moves focus. Host content is inserted after the initial render so the live region exists first. Assistive-technology announcement behavior still needs manual screen-reader review.
- Five-second lifetime matches the installed Mud default visible duration; no enter/exit animation is copied. Hover or focus pauses expiration independently. Leaving both grants five fresh seconds. A `TimeProvider` seam supports deterministic tests; each message timer is disposed on removal, eviction, or service disposal.
- At most five messages, oldest first. Exact active text+kind duplicates coalesce without extending their lifetime. Overflow evicts the oldest unpaused message; if all five are paused, the incoming message is ignored. These explicit bounded-storage choices avoid unbounded queues and preserve active reading/focus. Current multi-result operations publish at most two messages.
- Messages survive same-layout navigation and Auth/Main host handoff for their remaining lifetime. New ownership hides the old host; late disposal/events from the old host cannot affect the new owner. Unmounting resumes paused timers rather than leaking them; publishing without a mounted host is bounded by the same expiry.
- `ShopNotification` owns encoded text, a resource-named native dismiss button using `ShopIcon`, and independent hover/focus tracking. Info/Success/Warning/Error retain their service/model meaning, but render identical markup and appearance: no kind label, icon, or severity accent. Keep `ShopNotificationKind` and callers unchanged when adding future kind-specific presentation. No custom HTML payloads, action callback framework, placement matrix, or replacement vendor library.
- Figma source: [Snackbar, node 2948:17575](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2948-17575). Use `--shop-color-primary` background, `--shop-color-primary-contrast` text/icon, `subtitle-2` typography (14px/500), 24px padding/gap, square corners, and an 18px Close_MD icon. No border or shadow. The example is 241×66px; this is content-sized, not a fixed width/height for every message.
- `_notification.scss` owns bottom-center placement, responsive wrapping, and overflow. `_theme.scss` owns the application 24rem width cap, 18px Figma icon size, and notification layer `1500` (raised from `1400`). The 16px viewport gutter and width cap are application choices, not measured Figma geometry. Native modal top-layer ordering still takes precedence over z-index.
- Dismiss chrome is a native button local to the notification, not a shared button color/variant override. A 24px hit target extends around the 18px icon footprint without changing Figma padding or gap. The focus ring uses primary contrast for visibility on the dark bar. Long copy and 200% text wrap; stacked notifications scroll within the viewport.
- Both `MudSnackbarProvider` mounts are removed. `AddMudServices`, remaining providers, and Mud `Severity` values in inline `MudAlert` markup remain for unconverted controls; do not remove those until their consumers migrate. The E2E page object's `Snackbar` property temporarily retains its name but now locates `[data-testid=notification]`.

Browser verification must run **headed** (visible browser), per owner instruction. Set `E2E_HEADLESS=0`; do not use headless runs for subsequent migration batches unless the owner changes this preference.

### 8.4 Loading

Retain `BusyState`, `BusyKeys`, and `BusyFor`. Replace only their visual children and overlay implementation. A small CSS spinner or skeleton is sufficient.

Sign-in keeps its inline spinner and `aria-busy`/disabled behavior. Its resource-backed `role="status"` message uses `.shop-visually-hidden` from `base/_accessibility.scss`: available to assistive technology, without a visible extra “Loading…” label beside Login.

Do not add competing per-page busy flags. Pass `BusyFor`'s value into `ShopButton.Loading` or `ShopIconButton.Loading`; the controls centralize spinner-only appearance, disabling, and hidden status text, not operation tracking. Keep form inputs and related controls bound to the same busy value. Component-local interaction state such as disclosure expansion is unrelated and can remain local. Preserve cleanup on success, failure, and cancellation. Verify global blocking behavior separately from inline button loading.

### 8.5 JavaScript boundaries

- Use ES modules imported through `IJSRuntime`, initialized after rendering.
- Prefer element references over document-wide queries and generated selectors.
- Dispose listeners, observers, modules, and .NET references; tolerate legitimate teardown races without hiding all errors.
- Blazor owns rendered application markup/state. JavaScript performs focused browser tasks.
- The existing rich-text editor is an intentional exception: it owns descendants of its dedicated empty host element. Preserve that boundary and cleanup.
- Retain existing image/upload/editor modules only where their behavior remains necessary. Do not delete a module until its consumers and tests have been migrated.
- Do not remove sanitization or turn plain user content into `MarkupString` during this migration.

## 9. Component replacement map and behavior checklist

| Existing Mud surface | Destination | Preservation focus |
|---|---|---|
| Stack/Grid/Item/Spacer/Layout/MainContent | Semantic containers and Grid/Flexbox | Responsive geometry, scroll ownership, min sizes, source order |
| Text | Semantic HTML plus typography styles | Heading outline, wrapping, tracking, line height |
| Button/IconButton/Link | Native controls or small `ShopButton` | Navigation vs action, keyboard, submit behavior, accessible name |
| Icon/Avatar | Trusted SVG renderer or image/initials markup | Sizing, fallback, decorative semantics |
| Paper/Divider/Chip | Styled section/div/hr/span as appropriate | Meaningful semantics; clickable chips become buttons/radios |
| Form/TextField/Select/NumericField/DatePicker | Blazor form primitives and focused custom inputs | Binding, parsing, labels, validation, disabled state |
| CheckBox/ChipSet | Native checkbox/radio groups or selection buttons | Single vs multi-selection, required-true confirmation, selected state |
| Table/Th/Td | Semantic table with controlled selection | Headers, bulk selection scope, paging, responsive overflow |
| ExpansionPanels/ExpansionPanel | `details/summary` when suitable, otherwise button/disclosure | Keyboard, expanded state, independently open sections |
| RangeSlider | Two labeled native range inputs or focused range component | Two values, min <= max, step, keyboard, labels, callbacks |
| Pagination | Native navigation/control composition | One-based pages, valid range, ellipses, current-page semantics |
| Breadcrumbs/BreakpointProvider | Ordered breadcrumb navigation with owned responsive behavior | Links, current page, intermediate-item access |
| Menu/Popover | Native disclosure containing links/buttons | Focus, Escape, outside dismissal, visibility, permissions |
| Dialog/providers | Native dialog and project host/service | Completion, focus, cancellation, lifecycle |
| Snackbar/provider | Notification service and host | Announcements, timeouts, dismissal, messages |
| Overlay/ProgressCircular/Skeleton | Owned CSS and existing busy state | Blocking behavior, reduced motion, loading announcements |
| FileUpload | `InputFile` plus existing upload model | Limits, bytes, same-file reselection, preview URL lifetime |
| Image | Native image/picture as appropriate with existing preset API | Geometry, fallback, source races, accessibility |
| ThemeProvider | CSS tokens/base styles | All used variable values exist without Mud assets |

### Tables and bulk actions

Keep existing feature-specific tables. Extract shared table machinery only if a stable repeated need emerges. Do not build a generic grid engine.

Preserve selection by stable entity ID, select-all scope, clearing selection after operations, partial bulk failures, reference-blocked deletions, row-specific errors, and keyboard access. Preserve current sort/filter/query-state behavior rather than replacing it with client-only filtering. Use proper headers and accessible checkbox labels. Give overflow containers usable keyboard/scroll behavior on narrow screens.

### Filters, sorting, pagination, and breadcrumbs

- Keep `ShopSortSelect` typed enum/option contracts and localized labels.
- Preserve `ShopFilterPanel` single-select, multi-select, clear, and range behavior. A single-select group should have intentional radio/clear semantics; do not accidentally change its clear behavior.
- `MudRangeSlider` has two thumbs; a single native range input is not equivalent. Keep both ends, prevent crossing, and avoid duplicate/debounced query dispatch changes without verification.
- Keep catalogue and admin query-state records and `QueryStatePageBase`. Verify Back/Forward and cancellation of superseded loads.
- Preserve `ShopPagination` one-based `Page`, `TotalPages`, and `PageChanged`. Test zero results, one page, boundaries, and changing totals.
- Replace Mud `BreadcrumbItem` in both `BreadcrumbTrail` and `BreadcrumbState` with a Web-owned record. Preserve responsive collapsing or provide an equally usable overflow/disclosure without losing intermediate navigation. Inspect existing `ShopBreadcrumbs` breakpoint handling.

### Menus and navigation

Use an ordinary disclosure with links/buttons for `ProfileMenu` unless implementing the full ARIA application-menu keyboard pattern. Do not add `role="menu"` to a normal list and omit its keyboard contract. Preserve authorization-controlled links, sign-out, return URLs, and accessible account labels.

### Images

Keep `ShopImagePreset` names and effective treatment, including the existing `ProductDetail` preset even if its eventual page is absent. Preserve contain/cover, aspect ratios, source changes, mobile artwork selection, meaningful versus decorative alternatives, and stable fallback geometry.

Native image error events may simplify the current observer workaround. Verify late failures from an obsolete source cannot replace a newer valid image. Avoid downloading duplicate desktop/mobile artwork unnecessarily where a compatible `<picture>` design can preserve behavior. Lazy-load offscreen images; do not automatically lazy-load the primary above-the-fold image. Reserve geometry and verify broken-image behavior in the browser.

### Uploads

Preserve `ShopUploadedImage`, existing callbacks, ordering/primary-image behavior, persisted images, single-selection replacement, multiple selection, and per-file error rows. Current defaults include 10 files and 2 MiB per file, but read live values and call-site overrides before implementing.

- Read selected `IBrowserFile` streams before resetting/replacing their underlying input; references to files from a replaced selection may become invalid.
- Keep explicit `OpenReadStream` limits and do not trust `accept` or browser-reported MIME type as the sole security check.
- Preserve object URLs and revoke them on removal, replacement, and disposal. Do not replace previews with large base64 strings in component state.
- Preserve same-file reselection and guard against overlapping picks.
- Keep file validation failures visible, not silently dropped.
- Preserve existing browser picker/drop behavior where implemented, including keyboard access.

### Product editor and rich text

Preserve option/variant generation, prices, status-dependent validation, specifications, image pinning/fan-out, gallery order, dirty navigation, and existing `Result.ErrorArgs` mappings. Retain `ProductForm` as the shared create/edit composition.

Keep Quill and its modules/assets. Update only the surrounding UI contract, Mud styling dependencies, and field associations. Preserve sanitized persisted HTML, supported formatting, paste behavior, and editor cleanup. Editor content must not gain new trust because controls were rewritten.

## 10. Governance and automatic checks without SDD stages

The old canonical constitution requires Mud-only components, `MudText`, Mud color APIs/utilities, and `MudComponentBase`. The design script also rejects native text tags. Those rules must be aligned with the new architecture, not merely ignored.

Inspect and update only the relevant canonical files:

- `.sdd/skills/theshop-constitution/SKILL.md`
- Its directly applicable design/component/style/theme references and design checklist.
- `.sdd/scripts/check-design-rules.ps1` and any tests asserting the changed rules.

Replace the conflicting requirements with native semantic markup, project tokens, form accessibility, narrow component contracts, and the accepted SCSS locations. Keep existing architecture, route, resource, authorization, and documentation checks. Review the native-HTML rule, Mud field-label rule, Mud-specific inheritance rule, and class/style composition expectations in particular.

Do not add blanket `design-rules: ignore` comments across migrated files. Do not disable hooks. Do not edit generated adapters to create a second authority. This work is targeted policy/tool alignment for the selected architecture, not an SDD process rollout.

Inspect hook side effects and check the diff after commands that might run formatting. No numbered branch, handoff token, feature registry entry, or `.specs` stage record is required by this migration.

## 11. Phased implementation plan

For every phase: record changed files, run appropriate checks, inspect the diff, and leave a compilable state. Phase identifiers below are for tracking only. Do not create placeholder implementations that silently omit existing behavior.

### P0: Baseline and inventory

1. Reconfirm branch, commit, user edits, available tooling, and actual feature files.
2. Map every Mud component/type/service/CSS/JS/test dependency.
3. Record existing public `Shop*` APIs and their consumers.
4. Establish a baseline build and non-destructive tests. Separate existing failures from migration failures.
5. Capture representative current screens and interaction states before changing shared styles, using the same data and viewport for later comparisons.
6. Record resource/font availability and any unavailable browser/backend environment.

Exit: inventory and baseline evidence exist; no unexplained assumptions about missing features or tests.

### P1: Rules and visual foundation

1. Align conflicting canonical design rules/checks as described in section 10.
2. Create tokens, minimal base partials, and the selected stylesheet ownership structure.
3. Preserve current palette/typography through an explicit value mapping.
4. Add the small styling helper/base and icon renderer only as needed by initial consumers.
5. Keep legacy Mud assets/providers for remaining screens and avoid broad reset regressions.

Exit: old screens still build/render; new token styles work independently; design checks permit the intended native architecture.

### P2: First complete vertical slice

1. Implement shared button/field/validation foundations needed by one existing form.
2. Migrate a representative form such as sign-in, including validation, busy, notification, submit, and navigation behavior.
3. Migrate a simple storefront composition such as the product card, including image behavior and navigation.
4. Migrate one confirmation dialog, including result, focus, and cancellation behavior.
5. Update relevant tests to exercise DOM behavior. Compare browser states to baseline.

Exit: real form, content, and modal examples prove the contracts. If a contract fails here, fix it before mass conversion.

### P3: Shared interactions and shell

1. Finish dialog and notification services/hosts and their lifecycle behavior.
2. Migrate loading presentation, breadcrumbs, app bar, profile menu, footer, access/authorizing views, and layouts.
3. Mount a single shared host per active layout; retain any required Mud providers for unconverted descendants.
4. Replace Mud-specific breadcrumb data types and related tests together.
5. Verify route transitions, authorization rendering, notifications across navigation, global loading, and document scroll behavior.

Exit: native shell works with converted and remaining legacy screens; no duplicate active providers/hosts for the same new service.

### P4: Authentication and common controls

1. Migrate remaining sign-in/sign-up/verification forms.
2. Implement OTP and date-of-birth contracts, age confirmation, resend/error paths, and input accessibility.
3. Migrate money, sort, pagination, filter/disclosure/range, upload, and image controls needed by existing features.
4. Remove only browser workarounds that are obsolete and covered by replacement behavior tests.

Exit: authentication and common-control behavior pass component and relevant browser checks; no lost validation or keyboard operation.

### P5: Catalogue and admin workflows

1. Migrate catalogue layout, filters, results, sorting, pagination, empty/error/loading states, and URL history behavior.
2. Migrate brand/category/product listings and their selection/bulk actions.
3. Migrate brand/category forms and the product form, variant/specification content, image pin dialog, and rich-text wrapper.
4. If product-details/gallery files exist, migrate their actual behavior and tests; otherwise record not applicable.
5. Preserve every authorization boundary and Application command contract.

Exit: all existing routes/features render native UI and pass their behavior checks. No hidden legacy control remains in error, empty, unauthorized, or loading branches.

### P6: Dependency contraction

Only start after all live consumers have migrated.

1. Remove Mud and extension package references and any transitive references introduced elsewhere.
2. Remove `AddMudServices`, `AddMudExtensions`, imports, providers, cascading Mud instances, enums, utilities, and legacy test setup.
3. Remove Mud CSS/JS host links and old theme C# types after verifying no references remain.
4. Replace remaining `mud-*`, `--mud-*`, and vendor-supplied utility usage in markup/SCSS/JS/tests.
5. Consolidate applicable `app.css` rules into SCSS; remove its host link/file only after migrated boot/error presentation is verified.
6. Remove obsolete modules, partials, and temporary adapters only after consumer checks. Do not delete active Quill assets.
7. Inspect the `TheShop.Web.styles.css` host link. Retain it if generated scoped/library styles need it; remove only if proven unused. Do not assume its existence from the template.
8. Run a full Debug build, Release publish, relevant tests, and browser regression on output that no longer loads vendor assets.

Exit: the application is independent of Mud in dependency graph, source, styles, browser assets, and tests.

### P7: Final evidence and handoff

1. Complete the acceptance matrix and report exact pass/fail/skip counts.
2. Compare representative screenshots and interaction behavior; document any intentional native-control differences.
3. Check console errors, failed requests, missing fonts/assets, focus, mobile layouts, and actual deployed-path/deep-link behavior using local release output where feasible.
4. Update Graphify after source changes if available, as required by this repository; inspect resulting graph-file changes separately from application edits.
5. Record final remaining limitations, instructions for reproduction, and rollback boundary.

Exit: every required check has evidence or a clearly declared unresolved blocker. A build alone is not completion.

## 12. Verification commands and environment constraints

Commands below are starting points for the inspected repository. Run from the root with PowerShell 7 where scripts require it. Inspect live project/configuration before running. Capture exit codes and test results. Do not run all environments blindly as a single command chain.

### Build and ordinary tests

```powershell
dotnet build TheShop.slnx --nologo

dotnet test tests/TheShop.Domain.Tests/TheShop.Domain.Tests.csproj --nologo
dotnet test tests/TheShop.Application.Tests/TheShop.Application.Tests.csproj --nologo
dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --nologo

dotnet publish src/TheShop.Web/TheShop.Web.csproj -c Release --nologo
```

During development, run focused Web test classes for the component being changed, then the full Web suite before contraction. Run Infrastructure tests when affected or for the final solution-wide check once their Docker/database prerequisites are available. Preserve unrelated baseline failures explicitly rather than fixing unrelated systems under this task.

Useful focused command example:

```powershell
dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --filter 'FullyQualifiedName~ShopMoneyFieldTests' --nologo
```

After the targeted rule updates, run the design linter directly; no SDD stage invocation is needed:

```powershell
pwsh -NoProfile -File .sdd/scripts/check-design-rules.ps1 -Path src/TheShop.Web
```

If the rule script itself changed, inspect and run its relevant existing tests. Do not claim unexecuted policy tests passed.

### Browser/E2E prerequisites

Inspect:

- `tests/TheShop.E2E.Tests/Fixtures/E2EEnvironment.cs`
- `tests/TheShop.E2E.Tests/Fixtures/PlaywrightFixture.cs`
- `tests/TheShop.E2E.Tests/Fixtures/AppHostFixture.cs`
- `tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1`
- `tests/TheShop.E2E.Tests/Fixtures/ShopBrowser.cs` and authentication fixtures as needed.

At the inspected baseline:

- The app URL is `http://localhost:5218`.
- Fixtures skip when `tests/TheShop.E2E.Tests/.e2e-env` is missing.
- `E2EEnvironment.Headless` is a constant set to `false`; do not assume the commented `E2E_HEADED` suggestion controls it.
- The app fixture launches the dev server itself. Avoid launching a conflicting second server on the same port.
- Authentication storage-state files are updated as refresh tokens rotate. Do not replace their lifecycle during a UI migration.

**Destructive local setup warning:** `start-e2e-env.ps1` runs `supabase db reset`. It recreates the local database from migrations/seed. Do not run it automatically against a local environment that may contain valued data. Confirm the exact disposable test target and obtain authorization for that reset if not already provided. Production or shared-database resets are outside scope. The script's existing "idempotent" comment does not make it non-destructive.

Use an already provisioned disposable environment when possible. Never fabricate `.e2e-env` just to satisfy the existence check. Do not print or commit credentials, browser storage state, or authentication tokens.

After the environment and Playwright browser are available:

```powershell
dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --filter 'Suite=Smoke' --nologo
dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --nologo
```

If the browser is missing, first build the E2E project and inspect its generated `playwright.ps1` before invoking the browser installation command. Browser downloads and local backend setup may require environment-specific approval. Report unavailable prerequisites accurately and continue independent implementation/checks where possible.

An E2E process exiting successfully with all relevant tests skipped is **not** E2E evidence.

### Static dependency audit

```powershell
dotnet list src/TheShop.Web/TheShop.Web.csproj package --include-transitive

rg -n 'MudBlazor|MudExtensions|CodeBeam|AddMudServices|AddMudExtensions' src tests -g '*.cs' -g '*.razor' -g '*.csproj' -g '*.html' -g '*.js' -g '*.scss'
rg -n '<Mud|@inherits Mud|--mud-|\.mud-' src/TheShop.Web tests/TheShop.Web.Tests tests/TheShop.E2E.Tests -g '*.razor' -g '*.cs' -g '*.scss' -g '*.js' -g '*.html'
rg -n 'mud-|\b(pa|px|py|pt|pb|pl|pr|ma|mx|my|mt|mb|ml|mr)-[0-9]+\b|\bd-flex\b|\balign-center\b|\bjustify-' src/TheShop.Web -g '*.razor' -g '*.cs' -g '*.scss'
```

These are audit searches, not automatic proof. Exit code 1 from `rg` means no matches; exit code 2 means an error. Inspect generic utility matches: retain only classes actually defined by the project or intentional third-party assets. Also search specific removed enums/types discovered in P0; a bare `Color`, `Size`, `Variant`, `ObjectFit`, or `Breakpoint` may not contain the string "Mud".

Historical documentation and this guide may mention Mud. The zero-dependency requirement applies to live code/configuration/assets/tests and resolved packages, not erasing history. Canonical current instructions must no longer require Mud.

Inspect Release browser network requests to prove no `_content/MudBlazor` or extension asset is requested. Confirm all new CSS variables resolve after vendor styles are gone. Old build artifacts are not proof of live dependency; compare against freshly generated output in a controlled output location.

## 13. Acceptance matrix

Use actual test names and evidence links in the execution record. Add rows for any live feature discovered beyond this baseline.

| Area | Required proof |
|---|---|
| Boot and shell | Home/deep link loads; one main landmark; heading focus after navigation; font/assets load; no vendor requests |
| Responsive layout | Representative narrow/mobile, intermediate, and desktop widths; no unintended page overflow; usable zoom/reflow |
| Buttons/links | Correct native semantics; Enter/Space where applicable; disabled and busy; submit once; navigation links preserved |
| Forms | Labels, hints, field/form errors, invalid input retention, null values, dirty state, reset, cancellation |
| Authentication | Sign-in/sign-up, invalid email/date/age confirmation, OTP leading zero/paste/navigation, resend, return URL, sign-out |
| Authorization | Anonymous/customer/admin visibility; existing permission gates remain; denied paths remain denied |
| Dialogs | Confirm/cancel/Escape; backdrop policy; initial/restored focus; navigation/disposal; concurrent request behavior |
| Notifications | Correct messages/kinds; live-region announcements; dismissal/timer cleanup; route transition behavior |
| Busy state | Success/failure/cancellation all release busy state; no duplicate submissions; overlay scope correct |
| Catalogue | Filters, both range ends, sorting, pagination, empty/error/loading, deep links, Back/Forward, stale-load cancellation |
| Breadcrumbs | Current page, intermediate links, responsive treatment, resource-backed labels |
| Admin listings | Row/bulk selection, select-all scope, sorting, paging, partial outcomes, blocked deletion, narrow layout |
| Brand/category forms | Create/edit, validation, logo behavior, success/error notifications, navigation |
| Product editing | Create/edit, draft/publish, variants/prices, specifications, gallery order, image pins/fan-out, errors/conflicts |
| Images | Every existing preset, ratios, contain/cover, mobile source, alt/decorative behavior, missing/broken/changing source |
| Uploads | Type/size/count failures, single/multiple selection, repeated file, reset timing, URL cleanup, stored images |
| Rich text | Supported formatting, paste behavior, persisted content, validation/sanitization, labels, disposal |
| Product details/gallery | Only if present: options/selection, images, disclosures, route/loading/errors, existing actions |
| CSS/token independence | No undefined project tokens; no reliance on Mud reset/utilities; boot/error UI retains styling |
| Dependency removal | Direct/transitive packages, imports, providers, scripts/styles, enums, CSS, JS selectors, test setup audited |
| Governance | Current checks enforce new rules without weakening unrelated checks; hooks remain enabled |

Native date/select popups vary by platform. Document intentional appearance differences; the surrounding field design, data semantics, and usability still require validation. Do not excuse arbitrary page regressions as "native behavior".

## 14. Test migration principles

1. Preserve business assertions. Changing `FindComponent<MudSelect<...>>()` to a native input query is appropriate; deleting validation/selection assertions is not.
2. Prefer accessible role/name and stable existing `data-testid` values. Use project-owned IDs only where semantic queries are insufficient.
3. Replace `.mud-layout`, `.mud-snackbar`, `.mud-dialog`, and similar E2E selectors. Inspect shared page objects first because one fix can update many journeys.
4. Drive field input/change and submit events through rendered controls. Remove reflection that forces `_isFormValid` only after the replacement form can be exercised properly.
5. bUnit verifies rendered contracts, callbacks, state, validation, and service outcomes. It does not prove CSS geometry, native modal behavior, or actual browser focus.
6. Browser tests verify keyboard/focus, layout, uploads, dialog lifecycle, and interactions requiring JavaScript.
7. Do not weaken strict JS mocks globally just to silence missing calls. Adjust mocks to the new module contract and retain browser verification.
8. Use deterministic data and viewport sizes for before/after visual comparisons. Wait for fonts and expected content. Do not overwrite baselines without inspection.
9. Record test totals and skipped cases. A green command with no executed tests is insufficient.
10. Test lifecycle failure cases for new dialog/notification code, not only its happy path.

Suggested new focused tests, only where corresponding behavior is implemented:

```text
tests/TheShop.Web.Tests/
  Components/Common/ShopButtonTests.cs
  Components/Common/ShopMoneyFieldTests.cs             [migrate existing]
  Components/Common/ShopConfirmDialogTests.cs          [migrate existing]
  Components/Common/ShopNotificationHostTests.cs
  Components/Products/ProductFormTests.cs              [migrate existing]
  Common/Dialogs/ShopDialogServiceTests.cs
  Common/Notifications/ShopNotificationServiceTests.cs
  State/BreadcrumbStateTests.cs                        [migrate existing]

tests/TheShop.E2E.Tests/
  Journeys/                                           [extend existing journeys]
  Pages/                                              [replace Mud selectors]
```

## 15. Failure handling, rollback, and coexistence

No database migration is required for this UI replacement. Do not add data conversion or alter persisted structures. Preserve existing local-storage keys and authentication/session formats so older and newer UI bundles remain compatible with the same backend.

During development:

- Keep old packages/providers until their last consumer is migrated.
- Migrate a component and its direct consumers/tests as one coherent batch.
- Do not leave a custom control silently depending on legacy vendor CSS.
- Track temporary aliases/adapters with explicit consumers and removal phase.
- Re-running a phase starts with inspecting what already exists; do not duplicate services, providers, registration, CSS imports, or resource keys.
- Keep unrelated user edits outside migration changes.

Rollback is to the last verified application revision or a reviewed inverse of the current migration batch. Use source control checkpoints according to the user's commit authorization. Never use `git reset --hard`, broad checkout restoration, or recursive deletion to discard user work. Do not automate reverts of shared history without authorization.

Keep the previously verified app revision usable until final verification. Because this migration changes no backend contract, an authorized later deployment can retain the previous complete application artifact for rollback. Do not mix old HTML with new hashed assets or remove old hosting artifacts prematurely; deployment itself is outside this handoff.

If a phase fails, identify the failed contract, correct it, and rerun its focused checks before broader verification. Do not continue deleting compatibility code while its replacement is unverified.

If an environment prerequisite is unavailable, finish independent work and record the exact missing verification. If a design choice materially changes behavior beyond this guide, present the concrete choice to the user. Do not ask for routine permission to use native markup; that destination is already selected.

## 16. Known traps to check before declaring completion

- `Services/**` is excluded in the current Web project file.
- Base inheritance may be declared in Razor, C#, or both.
- Mud typography comments can disagree with actual theme construction; inspect actual values.
- Both `MainLayout` and `AuthLayout` currently mount Mud providers.
- Removing Mud CSS removes utility classes and document defaults, not only component skinning.
- `ShopFieldLabel` needs a real label association in the new contract.
- Boolean age confirmation must be `true`, not merely non-null.
- Native buttons default to form submission unless an explicit type is set.
- Standard hyperlinks and action buttons have different semantics; preserve them.
- `InputBase<TValue>` inputs need correct field binding and expression propagation.
- Money display formatting cannot be blindly written into a numeric input value.
- OTP values must retain leading zeros and paste/focus behavior.
- A two-thumb range slider cannot be replaced with a single input without changing behavior.
- Native `<dialog open>` is not the same as calling `showModal()`.
- A dismissed/unmounted dialog must resolve pending callers; cancellation is not a successful empty selection.
- File-input reset can invalidate references to selected files; read first, reset afterward.
- Blob preview URLs require cleanup; revoking stored remote URLs is not equivalent.
- E2E shared page objects and auth boot helpers contain `.mud-*` selectors.
- Current E2E fixtures skip without `.e2e-env`; a skipped run is not proof.
- Current E2E setup resets the local Supabase database.
- Generated CSS must be tested without the old vendor files, including Release output.
- Existing resource generation means `Strings.Designer.cs` is not manually authored.
- Rich-text content and SVG registry markup have different trust boundaries from arbitrary user HTML.
- Product-details/gallery files may not exist in this branch; their absence is not a task to build them.
- Existing `CartState` or navigation placeholders do not authorize building unfinished features.

## 17. Final definition of done

The migration is complete only when all applicable conditions hold:

- [ ] Every existing routed screen and conditional UI branch uses the accepted native UI architecture.
- [ ] Both Mud packages are absent from direct and transitive application dependencies.
- [ ] No live Mud imports, types, providers, service registration, CSS variables, selectors, utility reliance, or JS asset references remain.
- [ ] Tests no longer require Mud setup or its internal component/DOM contracts.
- [ ] `ShopColors`, `ShopTypography`, and `ShopTheme` no longer duplicate active SCSS ownership; `ShopIcons` remains available.
- [ ] All project visual values have defined SCSS ownership and all used `--shop-*` variables resolve.
- [ ] New and migrated project-owned component classes use component-prefixed kebab-case, without BEM double underscores or double hyphens; CSS custom properties retain the `--shop-*` prefix.
- [ ] Generic controls remain independent of feature use cases; backend and authorization contracts are preserved.
- [ ] Existing loading, query-state, routes, resources, and state-store behavior are retained.
- [ ] Form, dialog, upload, OTP, range, image, editor, and table contracts pass their applicable checks.
- [ ] Debug build and Release publish pass, with new warnings understood and addressed.
- [ ] Relevant unit/component tests execute and pass; baseline failures are clearly separated.
- [ ] Relevant E2E/browser checks actually execute and pass; required unavailable checks remain explicit blockers to a completion claim.
- [ ] Representative visual comparisons, keyboard/focus, zoom/mobile, console, and network checks are reviewed.
- [ ] Canonical current design instructions and checks match native UI; unrelated checks remain active.
- [ ] Temporary migration bridges and obsolete files are removed with consumers verified.
- [ ] Source changes, verification evidence, limitations, and rollback reference are recorded.
- [ ] No production operation, destructive data reset, publication, or unrelated refactor occurred without authorization.

## 18. Execution record and resume protocol

Use this section as the durable progress record, or link a concise companion record from here if it grows. Local evidence may live under `.migration/ui/`; review `.gitignore` and repository conventions before creating it. Never store secrets or authenticated browser state in committed evidence.

Current status: **Implementation in progress; first native foundation and slice verified.** This is not a completed dependency removal. Both Mud packages and providers remain required by unconverted screens.

| Phase | Status | Changed scope/checkpoint | Evidence or blocker |
|---|---|---|---|
| P0 Baseline | Complete for build/Web tests | Branch `refactor/ui-refactoring`, starting commit `dd747d9df0137842be5077bd7f2b90f99e6b92a6` | Solution build succeeded; baseline Web tests 845 passed, 0 failed, 0 skipped. Existing package advisories recorded below. |
| P1 Foundation | In progress | Scoped tokens/base SCSS, ShopComponentBase, ShopCssClass, ShopButton, ShopIconButton, ShopIcon, immediate ShopTextInput; canonical native guidance | Implemented subset passes build, component tests, and focused browser checks. Remaining services/controls are not implied complete. |
| P2 First slice | In progress | ProductCard, ShopImage, SignIn markup/form, native confirmations and variant-image picker | Existing callbacks, image behavior, validation, dialog results, and BusyState preserved and tested. SignIn still uses the snackbar bridge; this is not a fully vendor-free route yet. |
| P3 Shell/services | In progress | Native dialogs and notification service/host; dialog/snackbar providers removed | Next: native loading overlay and shell presentation. Other Mud providers still have consumers. |
| P4 Auth/controls | Not started | | |
| P5 Feature screens | Not started | | |
| P6 Remove dependencies | Not started | | |
| P7 Final verification | Not started | | |

### Batch 1 — 2026-10-02

Branch: `refactor/ui-refactoring`. Starting revision: `dd747d9df0137842be5077bd7f2b90f99e6b92a6`. No commit, push, deployment, database migration, or database reset was performed. The pre-existing guide and native-rule changes were preserved and extended. This is a verified working-tree checkpoint, not a new committed rollback revision.

Implemented scope:

- `Common/UI/`: visual base, class composition, button variant and control-size enums.
- `Components/Common/ShopButton*`, `ShopIcon*`, `ShopTextInput*`: native primitives. Inputs use `InputBase<string?>` and `oninput`; buttons enforce their actual type/disabled behavior; registry icons are decorative and non-focusable.
- `ShopImage*`: native image/placeholder markup, existing presets, responsive sources, callbacks, and failure recovery retained. The existing image observer remains intentionally for failures before registration, not as a Mud dependency.
- `Components/Products/ProductCard*`: native article and independent action buttons. Content selection keeps the callback contract and gains keyboard-operable button semantics; no product-details route was invented.
- `Pages/Auth/SignIn*`: native form, associated label and error messages, immediate validation, busy indicator, duplicate-submit protection, trimmed command input, and preserved return URL. Existing email validation semantics remain unchanged.
- `Styles/tokens/`, `base/`, `layouts/_auth.scss`, and component partials: owned tokens, scoped defaults, Figma-derived button/field geometry, responsive layout and explicit focus/error states. Existing Mud styles remain for their consumers. `_pagination.scss` now uses `@use` instead of deprecated `@import`.
- Component/page tests and existing image page-object selectors migrated without removing behavior assertions. New `NativeUiJourneyTests` verifies real browser input, focus, geometry and styling. `E2ETestBase` sanitizes failed-theory trace filenames for Windows.
- Conflicting canonical design rules/examples/checklists updated under `.sdd/`; no SDD stage flow or feature artifacts were required. Unrelated architecture, localization and authorization checks remain active.

Verification (all listed runs exited 0):

| Check | Result |
|---|---|
| `dotnet build TheShop.slnx --no-restore --nologo -m:1 -p:UseSharedCompilation=false` | Passed; final incremental build: 0 errors, 3 existing package-advisory warnings. |
| `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj` with no-restore/single-node flags | 876 passed, 0 failed, 0 skipped; baseline was 845. |
| Domain tests | 358 passed, 0 failed, 0 skipped. |
| Application tests | 599 passed, 0 failed, 0 skipped. |
| E2E filter below | 9 passed, 0 failed, 0 skipped: 3 native UI cases and 6 existing image composition cases. |
| Additional `Feature=native-ui` browser rerun after adding computed error-color assertions | 3 passed, 0 failed, 0 skipped; these are the same native cases, not 3 additional unique tests. |
| `pwsh -NoProfile -File .sdd/scripts/check-design-rules.ps1 -Changed` | Clean. |
| `git diff --check` | Passed; only line-ending conversion notices. |
| `graphify update .` | Passed; code graph refreshed. SQL extraction remains unavailable without `tree_sitter_sql`; some config files produce no nodes, and community labels use hub-name fallbacks. No extra dependency or LLM labeling run was introduced. |

Reproduce focused browser verification without submitting an email:

```powershell
dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --no-restore --nologo --filter 'Feature=native-ui|FullyQualifiedName~ShopImageJourneyTests.AC3_|FullyQualifiedName~ShopImageJourneyTests.AC4_|FullyQualifiedName~ShopImageJourneyTests.AC5_|FullyQualifiedName~ShopImageJourneyTests.AC6_|FullyQualifiedName~ShopImageJourneyTests.AC7_|FullyQualifiedName~ShopImageJourneyTests.AC9_' --blame-hang-timeout 3m -m:1 -p:UseSharedCompilation=false
```

Browser evidence: `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/` contains `buttons-no-vendor-css.png` and `signin-{390,1440}-{empty,valid,invalid}.png`. These are local generated evidence, not committed baselines; rerun the tests to reproduce them. Screenshots were visually inspected. Button tests disable Mud/CodeBeam and legacy `app.css`, retain the actual generated `TheShop.css`, and measure all nine variants against the inspected Figma dimensions. Sign-in checks retain the current mixed-layout styles, cover two viewports, typing without blur, label focus, Tab focus, readable error-token color and no horizontal overflow. Browser physical-pixel rounding is allowed within one physical pixel for the authored 2px focus outline. The tests block configured backend traffic and assert zero requests; they never submit a valid email.

Intentional differences: explicit labels and semantic headings/buttons; visible keyboard focus; darker small error text for contrast. Interaction-state colors not provided in Figma are documented inferred tokens, not claimed design matches. Whole-page pixel parity is not claimed because the supplied reference is the components section, not a verified page design. Remaining ambiguous Figma icon-button sizes are not silently generalized.

Remaining compatibility and verification:

- `ISnackbar` in SignIn and Mud providers in the layouts are explicit temporary bridges. Legacy themes, packages, utilities and unconverted screens/controls remain. Do not remove either package yet.
- Local Supabase at `127.0.0.1:54321` was unavailable. Backend-dependent full auth/admin journeys and Infrastructure integration tests were not verified. No service was started or data reset implicitly.
- Release publish, complete visual coverage, assistive-technology review and final dependency-removal checks remain for later phases. This batch does not satisfy the final definition of done.
- Existing package advisories remain: `SSH.NET` 2025.1.0 (high, Infrastructure test dependency) and `AngleSharp` 1.4.0 (moderate, Web test dependency). Existing E2E cancellation-token analyzer warnings also remain. No unrelated package upgrade was included.

Next concrete action: implement and verify the project-owned dialog/notification services and hosts, then migrate the remaining auth controls/screens and layouts in dependency order. Inspect this checkpoint before editing; reuse existing primitives instead of recreating them. Preserve the snackbar bridge until its replacement and callers are verified.

### Batch 2 — Figma color-token alignment — 2026-10-02

Historical record: the compatibility aliases below were subsequently removed in batch 3. Follow section 5.1 for the current direct-token contract.

Scope: `Styles/tokens/_colors.scss` and this guide only; prior migration/user changes preserved. Figma was read, not edited. All 22 named paint styles were re-read after the owner's surface renames. The agreed names omit `brand-`/`semantic-`, retain `text-`/`surface-`/`lines-`, and expose distinct roles even when their values match. See section 5.1 for the full mapping.

Compatibility: seven existing application names now alias Figma-backed tokens. All 14 previous color-token values resolve identically to the pre-edit snapshot, including normalization of `#fff` to `#ffffff`. No component selectors, layout, C# palette, inferred interaction/error-text values, or Mud consumers changed. The migration skill's expand-and-verify approach was used; alias contraction was not performed.

Verification:

- Live Figma/source comparison: 22 of 22 named styles matched their mapped tokens and 8-bit hex values.
- Alias resolution: all seven aliases resolve; no missing target or cycle. All 14 pre-existing tokens preserve their resolved colors.
- `dotnet build src/TheShop.Web/TheShop.Web.csproj --no-restore --nologo -m:1 -p:UseSharedCompilation=false`: passed, 0 warnings, 0 errors; Sass compiled through the existing build integration.
- Generated `wwwroot/css/TheShop.css`: all 29 source color declarations matched (22 styles plus seven aliases); generated CSS was not hand-edited.
- `git diff --check`: passed, with existing line-ending notices only.
- No new browser run or full test-suite run for this value-preserving token change; batch 1 results remain historical evidence, not a new test run. No new whole-page visual-parity claim.

Rollback: review an inverse of this two-file batch only, restoring the previous 14-token definitions and guide section if necessary; do not reset the shared worktree. Future component work must use the agreed mapping rather than introduce another palette. Remaining migration phases and backend-verification limitations are unchanged.

### Batch 3 — Remove application color aliases — 2026-10-02

Historical record: the button-color component overrides retained here were subsequently removed in batch 4. State/accessibility tokens remain unchanged.

Completed the owner's approved alias contraction. `_colors.scss` now contains only the 22 Figma-backed colors. Replaced all seven former alias names in button, field, image, product-card, auth-layout, and theme-state SCSS with their direct Figma-backed targets. No Razor/C# behavior or palette values changed. Component overrides (`--shop-button-*`), interaction tokens (`--shop-color-action-hover`, `--shop-color-action-pressed`, `--shop-color-action-disabled`), and accessibility colors remain supported.

Updated section 5.1 and canonical theme/style/component examples to avoid recommending removed names. The historical replacement table intentionally retains the old names for future audits; it is not an instruction to restore them.

Verification:

- Captured the generated CSS before editing. After recompilation, the stylesheet is identical after whitespace normalization and exactly the intended transformation: delete the seven alias declarations and replace their exact references. All other declarations/selectors are preserved, including component overrides and state rules. This is the safe-refactor preservation check, not a fresh browser visual-parity claim.
- `dotnet build src/TheShop.Web/TheShop.Web.csproj --no-restore --nologo -m:1 -p:UseSharedCompilation=false`: passed, 0 warnings, 0 errors.
- Exact-name search across `src`, `tests`, and canonical constitution guidance found no remaining active references to the deleted aliases. Distinct suffixed state tokens were deliberately excluded from the removal pattern.
- `git diff --check`: passed, with line-ending notices only. Browser and full test suites were not rerun for this CSS-equivalent refactor.

Rollback: restore the seven alias declarations and reverse only this batch's consumer/documentation substitutions if needed. Preserve unrelated and staged work; no commit, reset, or deployment performed. Remaining migration phases are unchanged.

### Batch 4 — Button/icon styling ownership — 2026-10-02

Completed the owner's product-card styling correction and stylesheet split:

- `_button.scss`: shared button foundation, text-button variants/sizes, `.shop-button-icon` adornment sizing, and shared disabled/forced-colors treatment.
- `_icon-button.scss`: existing 36×36 medium icon-only geometry and surface-filled treatment, using direct Figma-backed color variables. It composes the existing button foundation rather than copying it.
- `_icon.scss`: standalone SVG display, size, shrink, and alignment defaults.
- `TheShop.scss`: imports icon, button, then icon-button once each so generic SVG defaults do not override button adornments.
- `_producttile.scss`: cart/wishlist actions retain only positioning; their shared colors, padding, and dimensions moved to the icon-button owner. The card-specific content-selection button layout remains here. ProductCard explicitly applies the shared `shop-button-surface` and `shop-button-icon-only` classes.
- Removed the four unused button-color override properties and their fallback chains. Internal button-size properties and all `_theme.scss` action/focus/error states remain. No new Razor wrapper or dependency was introduced.

Verification:

- All 877 Web tests passed, 0 failed/skipped, including a new ProductCard assertion that both actions use the shared treatment classes.
- The nine backend-independent browser cases passed, 0 failed/skipped. The existing native button test now also verifies icon-only 36×36 frames, 24×24 icons, 6px padding, enabled/hover/pressed/disabled colors, and independence from product-card classes and vendor CSS. The other cases cover sign-in at two viewports and six image-composition scenarios. No OTP request or database reset was performed.
- The SCSS split preserves all 244 compiled top-level CSS rule blocks when compared to the immediately preceding button-extraction working state, ignoring whitespace/comments and block ordering. Browser tests verify affected cascade interactions after reordering; rule-set equality alone is not treated as appearance proof.
- `pwsh -NoProfile -File .sdd/scripts/check-design-rules.ps1 -Changed`: clean. `git diff --check`: passed. Existing AngleSharp advisory and unrelated E2E cancellation-token analyzer warnings remain.
- Screenshot `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/surface-icon-buttons-no-vendor-css.png` was visually inspected. Existing screenshot paths remain reproducible through the same focused E2E command in batch 1.

The earlier requested rerun also passed all 358 Domain and 599 Application tests before this Web-only correction. Full backend E2E and container-backed Infrastructure coverage remain unavailable while Docker/Supabase are offline; no approval for a local database reset was received. These isolated runs are not a full backend-suite pass.

Next: resume native confirmation dialog and notification work. It was inspected but not implemented before the owner's styling corrections. Preserve all staged/user work; no commit or deployment performed. Rollback of this batch requires reversing the shared class/consumer changes together, not deleting the new partials while their imports remain.

### Batch 5 — Figma typography tokens — 2026-10-02

Aligned all 13 local Figma text styles in `tokens/_typography.scss`; removed the old application typography aliases after migrating all native consumers. Added the non-emitting `abstracts/_text-style.scss` mixin. Updated native base, button, field, image, product-card, and auth typography, plus canonical styling examples. See section 5.1 for the full mapping and explicit component exceptions. Figma was read only; no design styles were changed.

Intentional visual corrections include AUTO/normal line heights, product title 18px → 20px, current price weight 700 → 500, and Button TITLE case. Original-price strike-through, auth uppercase treatment, visible-label emphasis, resource copy, input validation, keyboard behavior, button/icon geometry, and all state/color tokens remain. Existing Mud typography stays untouched for unmigrated consumers. Prior staged/user changes were preserved; no SDD stage artifacts, commit, deployment, or database operation was performed.

Verification:

- `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --no-restore --nologo --blame-hang-timeout 3m -m:1 -p:UseSharedCompilation=false`: exit 0; **877 passed, 0 failed/skipped**. This also compiled the Web project and SCSS successfully.
- Focused backend-independent E2E command from batch 1 (native-ui plus image AC3/4/5/6/7/9): exit 0; **10 passed, 0 failed/skipped**. The new typography case checks all 13 styles' family/size/weight/tracking/case, AUTO line-box heights within 1px, native consumer selectors, original-price decoration, and 200% root-font scaling, with vendor styles disabled. Existing button geometry and sign-in/image regressions also pass.
- Visually inspected `native-ui-evidence/typography-no-vendor-css.png`, `signin-390-invalid.png`, and `signin-1440-empty.png` under the E2E `bin/Debug/net10.0` directory. These provide token specimens and affected-screen checks, not a claim of whole-application Figma parity.
- Design gate and `git diff --check`: clean. Old typography-variable consumer search: no matches in source/tests/canonical rules. Existing AngleSharp advisory and unrelated E2E cancellation-token warnings remain; no dependency upgrades included.
- Full backend E2E and container-backed Infrastructure tests were not rerun for this styling batch. Earlier infrastructure limitations remain; isolated browser coverage is not a full backend-suite pass.

Rollback: reverse only this batch's token map, mixin, native consumer, test, and documentation changes together. Restoring declarations alone would leave the new consumers unresolved. Preserve staged work; do not use a broad reset. Next migration scope remains native dialogs/notifications, not further token aliases or global heading resets.

### Batch 6 — Native confirmation dialogs — 2026-10-03

Scope: the next bounded dialog slice, independent of SDD stages. Migrated all confirmation callers: brand/category/product single and bulk deactivate/delete, product-form unsaved internal navigation, and configured option removal. Notification services and the variant-image selection dialog remain pending. Existing staged and unstaged migration work was preserved.

Implementation:

- Added `Common/Dialogs/{IShopDialogService,ShopDialogService,ShopDialogRequest,ShopConfirmationOptions}.cs`, `ShopDialogHost`, and the layout-owned `ShopUiHost`. Queueing, identity-based completion, cancellation, and safe host handoff are covered by tests.
- Converted `ShopDialog` and `ShopConfirmDialog` to native HTML plus existing ShopButton/ShopIcon primitives. Added the small `wwwroot/js/shopDialog.js` ES module. Strings remain caller-localized and HTML-encoded; no resource content or backend contracts changed.
- Added owned native dialog SCSS, a shared destructive button treatment, and a documented modal-backdrop token. Existing color/typography/state tokens remain otherwise unchanged. Native `<dialog>` controls modality; no custom portal/focus-trap framework or dependency was added.
- Isolated the old dialog chrome as `LegacyShopDialog` for `VariantImageDialog` alone. Its previous styling remains under `.shop-legacy-dialog`. Mud dialog/snackbar providers remain because they still have consumers.
- Migrated shared confirmation page-object selectors to `dialog.shop-dialog`. Updated existing page mocks to the new service contract; assertions still cover confirm/cancel mutation dispatch and preservation of permission gates.

Verification:

- `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --no-restore --nologo --blame-hang-timeout 1m -m:1 -p:UseSharedCompilation=false`: exit 0; **891 passed, 0 failed/skipped**. Includes preserved admin mutation/permission tests, native unsaved-navigation and configured-option-removal outcomes, encoded/accessible dialog markup, single completion, queued/cancelled requests, initialization failure, host teardown, and layout handoff.
- Focused E2E command from batch 1 (`Feature=native-ui` plus image AC3/4/5/6/7/9): exit 0; **12 passed, 0 failed/skipped**. New dialog cases at 390×844 and 1440×900 verify native modal state, accessible name, Cancel-first focus, Tab/Shift+Tab, background inertness, Escape/backdrop/close/cancel/confirm, programmatic close, repeated opening, 200% text with long content, removal cleanup, and focus restoration. Browser fixtures render real Razor markup and exercise the production JS module without vendor CSS; C# host/service/consumer behavior is covered separately in bUnit. These checks do not claim a complete authenticated backend journey.
- Screenshots `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/confirmation-390.png` and `confirmation-1440.png` were visually reviewed against the inspected Figma chrome. Geometry comparisons allow 1px device-pixel rounding tolerance. Full Figma parity is not claimed for inferred confirmation states or the larger close-button target.
- `pwsh -NoProfile -File .sdd/scripts/check-design-rules.ps1 -Changed`: exit 0, clean. `git diff --check`: exit 0. No old `ShowAsync<ShopConfirmDialog>`, confirmation `DialogParameters`, or `ConfirmColor` references remain in source/tests. SCSS and Web builds succeeded through the test commands.
- Early verification attempts exposed missing test DI registrations and incorrect bUnit wrapper disposal; fixtures now use `DisposeComponentsAsync()` and await completion with a bounded timeout. Final runs finish normally. Existing AngleSharp advisory and unrelated E2E cancellation-token warnings remain.
- Code graph refreshed with `graphify update .`; existing missing-SQL-parser/empty-file extraction warnings remain. Full backend E2E and container-backed Infrastructure tests were not run; the previously recorded Docker/Supabase limitation remains.

Rollback: reverse this batch's service registrations, layout host wiring, component/JS/SCSS changes, consumer calls, and test changes together. Restore the former ShopDialog/ShopConfirmDialog implementation and the image picker's original wrapper before removing LegacyShopDialog. Never remove a still-referenced provider or use a broad reset. No database reset, backend mutation, commit, or deployment was performed.

Next: native variant-image picker with typed cancellation versus accepted-empty selection, then native notification host/service and snackbar consumer migration. Do not repeat completed confirmation or token work.

### Batch 7 — Shared visual enums and button colors — 2026-10-03

Scope: the owner's approved shared color/variant/size contract, independent of SDD stages. Branch `refactor/ui-refactoring`, starting commit `dd747d9`. Existing migration changes were already staged; this batch preserves that index and adds only unstaged changes. No commit, deployment, database operation, or next migration slice was performed.

Implementation:

- Renamed `Common/UI/ShopButtonVariant.cs` to `ShopVariant.cs` and `ShopControlSize.cs` to `ShopSize.cs`; added `ShopColor.cs`. Removed the old source files after migrating all live consumers and tests together. These shared enums remain Web-only; no external/persisted data contract is affected.
- `ShopButton` now accepts Color independently of Variant and Size, defaults to Primary/Filled/Medium, and rejects undefined enum values. Existing native type, disabled, callback, icon, resource, and attribute-forwarding contracts remain. The base component was not expanded.
- `_button.scss` owns all eight color roles × three variants through token-based Sass recipes. Error Outlined/Text no longer inherit a filled background. `_icon-button.scss` retains only the inspected icon-only geometry; `_icon.scss` remains unchanged. No runtime button-color aliases were reintroduced.
- Product-card overlay buttons now pass `ShopColor.Surface`; destructive confirmation maps to `ShopColor.Error`. Primary, surface, and destructive-filled behavior is preserved. Sign-in and dialog callers use the renamed enums without behavior changes.
- `_colors.scss` and existing action/focus/error tokens were not changed. Five new accessibility-specific values in `_theme.scss` cover secondary/info/success/warning text and a black foreground for secondary-filled resting buttons. Section 6.2 records the contrast rationale and inferred states; these are not claimed Figma measurements.
- Updated the guide and relevant canonical component/style examples. Other components may reuse the enums when they implement a meaningful supported contract; no unused icon/input parameters or generic styling framework was introduced.
- Added an opt-in `E2E_HEADLESS=1` flag to the existing Playwright fixture; its interactive default remains unchanged.

Verification:

- Before editing: focused ShopButton/ShopConfirmDialog/ProductCard tests **51 passed**. After editing: **63 passed**, covering all 72 color/variant/size compositions, activation, undefined enum rejection, parameter rerendering, and preserved consumers.
- `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --no-restore --nologo --blame-hang-timeout 1m -m:1 -p:UseSharedCompilation=false`: exit 0; **903 passed, 0 failed/skipped**. Web and SCSS compilation passed through this run.
- New `NativeButtonColorJourneyTests`: **2 passed, 0 failed/skipped**, at 390×844 and 1440×900. Real rendered ShopButton markup and compiled project CSS are exercised with vendor/template CSS disabled. All 24 enabled color/variant combinations are checked at rest/hover/pressed, including computed token colors and minimum 4.5:1 text contrast on the specimen's light surface. Checks also cover outlined versus text boundaries, disabled interaction/state invariance, visible keyboard focus, forced-colors boundaries, and horizontal overflow. C# event dispatch is tested separately in bUnit.
- Final browser regression command below: exit 0; **14 passed, 0 failed/skipped**. Includes the two color cases, previous measured primary button/icon geometry, typography, sign-in, native dialogs, and six image cases. This total includes the two isolated color cases; do not add them again. Backend requests remain blocked in the native UI cases.
- Visually reviewed `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/button-colors-390.png` and `button-colors-1440.png`. Existing geometry/browser assertions remain intact. Screenshots are reproducible local evidence, not committed image baselines.
- `pwsh -NoProfile -File .sdd/scripts/check-design-rules.ps1 -Changed`: exit 0, clean. `git diff --check`: exit 0. No old enum or `shop-button-danger` consumers remain in source/tests/canonical guidance; historical names in this guide intentionally explain the transition.
- `graphify update .`: exit 0; code graph refreshed. Existing empty-file/missing-SQL-parser warnings and hub-name community-label fallbacks remain. No semantic extraction, dependency installation, or LLM labeling was added.

Browser retry notes: the first attempt encountered a separately running Visual Studio server with stale fingerprinted WASM/PDB asset URLs (404/integrity failures) plus sandbox-blocked fonts. No user process was stopped. A later retry loaded the app successfully; visible-browser hover/pressed assertions then intermittently observed inactive states. Headless runs with retrying CSS assertions passed with unchanged expected colors and contrast thresholds. The final combined regression run also passed. Existing AngleSharp advisory and unrelated E2E cancellation-token analyzer warnings remain.

Reproduce after Web/E2E builds, with the test port free and normal browser/font access:

```powershell
$env:E2E_HEADLESS = '1'
dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --no-build --no-restore --nologo --filter 'Feature=native-ui|FullyQualifiedName~ShopImageJourneyTests.AC3_|FullyQualifiedName~ShopImageJourneyTests.AC4_|FullyQualifiedName~ShopImageJourneyTests.AC5_|FullyQualifiedName~ShopImageJourneyTests.AC6_|FullyQualifiedName~ShopImageJourneyTests.AC7_|FullyQualifiedName~ShopImageJourneyTests.AC9_' --blame-hang-timeout 2m -m:1 -p:UseSharedCompilation=false
```

Limitations: transparent treatments are verified on light surfaces, not arbitrary photographs/dark backgrounds. Full authenticated/backend E2E and container-backed Infrastructure tests were not run. No full-application Figma or accessibility-conformance claim is made.

Rollback: reverse only this batch's enum renames, Color API and consumer updates, Sass recipes/accessibility additions, tests, and documentation together. Restore the old surface/danger recipes with their callers if reverting. Do not restore just the old filenames or reset the worktree/index, because earlier migration work belongs to the owner. The former files remain recoverable in the pre-existing index.

Next: native variant-image picker, then native notification service/host and snackbar consumer migration. Shared visual choices are complete; do not recreate the enums or repeat token alignment.

### Batch 8 — Direct base/contrast label colors — 2026-10-03

Owner clarification: filled controls use their color role's contrast token; outlined/text controls use the base role color. New semantic contrast defaults should all be white for now. This supersedes batch 7's button-specific text overrides and blanket contrast-pass claim, not its shared enum API or geometry.

Implemented:

- Added info-contrast, success-contrast, warning-contrast, and error-contrast (`#ffffff`) to `_colors.scss`. The 22 inspected Figma style values and existing brand contrast values are unchanged. No Figma document was modified.
- Filled button labels now consume the role's `*-contrast` token at rest and during interaction. Outlined/text labels consume the base role token. Surface retains text-primary; disabled colors remain shared. Secondary/status outlined borders follow their base colors; existing neutral borders remain unchanged.
- Removed secondary/info/success/warning `*-text` and secondary-contrast-accessible. Their previous dark interaction backgrounds remain under explicit `*-hover` state tokens. Added error-hover to separate the button state from form-validation error-text; the latter remains unchanged for native fields only. Existing action tokens remain unchanged.
- Updated component documentation and the migration contract. No enum API, handler, geometry, typography, validation, or base-component changes were made. Prior staged work was preserved; this batch is unstaged and reversible independently.

Verification:

- Focused ShopButton/ShopConfirmDialog/ProductCard tests: **63 passed, 0 failed/skipped**; Web and SCSS compilation passed through the run.
- `E2E_HEADLESS=1` with `dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --no-restore --nologo --filter 'Feature=native-ui' --blame-hang-timeout 2m -m:1 -p:UseSharedCompilation=false`: **8 passed, 0 failed/skipped**. Covers the revised token matrix at both viewports plus primary/icon sizing, typography, sign-in validation and native dialogs. Color assertions still check actual compiled CSS, disabled states, keyboard focus and forced colors. Known low-contrast pairs are measured, explicitly classified, and logged as `KNOWN CONTRAST GAP`; unaffected pairs retain the 4.5:1 assertion.
- Reviewed the regenerated mobile color specimen at `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/button-colors-390.png`. The desktop specimen is also regenerated by the suite. Pale tertiary outlined/text labels and bright semantic combinations are intentional consequences of the requested temporary mapping, not overlooked visual regressions.
- Design-rule gate and `git diff --check`: clean. Removed-token source/test scan found no remaining consumers. `graphify update .` succeeded with the existing empty-file/missing-SQL-parser and community-label fallback warnings.
- The full 903-test Web suite, image regression subset, backend-dependent E2E, and Infrastructure tests were not rerun for this focused color change. Earlier passes remain historical. Existing dependency/analyzer advisories are unchanged.

Accessibility limitation: white on secondary/info/success/warning/error resting fills measures approximately **4.29 / 2.80 / 1.32 / 1.63 / 3.44:1**, respectively, below the 4.5:1 small-text threshold. Base-colored outlined/text labels also have documented gaps, including very pale tertiary text. Passing token-contract tests does not mean these combinations pass accessibility requirements. Resolve these palette choices before production accessibility sign-off; do not silently reinstate the removed text overrides.

Rollback: reverse this batch's palette additions, button mappings, theme state names, test expectations, and documentation together. Restore the removed overrides only as part of that explicit rollback. Do not reset the worktree or staged migration changes. Next migration scope remains the native variant-image picker and notifications.

### Batch 9 — Icon-button component, sizes, class helper, and direct validation color — 2026-10-03

Branch: `refactor/ui-refactoring`; starting commit: `dd747d9`. Earlier migration work was already staged. Preserved that work; this batch remains unstaged. No SDD workflow artifacts, backend contracts, dependencies, or Figma document changes.

Implemented:

- Native form error messages now consume `--shop-color-error`, matching invalid borders. Removed `--shop-color-error-text` from `_theme.scss`; error-hover and all action-state tokens remain unchanged. Validation semantics, accessible associations, and submission behavior are preserved. This explicitly supersedes the earlier batch records retaining darker form error text.
- Added `ShopIconButton.razor` and `.razor.cs`, composing `ShopButton` and decorative `ShopIcon` without an extra DOM element. Required Icon/Label, shared Color/Variant/Size, native Type, Disabled, click callback, and actual-button attribute forwarding are covered by component tests. Product-card cart/wishlist actions and dialog close now consume it; callers no longer pass the removed icon-only class.
- Added Small/Medium/Large geometry for Filled/Outlined/Text to `_icon-button.scss`, sourced from Figma set `322:6450` and its nine nodes listed in section 6.2. The duplicate 42px Filled/Medium label is explicitly interpreted as Large; no Figma naming correction is claimed. Filled/Medium product actions stay 36px. Text/Medium dialog close becomes 48px, intentionally increasing header height while preserving focus and dismissal behavior.
- Extended `ShopCssClass` with reusable enum-based `Modifier`, removing the repeated visual-enum switches from ShopButton. Invalid options still throw with useful parameter names. Native type validation stays separate; static styles stay in SCSS. No general-purpose style/builder framework was introduced.
- Updated canonical native component/theme examples and this standalone guide. Historical batch records remain historical; the active contracts above govern new work.

Verification:

- Before extraction: focused ShopButton/ShopConfirmDialog/ProductCard baseline **63 passed**. After all changes: `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --no-restore --nologo --blame-hang-timeout 1m -m:1 -p:UseSharedCompilation=false` — **930 passed, 0 failed/skipped**; Web and SCSS compilation passed.
- Browser regression command: `E2E_HEADLESS=1` and `dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --no-restore --nologo --filter 'Feature=native-ui|FullyQualifiedName~ShopImageJourneyTests.AC3_|FullyQualifiedName~ShopImageJourneyTests.AC4_|FullyQualifiedName~ShopImageJourneyTests.AC5_|FullyQualifiedName~ShopImageJourneyTests.AC6_|FullyQualifiedName~ShopImageJourneyTests.AC7_|FullyQualifiedName~ShopImageJourneyTests.AC9_' --blame-hang-timeout 2m -m:1 -p:UseSharedCompilation=false` — **14 passed, 0 failed/skipped**. The initial run caught an overload error in the new test assertion; corrected before this passing run. Existing unrelated cancellation-token analyzer warnings remain.
- Browser assertions cover all nine icon-button frame/padding/icon measurements with vendor CSS absent, actual accessible names, unchanged surface-button states, revised form error color, dialogs at both viewports, existing typography/color states, and six image regressions. Unit tests additionally verify size changes, invalid enums, required labels/icons, disabled click protection, and attribute precedence. These are backend-independent checks, not a full authenticated commerce journey.
- Visually inspected `icon-button-sizes-no-vendor-css.png`, `confirmation-390.png`, and `signin-390-invalid.png` in `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/`. Icons are unclipped; dialog content/actions fit; form error is visibly the requested brighter red.
- Design-rule check (`pwsh -NoProfile -File .sdd/scripts/check-design-rules.ps1 -Changed`) and `git diff --check` passed. No source/test consumers remain for the removed error-text token or icon-only class.
- `graphify update .` succeeded: 8,679 nodes / 22,093 edges. Existing empty-file, missing SQL-parser, and community-label fallback warnings remain; no extra parser dependency or paid labeling step was added.

Known limitation: requested base-red validation text on white is approximately **3.44:1**, below the small-text AA threshold. Earlier button palette contrast limitations also remain; passing tests do not establish accessibility compliance. No full backend-dependent E2E or Infrastructure suite was run for this Web-only change.

Rollback: reverse this batch's token/field change, component and consumer extraction, size map, class-helper changes, tests, and documentation together; restore the former icon-only class at its call sites if reverting extraction. Preserve earlier staged migration work. Next scope remains the native variant-image picker, followed by notifications; neither is implemented by this batch.

### Batch 10 — Native variant-image picker — 2026-10-03

Branch: `refactor/ui-refactoring`; starting commit: `dd747d9`. Earlier migration work was staged at entry and remains intact in the index. This batch is unstaged. Scope is the picker and its dialog bridge only; notifications, other controls, packages, backend contracts, and SDD workflow artifacts are unchanged.

Implemented:

- Replaced the picker Mud markup with existing `ShopDialog`, `ShopButton`, `ShopIcon`, `ShopImage`, native toggle buttons, and a native radio group. Typed completion explicitly distinguishes cancellation from an accepted null image; saving no image clears the pin, while cancellation discards changes. A per-instance completion guard prevents duplicate dispatch.
- `ProductVariantsCard` now mounts the picker directly with request-identity and disabled/stale-data guards. Existing individual/shared-option fan-out and `StateChanged` payloads remain. Product DTOs stay outside the shared confirmation service; there is no new generic dialog framework or awaitable result left pending on owner disposal.
- Inspected Figma node `2680:14477` through its current screenshot and child geometry. Added `_variant-image.scss` and one resource-backed numbered image label. Uses the measured 120px frames, 16px gallery gaps, 32px body gaps, and shared 500px/24px dialog chrome. Mobile wrapping, native radios, safe Cancel-first focus, 48px shared close button, and retained Circle_Check icon are intentional implementation differences, not exact screenshot parity.
- Updated the native modal module so opening another modal notifies/dismisses the previous owner, including picker-to-unsaved-confirmation transitions. Corrected a selection border hidden beneath image content during screenshot review; its pseudo-element now paints above the image without changing frame size.
- After consumer migration and passing focused checks, removed `LegacyShopDialog.razor`/`.razor.cs`, its SCSS overrides, and both layout `MudDialogProvider` mounts. The staged originals remain recoverable. Mud theme/popover/snackbar providers and registration/packages/assets still serve unmigrated screens and remain intentionally.
- Added picker component and caller tests; updated the existing create-product E2E page object to the native dialog and radios. Added desktop/mobile browser checks using real rendered picker markup, compiled SCSS, and production modal JS without vendor CSS. Updated the phase summary and active dialog architecture in this guide.

Verification:

- Pre-edit focused ProductVariantsCard/ShopConfirmDialog/ProductForm baseline: **44 passed**. After migration and bridge removal, `dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --no-restore --nologo --blame-hang-timeout 1m -m:1 -p:UseSharedCompilation=false`: **942 passed, 0 failed/skipped**. This compiled Web/SCSS and includes 12 new picker/caller cases covering selection, explicit clear versus cancel, scope, duplicate/stale completion, disabled state, removed gallery images, and modal initialization failure.
- During development, checks caught a missing Razor namespace import, a bUnit-only virtualized-row reset in the test helper, and an incorrect JS mock setup API. All were corrected before the passing full suite.
- Final browser command: `E2E_HEADLESS=1` with `dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --no-restore --nologo --filter 'Feature=native-ui|FullyQualifiedName~ShopImageJourneyTests.AC3_|FullyQualifiedName~ShopImageJourneyTests.AC4_|FullyQualifiedName~ShopImageJourneyTests.AC5_|FullyQualifiedName~ShopImageJourneyTests.AC6_|FullyQualifiedName~ShopImageJourneyTests.AC7_|FullyQualifiedName~ShopImageJourneyTests.AC9_' --blame-hang-timeout 2m -m:1 -p:UseSharedCompilation=false`: **16 passed, 0 failed/skipped** after provider removal and the border correction. Covers the two new picker viewports plus existing native UI and six image regressions. Picker assertions cover accessible names/pressed state, frame/gap measurements, the visible border layer, Enter/Space activation, radio arrow keys, Escape/backdrop/close, focus restoration, modal replacement, and 200% text/long-content overflow.
- Reviewed final `variant-image-390.png` and `variant-image-1440.png` in `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/` against the inspected Figma screenshot. Desktop shows three columns, mobile wraps to two; the selected border and indicator remain visible and actions fit.
- Design-rule gate (`pwsh -NoProfile -File .sdd/scripts/check-design-rules.ps1 -Changed`) passed. Exact-symbol scan across source/tests finds no Mud dialog service/instance/result/parameter/provider or legacy dialog wrapper/style consumers. `git diff --check` passed.
- `graphify update .` completed: **8,700 nodes / 22,168 edges**. Existing empty-file, missing SQL parser, and community-label fallback warnings remain; no extra dependency or paid labeling was added.

Verification boundary: browser specimens use intentional image placeholders; actual Blazor state mutations are exercised in bUnit. The authenticated create-product journey's selectors were migrated but that backend-writing journey was not run. No database fixtures, Infrastructure tests, deployment, or release publish were performed. Existing AngleSharp NU1902 advisory and unrelated E2E cancellation-token analyzer warnings remain. Previously recorded palette contrast gaps are unchanged.

Rollback: reverse the picker/caller, resource, stylesheet, module, tests, and guide changes together; restore both legacy wrapper files, their styles, and the two provider mounts from the prior staged checkpoint. Do not reset the worktree or disturb earlier staged migration work. Next batch: native notification service/host and `ISnackbar` consumer migration; do not repeat token, button, image, or dialog work.

### Batch 11 — Dialog width choices and named content regions — 2026-10-03

Scope: owner-approved `ShopMaxWidth` plus `TitleContent` / `DialogContent` / `DialogActions`; no further Mud migration or backend changes. Branch remains `refactor/ui-refactoring`, starting revision `dd747d9`; prior staged changes were preserved and this batch was not committed.

- Added the shared Web-only enum and `_sizing.scss` scale. Width modifiers reuse `ShopCssClass`; `_theme.scss` owns the unchanged 500px default. `None` retains viewport gutters. No replacement UI dependency, enum metadata, or duplicate C# measurements.
- Replaced `ShopDialog.Title`, `ChildContent`, and `Actions` with the three explicit fragments; migrated confirmation and variant-image consumers together. A stable unique title wrapper supplies accessible naming. Callers own heading markup; titles remain encoded/localized.
- Open-only flex layout keeps header/actions outside the scrollable center. Body is a named keyboard-focusable region, adding a deliberate tab stop. Short dialogs remain content-sized; tall bodies are viewport-constrained. Existing JS lifecycle, service queue, focus restoration, dismissal, picker selection, and typed completion were unchanged.
- Added nine bUnit cases for width mapping/reset, invalid values, slot ownership, naming, and IDs. Extended native browser checks with all eight width configurations at 390/1440/3000px and body-only scrolling at 200% text size on mobile/desktop.

Verification:

- Before edits: focused confirmation/picker tests **15 passed**. After edits: full Web suite **951 passed, 0 failed, 0 skipped** (`dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --no-restore --nologo -m:1 -p:UseSharedCompilation=false`).
- Design-rule gate clean; targeted `dotnet format whitespace --verify-no-changes` passed; `git diff --check` passed. Source/test scan found no calls to the removed dialog parameters.
- `graphify update .` completed: **8,717 nodes / 22,197 edges**. Existing empty-file, missing SQL parser, and community fallback warnings remain; no additional dependency installed.
- Final browser retry: **19 passed, 0 failed, 0 skipped**. Ran `dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --no-build --no-restore --nologo` with `E2E_HEADLESS=1`, filter `Feature=native-ui|FullyQualifiedName~ShopImageJourneyTests.AC3_|FullyQualifiedName~ShopImageJourneyTests.AC4_|FullyQualifiedName~ShopImageJourneyTests.AC5_|FullyQualifiedName~ShopImageJourneyTests.AC6_|FullyQualifiedName~ShopImageJourneyTests.AC7_|FullyQualifiedName~ShopImageJourneyTests.AC9_`, `--blame-hang-timeout 2m`, and TRX logger `dialog-batch11.trx`. Results: `tests/TheShop.E2E.Tests/TestResults/dialog-batch11.trx`.
- Reviewed `confirmation-scroll-390.png` and `confirmation-scroll-1440.png` under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/`: title/actions remain visible at 200% text size while the middle scrolls. Browser assertions cover all width caps, viewport gutters, hidden closed dialogs, content-sized short dialogs, stationary header/footer, and existing modal focus/dismissal.
- Initial runs encountered stale development-server asset manifests: fingerprinted Web WASM/PDB requests returned 404 and Blazor did not initialize. These were environment startup failures before dialog assertions. The passing retry used a freshly restarted server without concurrent build commands. Only the agent-owned temporary server was restarted; no Visual Studio process was stopped.

Limitations: the browser specimens use real rendered components, production JS, and generated SCSS with vendor styles disabled; bUnit covers C# dispatch. No authenticated backend-writing journeys, Infrastructure tests, database resets, deployment, or release publish. Existing AngleSharp NU1902 and unrelated E2E analyzer warnings remain. Fixed header/actions must fit within the viewport to leave useful body space; prior palette contrast limitations remain.

Rollback: reverse this batch's enum, sizing/theme/entry-point changes, dialog API/SCSS, both callers, tests, and documentation together. Restore previous fragment names and dialog sizing from the pre-batch staged checkpoint without resetting or undoing unrelated staged work. The next migration batch remains native notifications and `ISnackbar` consumers.

### Batch 12 — Native notification service and snackbar consumer migration — 2026-10-03

Scope: notifications only. Prior staged migration work was preserved. No changes to business operations, authorization, repositories, backend contracts, or SDD workflow artifacts; no commit/deployment.

- Added `Common/Notifications/{IShopNotificationService,ShopNotificationService,ShopNotificationMessage,ShopNotificationKind}.cs`, `ShopNotificationHost`, `ShopNotification`, `_notification.scss`, two theme layout tokens, and six resource labels.
- Registered one scoped concrete/interface instance and `TimeProvider.System`. Added the notification host to `ShopUiHost`; removed snackbar providers only after migrating all 15 source consumers and their existing mock assertions together. Updated the shared E2E notification locator. Inline `MudAlert` consumers remain unchanged.
- Preserved caller-resolved messages, kinds, result branching, and navigation. Bounded storage, duplicate coalescing, no animation, polite announcement, neutral text/border-accent treatment, and explicit timer-pause rules are documented intentional choices above.
- Added 18 deterministic Web tests for expiry, timer disposal, duplicate/overflow handling, stale dismissal, host transfer, pause semantics, encoded markup, resource labels, and live-region ownership. Added four browser cases for actual Blazor error/success/navigation/expiry/dismissal and vendor-free responsive/large-text notification specimens. Auth RPC/OTP requests are fulfilled locally; no emails or backend writes occur.

Verification checkpoint:

- Baseline Web suite **951 passed**; migrated existing suite **951 passed**; full suite with new coverage **969 passed, 0 failed, 0 skipped**.
- E2E project build succeeded; design-rule gate clean; `git diff --check` clean. Source/test scan finds no application `ISnackbar`, `MudSnackbarProvider`, or `.mud-snackbar` selectors.
- Browser run was interrupted to honor the owner's **no headless** instruction. The verified test-process tree was stopped, then subsequent runs used `E2E_HEADLESS=0`. Do not count the interrupted run as verification. A stale dev-server module manifest caused initial startup failures; only the agent-owned temporary server was restarted after builds.
- Final headed suite: **21 passed / 2 failed / 0 skipped**, including **all four new notification checks and both picker checks passing**. The two existing button-color hover/pressed checks then passed on an isolated headed retry (**2 passed / 0 failed**), without changing their code or product styles. All 23 selected checks therefore passed across these runs, not in one clean full run. Preserve both results rather than claiming a single 23/23 run.
- Command: `dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --no-build --no-restore --nologo --blame-hang-timeout 2m`, with `E2E_HEADLESS=0`, filter `Feature=native-ui|FullyQualifiedName~ShopImageJourneyTests.AC3_|FullyQualifiedName~ShopImageJourneyTests.AC4_|FullyQualifiedName~ShopImageJourneyTests.AC5_|FullyQualifiedName~ShopImageJourneyTests.AC6_|FullyQualifiedName~ShopImageJourneyTests.AC7_|FullyQualifiedName~ShopImageJourneyTests.AC9_`, and TRX logger `notification-batch12-headed.trx`. Retry filter: `FullyQualifiedName~NativeButtonColorJourneyTests`, logger `notification-color-retry-headed.trx`. Both files are under `tests/TheShop.E2E.Tests/TestResults/`.
- Real-Blazor notification checks verify encoded/resource-backed operation feedback, hover/focus pauses beyond five seconds, keyboard dismissal, success navigation to OTP, and expiry. The mobile expiry assertion explicitly moves the cursor away: the new toast can cover the old submit button's pointer position and correctly remain paused. The picker test now respects the owner's existing `ShopMaxWidth.Small` selection and fractional-device-pixel border rounding; no picker production code was changed.
- Reviewed mobile/desktop `notifications-{390,1440}.png` and large-text variants under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/`. All four kinds wrap; 200% text stays inside the viewport with scrollable overflow and visible keyboard-dismiss focus. No Figma toast match is claimed.
- `graphify update .` completed: **8,788 nodes / 22,457 edges**. Existing empty-file, missing SQL parser, and community-label fallback warnings remain; no additional dependency installed.

Verification boundary: no authenticated admin/backend-writing journeys, Infrastructure tests, database resets, release publish, or screen-reader certification. Existing AngleSharp NU1902 and unrelated E2E xUnit1051 warnings remain. Existing palette contrast gaps elsewhere are unchanged.

Rollback: reverse this batch's service/DI, host/components/styles/resources, consumer/test substitutions, and documentation together. Restore the two snackbar providers and previous `ISnackbar` injections from the pre-batch staged checkpoint before removing the native notification service. Do not broadly reset or disturb previous batches. Next batch: replace the existing loading overlay's Mud presentation while preserving `BusyState`, `BusyKeys`, and `BusyFor`, then continue shell/auth controls in dependency order.

### Batch 13 — Figma snackbar appearance and bottom-center placement — 2026-10-03

Scope: presentation alignment with owner-supplied Figma Snackbar `2948:17575`. Used project constitution and surgical-patch rules; no SDD-stage dependency, new service abstraction, business change, or shared button variant. Preserved earlier staged/unstaged migration work.

- Updated `ShopNotification.razor`/code-behind, `_notification.scss`, and `_theme.scss`. Every kind now uses the same primary/primary-contrast bar with subtitle-2 text, 24px padding/gap, square corners, and an 18px close icon. Removed visible kind labels, kind classes, and severity borders; retained `ShopNotificationKind`, message records, service API, and all callers unchanged.
- Content-sized bars sit bottom-center, capped at 24rem with a 16px viewport gutter. Raised `--shop-layer-notification` from 1400 to 1500; native modal dialogs still render above document stacking layers. The sample's 241×66px dimensions are verified, not enforced on arbitrary message copy.
- Native notification-owned dismiss chrome keeps a 24px hit target around the Figma 18px footprint. This avoids feature overrides of shared button color/geometry. Primary-contrast focus ring remains visible. Encoded text, polite announcements, hover/focus pause, five-second expiry, navigation persistence, duplicates, and queue handling remain unchanged.
- Updated section 8.3 and canonical component guidance. Extended unit coverage to assert identical rendering while retaining each kind; extended browser coverage for Figma measurements, bottom-center placement, stacking layer, dismiss target size, and focus contrast without vendor CSS.

Verification:

- Baseline notification tests: **18 passed / 0 failed / 0 skipped**. Full Web tests after production edits: **969 passed / 0 failed / 0 skipped** (`dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --no-restore --nologo -m:1 -p:UseSharedCompilation=false`).
- E2E build succeeded. Final **headed** notification suite: **4 passed / 0 failed / 0 skipped** at 390×844 and 1440×900. Command: `dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --no-build --no-restore --nologo --filter FullyQualifiedName~NativeNotificationJourneyTests --blame-hang-timeout 2m --logger "trx;LogFileName=notification-figma-final-headed.trx"`, with `E2E_HEADLESS=0`.
- Earlier attempt: four failures before sign-in loaded against the existing Visual Studio server. Owner stopped that server; verification used a freshly started agent-owned server. First retry: two behavior checks passed, two centering assertions failed because they included the browser scrollbar. Corrected assertions to use the layout viewport's `clientWidth`; no production centering change. Retained initial/retry/final TRX results under `tests/TheShop.E2E.Tests/TestResults/`.
- Visually reviewed `notifications-{390,1440}.png` and `notifications-large-text-{390,1440}.png` under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/`. All kinds share the Figma bar. Long copy at 200% text wraps; mobile stacks scroll and keyboard-dismiss focus remains visible.
- Design-rule check and `git diff --check` passed. Knowledge graph refreshed. Existing AngleSharp NU1902, unrelated E2E xUnit1051, and graph parser/label warnings remain. No authenticated backend writes, deployment, or headless browser run.

Rollback: reverse only this batch's notification renderer/styles, three theme tokens, corresponding tests, and documentation. Keep the existing native service, notification kind API, and callers. Preserve all earlier migration work. Next migration scope remains the native loading overlay; no additional migration slice was started.

### Batch 14 — Field focus and sign-in loading correction — 2026-10-03

- Fixed duplicate focus decoration: shared focus rules no longer outline `.shop-field-input`; the existing field wrapper owns the bottom stroke. Invalid styling also uses the wrapper with direct `--shop-color-error`, while validation text/ARIA remain intact. Forced colors retain a wrapper outline. Keyboard-focus outlines on actual buttons/links remain unchanged; arrow icons have no independent outline.
- Owner confirmed the spinner stays. Visually hid the extra Loading status through `.shop-visually-hidden`; retained its resource text and status semantics, disabled/busy state, and duplicate-submit guards. No shared button API or business behavior changed.
- Verification: **41 focused Web tests passed**, E2E build succeeded, and **4 headed browser checks passed** (mobile/desktop input validation, focus, busy state, forced colors, and a vendor-free case). Browser command: `dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --no-build --no-restore --nologo --filter FullyQualifiedName~NativeUiJourneyTests.SignIn_ --blame-hang-timeout 2m --logger "trx;LogFileName=field-focus-loading-headed.trx"`, with `E2E_HEADLESS=0`. All backend responses were mocked or blocked; no OTP email was sent.
- Reviewed `signin-390-field-focus.png`, `signin-{390,1440}-busy.png`, and `signin-390-forced-colors-focus.png` under the existing `native-ui-evidence` directory. The vendor-free desktop specimen also exposes the static Blazor error template because its hiding rule belongs to disabled `app.css`; this is not a runtime exception, and the form completes/re-enables in the test.
- Design gate and whitespace checks passed; graph refreshed. Existing package/analyzer/graph warnings remain. No full-suite, deployment, or screen-reader certification claim. Rollback only this batch's field/accessibility SCSS, SignIn status class, focused tests, and documentation; preserve previous migration work. Native loading-overlay migration remains next.

### Batch 15 — Direct icon-click focus regression — 2026-10-03

- Corrected the batch-14 verification gap: it checked keyboard activation, not direct pointer-down on the SVG. Two new headed tests reproduced the bug with and without vendor CSS: clicking the icon did not focus its owning button.
- `ShopIcon.razor` now suppresses `tabindex` with an explicit null attribute, also overriding unmatched caller values. `_icon.scss` delegates pointer targeting to the owning control. This fixes decorative icon focus at its source; no global outline suppression, page workaround, or button API change.
- Updated icon unit assertions for default/conflicting attributes and added two browser cases covering text buttons and icon-only buttons, real icon-coordinate pointer-down/up, one click activation, no pointer outline, and preserved Tab focus on the parent.
- Verification: **51 focused Web tests passed**; E2E build and design/whitespace checks passed. Before fix: **2 headed regression cases failed** (`icon-click-before-headed.trx`). After fix: **6 headed cases passed**, including the four sign-in focus/loading checks (`icon-click-after-headed.trx`). Used `E2E_HEADLESS=0` and filter `FullyQualifiedName~Buttons_DirectIconClick|FullyQualifiedName~NativeUiJourneyTests.SignIn_`. No backend writes. Graph refreshed; existing package/parser warnings remain.
- Rollback only this batch's icon markup/SCSS, icon unit assertions, browser regression cases, and documentation. Previous field/loading fixes remain intact. The shared button still shows a visible outline for keyboard focus, deliberately.

### Batch 16 — Centralized spinner-only button loading — 2026-10-03

- Added `Loading` to `ShopButton` and `ShopIconButton`. The icon button forwards to the shared implementation. Neither subscribes to `BusyState`, starts work, nor stores independent busy state; callers supply `BusyFor`'s value.
- The shared button owns effective disabling (`Disabled || Loading`), synthetic-event protection, enforced `aria-busy`, an absolutely centered decorative spinner, and a primed visually hidden status sibling. Label/icons remain mounted and transparent while busy, preserving dimensions and accessible names. No fixed text-button width, extra outer wrapper, or page-level duplicate status.
- Sign-in, the only existing native inline button-spinner consumer, now passes `Loading="@busy"`. Validation, keyed operation tracking, disabled inputs, duplicate-submit guard, and recovery remain unchanged. Legacy Mud loading controls are outside this batch.
- `_button.scss` owns content/spinner positioning; `_icon-button.scss` supplies the existing variant/size icon dimension to the shared spinner. Existing spinner animation, reduced motion, disabled colors, focus, and icon-pointer handling remain intact. Loading appearance is an approved interaction decision, not a new Figma measurement.
- Verification: **974 Web tests passed**, including five new loading cases. Initial focused run exposed a test-only reference-equality assumption about bUnit wrappers; changed it to retained-content assertions and verified actual DOM identity in the browser. E2E build succeeded; **10 headed browser checks passed** (`button-loading-headed.trx`, `E2E_HEADLESS=0`, filter `FullyQualifiedName~NativeUiJourneyTests`). Coverage includes mobile/desktop Sign-in loading/recovery and retained DOM nodes, accessible names, all nine variant/size combinations for both button types, unchanged resting geometry, centered/size-correct spinners, reduced motion, forced-colors border presence, vendor-free rendering, and direct-icon-click focus. All backend traffic was mocked or blocked; no OTP was sent.
- Visually reviewed `buttons-loading-390.png` and `signin-390-busy.png`: only spinners visible while busy, original button dimensions retained. Automated accessible-name/status checks do not constitute screen-reader certification. Existing AngleSharp NU1902 and unrelated E2E xUnit1051 warnings remain.
- Rollback only this batch's loading parameters/rendering, button/icon-button SCSS, Sign-in substitution, associated tests, and documentation together. Preserve preceding migration work. Next migration scope remains the native loading overlay, not another UI framework.

For each completed batch record:

```text
Date:
Branch and starting commit:
Pre-existing user changes:
Phase and component/page scope:
Files changed:
Preserved behavior:
Intentional differences and reasons:
Commands executed and exit codes:
Test counts: passed / failed / skipped:
Browser/visual evidence:
Temporary compatibility remaining:
Unresolved issues:
Last verified checkpoint:
Next concrete action:
```

On resume, read the latest record, inspect current Git state, and verify that completed work still exists. Continue from the first incomplete dependency. Do not repeat package removal, create duplicate hosts, or redo verified phases without new evidence that they need work.

The final implementing-agent response should state what changed, the exact verification performed, remaining limitations, any intentional native-control differences, and where evidence lives. Do not merely report that tags were replaced or the solution compiled.

## 19. Prompt to give the implementing AI

> Implement the migration described in `UI_MIGRATION_GUIDE.md`. Treat it as a standalone technical handoff; do not require the SDD stage workflow or create SDD artifacts as prerequisites. Reinspect the current branch and preserve all existing user changes. Follow the approved native HTML, SCSS, and Blazor architecture, align only conflicting canonical Mud-specific rules/checks, and execute the technical phases with actual verification. Keep behavior and backend contracts intact. Update the execution record as you progress. Do not deploy, publish, reset databases, or replace unrelated systems. Report unavailable verification honestly and ask only when a material choice or unavailable authority blocks progress. Do not claim completion until the definition of done is satisfied.

## 20. Technical references

Check documentation against the installed .NET/browser versions when implementation details are uncertain. These references supplement this handoff; they do not add a workflow dependency.

- [Blazor built-in input components](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/input-components?view=aspnetcore-10.0)
- [Blazor form binding and custom inputs](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/binding?view=aspnetcore-10.0)
- [Blazor form validation](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-10.0)
- [Blazor JavaScript interoperability and DOM ownership](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/?view=aspnetcore-10.0)
- [Native dialog element](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/dialog)
- [WAI modal dialog interaction pattern](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/)

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
      ShopMoneyField.razor / ShopMoneyField.razor.cs
      ShopNumericField.razor / ShopNumericField.razor.cs
      ShopDateField.razor / ShopDateField.razor.cs
      ShopDialog.razor / ShopDialog.razor.cs
      ShopDrawer.razor / ShopDrawer.razor.cs
      ProfileDrawer.razor / ProfileDrawer.razor.cs
      ShopDialogHost.razor / ShopDialogHost.razor.cs
      ShopConfirmDialog.razor / ShopConfirmDialog.razor.cs
      ShopNotificationHost.razor / ShopNotificationHost.razor.cs
      ShopNotification.razor / ShopNotification.razor.cs
      ShopImage.razor / ShopImage.razor.cs
      ShopImagePreset.cs
      ShopImageUpload.razor / ShopImageUpload.razor.cs
      ShopFileDropzone.razor / ShopFileDropzone.razor.cs
      ShopImageTile.razor / ShopImageTile.razor.cs
      ShopUploadedImage.cs
      ShopPagination.razor / ShopPagination.razor.cs
      ShopBreadcrumbs.razor / ShopBreadcrumbs.razor.cs
      ShopAppBar.razor / ShopAppBar.razor.cs
      ShopBadge.razor / ShopBadge.razor.cs
      ShopSortSelect.razor / ShopSortSelect.razor.cs
      ShopFilterPanel.razor / ShopFilterPanel.razor.cs
      ShopBulkActionBar.razor / ShopBulkActionBar.razor.cs
      ShopRichTextEditor.razor / ShopRichTextEditor.razor.cs
      ShopLoadingOverlay.razor / ShopLoadingOverlay.razor.cs
      BusyFor.razor / BusyFor.razor.cs
      OtpInput.razor / OtpInput.razor.cs
      [existing footer and access views]
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
    CurrencyFormatter.cs
    UI/
      ShopBreadcrumbItem.cs
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
      _layers.scss
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
      _file-dropzone.scss
      _image-tile.scss
      _otpinput.scss
      _breadcrumbs.scss
      _appbar.scss
      _badge.scss
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
    js/shopFileDropzone.js
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
- `tokens/_theme.scss`: shared radius, focus, interaction states and field chrome/motion (TextField and Select). Component-only measurements do not belong on global `:root`.
- `tokens/_layers.scss`: shared document stacking order; native modal dialogs/drawers use browser top-layer ordering instead of z-index values.
- `base/_reset.scss`: minimal browser normalization, not an aggressive removal of native behavior.
- `base/_document.scss`: body background, foreground, document sizing, boot/error presentation as appropriate.
- `base/_typography.scss`: applies typography defaults to semantic HTML.
- `base/_accessibility.scss`: shared focus treatment, visually hidden content, reduced-motion rules.
- `components/`: selectors and component-only measurements owned by reusable component families, including feature components such as product cards. Keep local custom properties on the component root or closest owning selector; use ordinary declarations for one-off values. Reuse shared colors, typography, spacing and sizing tokens directly; do not duplicate them locally. Defaults moved from `:root` are now overridden on the component itself, not an ancestor.
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
- The spinner is absolutely centered, decorative, and sized from the button's existing icon-size value. Icon-only variants supply their own size. Use `ShopLoader` animation and reduced-motion behavior; loading is an owner-approved interaction decision, not a measured Figma state.
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

### 6.5 Expander, Chip and Pagination — batch 25

Inspected live Figma sections on 2026-10-03: [Expander 2955:19054](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2955-19054), [Chip 2961:19727](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2961-19727), and [Pagination 2963:20185](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2963-20185). No Figma mutations. All three components derive from `ShopComponentBase`; root Class/Style/AdditionalAttributes are forwarded. Colors, typography and spacing consume existing shared tokens; component-specific dimensions stay local to their component SCSS.

#### ShopExpander

- Component set `2141:6254`: default/custom header, collapsed/expanded. Top/bottom inside 1px default-line strokes, no side border/fill/radius; 24px block padding, 16px header gap, 32px header-to-body gap. Default title is H4 (34px Barlow Condensed Bold); overview is Subtitle 1 (16px/500) in secondary text. The toggle is a 24px footprint containing an 18px plus/minus icon and 3px padding, not a nested icon button. The 605.25px sample width and 120px sample body height are not component constraints. Default sample is 89px collapsed / 241px expanded with that body.
- API: `Title` or non-interactive phrasing `TitleContent`, optional non-interactive `OverviewContent`, `ChildContent`, `Expanded`, `ExpandedChanged`, `Disabled`. Default is collapsed; `@bind-Expanded` supports external ownership. Without a change callback, local toggles survive unrelated parent renders; a changed Expanded parameter resynchronizes state. Groups open independently, without an accordion registry/provider.
- A semantic h3 contains one native type=button trigger with stable aria-controls/aria-expanded. Header slots must not contain buttons, links, inputs or other interactive descendants. Title/overview wrappers use flex alignment so custom smaller text and chips center with the toggle rather than sitting on the default H4 baseline. Contiguous sibling expanders share one divider: the first draws top/bottom strokes, later siblings draw only the bottom stroke (also enforced with borders in forced colors).
- The body remains mounted when collapsed, retaining input/component state. `inert` and `aria-hidden` exclude descendants from interaction/assistive navigation immediately. A 0fr/1fr grid transition animates natural content height over 280ms with cubic-bezier(0.4, 0, 0.2, 1); an inner clipping wrapper contains body padding. Visibility hides after collapse and becomes visible immediately on expansion. Do not restore the HTML `hidden` attribute/display:none during animation, add arbitrary max-height caps, or use JavaScript timers. Reduced-motion disables the transition. Disabled prevents user toggles; external state can still change. Motion, focus and forced-colors treatments are implementation/accessibility decisions, not supplied Figma states.
- `_expander.scss` owns chrome. `ShopFilterPanel` uses keyed expanders for all filter kinds and a custom smaller filter title. `_filter-panel.scss` owns filter layout only. Batch 29 replaces the temporary `MudRangeSlider` with `ShopRangeSlider` while keeping range debounce in the panel. Product editor accordions still use Mud and `_expansionpanel.scss`; do not remove that bridge yet.

#### ShopChip

- Component set `180:485`: square, transparent outlined chips. Default border is lines-default; selected border is primary. No filled/color variants are introduced. Text stays text-primary in both states.

| Size | Minimum height | Inline padding | Gap | Icon | Typography |
|---|---|---|---|---|---|
| Small | 24px | 8px | 4px | 18px | Caption, 12px/400 |
| Medium (default) | 32px | 12px | 4px | 20px | Body 2, 14px/400 |
| Large | 40px | 16px | 6px | 24px | Body 1, 16px/400 |

- API: localized non-interactive `ChildContent`, shared `ShopSize`, `Selected`, optional `SelectedChanged`, `Disabled`, trusted decorative `StartIcon`/`EndIcon`. Without SelectedChanged it is a non-interactive span (for labels/counts); with it, a native type=button with aria-pressed. Selection remains caller-controlled; activation requests the opposite state, disabled suppresses callbacks. EndIcon is decorative, not an implicit remove action. No generic chip-set/radio form owner is added; existing form ChipSets remain until their behavior is migrated deliberately.
- Insets use box-shadow to preserve Figma's inside-stroke dimensions. Width follows content; long labels wrap, and min-height may grow. Hover, disabled, visible focus and forced-colors treatments are inferred. `_chip.scss` retains isolated `.mud-chip*` overrides for unconverted consumers; native chips do not consume them. The filter count uses a read-only Small chip, avoiding an interactive control inside the expander trigger.

#### ShopPagination

- Set `2963:20184` shows All/Start/Middle/End ranges: page 3 of 5; page 3 of 20; page 10 of 20; page 18 of 20. Items are 64px minimum width, numeric items 40px minimum height, arrow items 44px, gap 6px, 22px Arrow_Left_MD/Arrow_Right_MD. Button typography is 14px/500. Selected page has a primary outline, others default-line outlines; backgrounds remain transparent. Figma's 2026-10-03 ellipsis update uses a 22px `More_Horizontal` icon centered in the unchanged 64px × 40px slot, secondary color and no outline. Render the trusted `ShopIcons.Outlined.More_Horizontal` through `ShopIcon`, reusing `shop-pagination-icon` sizing/stroke rules; do not use a text ellipsis or add a clickable action.
- Existing one-based Page/TotalPages/PageChanged API is preserved, with an added Disabled guard. A named nav contains a list of native buttons; exactly one current page uses aria-current=page. Resource-backed previous/next/page labels are on buttons; ellipses are non-interactive and hidden from assistive technology. Selection does not mutate Page or fetch data; the existing catalogue/admin page owns loading, URL/query state and history.
- Zero/negative totals hide navigation. Out-of-range Page is clamped for presentation without firing callbacks. One page disables both arrows; current/disabled/out-of-range requests emit nothing. Always include first/last pages; show all through seven pages, first/last five near a boundary, otherwise current ±1 with ellipses. Rendering stays bounded even for int.MaxValue totals, with overflow-safe window arithmetic. Keys preserve page-button identity as windows move.
- `_pagination.scss` is fully native; no remaining MudPagination consumers. Native pagination owns its distinct Figma geometry rather than overriding shared ShopButton variants. At narrow widths or enlarged text, the list wraps in source order instead of overflowing or shrinking targets. This responsive choice, hover/disabled/focus/forced-colors states, and empty-state rules are implementation decisions; Figma supplies desktop resting states only.

### 6.6 Appbar, Breadcrumb and Badge — batch 27

Inspected live Figma sections [Appbar 2970:9248](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2970-9248), [Breadcrumb 2963:21466](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2963-21466), and [Badge 2961:19777](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2961-19777) on 2026-10-04. Node geometry, style bindings and screenshots are the visual source; no Figma edits.

**Appbar:** `ShopAppBar` renders a native header, named primary nav, route-backed anchors, existing `ShopImage` logo, and shared outlined Medium icon treatments. `_appbar.scss` owns the layout. Component `153:5576` is 64px high at default text size, 16px inline inset, 52px logo, 36px navigation gaps, and 24px action gaps with 34px icon controls. Grid columns hug the logo/actions and let the center navigation fill available space. The Figma grid reports 8px vertical padding, but its centered 52px logo actually starts at y=6 in the 64px frame; implementation follows that visible geometry using 6px block padding and a growing minimum height. Background is `--shop-color-surface-appbar`. Small nav links use Button typography and uppercase treatment. Below 960px, links wrap in a second row; this responsive treatment is inferred, not a supplied mobile design. No invented hamburger/menu framework. Search remains the existing unwired action; this batch does not add search behavior or missing route destinations. Batch 30 replaces the signed-in ProfileMenu bridge with the MainLayout-owned ProfileDrawer. The appbar only raises an account request and reflects the layout's open state. Other Mud consumers still require their providers.

**Breadcrumb:** `ShopBreadcrumbs`, `BreadcrumbTrail` and `BreadcrumbState` use `Common/UI/ShopBreadcrumbItem`, not Mud's model. Existing builder/state APIs, route safety checks, layout subscription/clearing and page consumers retain their behavior. A native named nav contains an ordered list; ancestors with destinations are anchors, disabled/removed ancestors are spans, and only the final span has `aria-current="page"`. Text remains encoded with full title labels. `Class`, `Style` and unmatched attributes target nav; the owned accessible name wins. Figma `357:4466` / `2963:21430`: subtle surface, 42px natural strip at default text size, 36px desktop inline padding, 8px gaps, 24px chevrons, Button text with 4px/5px padding, and a shared `ShopIconButton` (Small / Text / Primary) with the 18px `More_Horizontal` icon in its 24px footprint (Figma instance `2974:9343`, rechecked 2026-10-04). Below 960px, preserve the previous root/current-only collapse and access to every intermediate item via the disclosure; mobile inline padding is 16px. CSS controls visibility, without a breakpoint provider or resize JS. Click/Enter/Space reveals ancestors and removes the ellipsis button and its separator. Before expansion the button exposes `aria-expanded="false"`, `aria-controls` and the Show parent pages label. After rendering, focus moves to the first revealed ancestor; a disabled ancestor can receive programmatic focus without entering the tab order. The full trail appears at desktop widths; a changed trail resets expansion, an equivalent rerender does not. Wrapping, a 20rem truncation cap and growing height accommodate long labels/zoom. Those overflow/interaction details are implementation choices, not additional Figma variants.

**Badge:** `ShopBadge` is a non-interactive span with localized `ChildContent`; styling/attributes target that span. Reuse shared `ShopVariant` (Filled/Outlined only), `ShopColor` (Primary/Secondary/Info/Success/Warning/Error), and all three `ShopSize` values. Defaults are Primary/Filled/Medium; unsupported variants/colors fail explicitly. No click, close, selection, count, dot or placement engine. `ShopChip` remains the selectable control. Set `2961:19776` defines Small/Medium/Large at 24/32/40px minimum height, 8/12/16px inline padding, Caption/Body 2/Body 1 typography, square corners and content-derived width. Height may grow with wrapped text. Filled labels consume role contrast tokens directly; outlined labels consume role colors. Outlined Primary alone uses `--shop-color-lines-default` for its inside 1px stroke; other outlined colors use their role token. Forced colors uses a system border instead of an inset shadow. The approved palette's existing small-text contrast limitations remain; matching Figma does not establish WCAG color compliance. The existing first-image Primary label now uses a Small outlined `ShopBadge`; upload selection/order/validation remain unchanged.

```razor
<ShopBadge Size="ShopSize.Small" Variant="ShopVariant.Outlined"
           Color="ShopColor.Primary">@Strings.AddProduct_ImagePrimary</ShopBadge>
```

This example uses the existing product-gallery Primary resource label.

### 6.7 Bulk Action Bar — batch 28

Source: [Bulk Action Bar section 2976:9566](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2976-9566), component `2976:12393`, and [Manage Brands 2534:5044](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2534-5044), inspected 2026-10-04. The page instance `2976:12472` fills the Brand Section; the specimen's 1250px width is not a component constraint.

- Keep `ShopBulkActionBar` page-owned above the table, after search/sort, in Manage Brands, Categories and Products. Existing `Visible`, `SelectedCount`, `Actions` and `OnClose` contracts remain; selection, permission gates, confirmations, mutations, partial outcomes and busy state remain with each page. No layout-owned selection service, cloned action bar, or page-specific commands in MainLayout.
- Appearance: subtle surface, default-line 1px border, square corners, 16px padding/gaps. The 72px desktop example is natural height, not a fixed constraint. Count uses H5 with minimum two digits; Selected uses uppercase Caption with an 8px gap. A polite status exposes the unpadded localized count once. Action buttons are Medium / Outlined, Primary for Active/Inactive, Error for Delete; use Show/Hide/Trash_Empty icons. Close is Medium / Outlined / Primary with Close_MD, separated by a default-divider line and 16px gutter. No Mud component or token inside the bar or its migrated action slots.
- `Class`, `Style` and unmatched attributes target the named section, not its positioning wrapper. Actions retain native button semantics; no toolbar role or custom arrow-key model is introduced. Close requests the page clear its selection and does not mutate Visible internally.
- MainLayout supplies only `data-shop-scroll-root` on its existing scroll container beneath appbar/breadcrumbs. `_shell.scss` applies measured docking scroll padding. The bar fills its page container normally; after its anchor passes the scroll-container top, it docks to that top and fills the scrollport's client width (excluding the scrollbar). It returns inline when its anchor re-enters. It remains available while scrolling into the footer until selection clears or navigation disposes it. No hardcoded appbar/breadcrumb heights.
- `shopBulkActionBar.js` is a small component-owned geometry/lifecycle module. It reserves the inline height, retains one DOM instance, batches scroll work with requestAnimationFrame, observes size/removal, uses a 1px threshold tolerance, and cleans up listeners/observers/animation/scroll padding on hide or disposal. No continuous .NET scroll callbacks or global bulk state. Before module initialization, or when no shell scroll root exists, the bar renders inline.
- Dock/undock animates measured horizontal geometry and a bounded vertical correction over 200ms with `cubic-bezier(0.2, 0, 0, 1)`, not scale or opacity: text stays sharp and controls keep focus. Reversals start from the current rendered position. Reduced motion skips animation. Geometry variables live on the slot; the shared stacking contract is `--shop-layer-sticky: 1000`, below notifications and existing popup layers; native dialogs retain top-layer priority.
- Mobile adaptation (inferred, not a supplied Figma variant): below 640px, count/close share the first row and actions wrap below. Height grows for zoom and wrapping; resize/font changes refresh the reserved inline footprint. Existing palette contrast limitations still apply to the Error outline/text.
- Remove the former fixed-bottom placement, vendor z-index/background, and selection-dependent bottom page padding. Leave unrelated list filters, search fields, row content, footer and layout Mud consumers for their own migration batches.

### 6.8 Range Slider — batch 29

Source: [Range Slider section 2976:16148](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2976-16148), component `2976:16157`, thumb `2976:16149`, tooltip `2976:16152`, inspected 2026-10-04. The 400px specimen fills its parent; it is not a fixed component width.

- `ShopRangeSlider` is a generic **decimal** interval control, not a price component or generic numeric framework. It owns two native range inputs, the active interval, endpoint labels, active-thumb tooltip and two outlined `ShopNumericField` editors (shared in batch 37). `ShopRangeValue(Lower, Upper)` travels in one `ValueChanged` callback; `@bind-Value` is supported. `Min`, `Max`, positive `Step`, `Disabled`, `Label`, `MinimumLabel`, `MaximumLabel`, and optional `ValueFormatter` form the public contract. Class/Style/unmatched attributes target the fieldset. Internal IDs remain component-owned.
- The consumer supplies all currency/unit formatting. Display-only formatting applies to endpoints, tooltip, accessible value text and idle editors. Focus edits a plain invariant decimal number; Enter or blur commits it. Invalid/empty/out-of-bounds text stays visible with an associated localized error and never changes the applied range. Escape restores the current number. Enter retains plain-number editing until blur. This edit interaction is an implementation decision; Figma supplies the visual states, not parsing behavior.
- Clamp and snap thumb movement without crossing. Steps are anchored at Min; the maximum endpoint remains selectable even when not step-aligned. Normalize incoming values for display without emitting callbacks. Equal bounds disable all controls; invalid bounds/steps fail explicitly. Keep decimal arithmetic in C#; the small JS module supplies native pointer/keyboard candidates only.
- Geometry: 24px circular thumb, 1.5px primary border, white surface ring, 14px primary dot; default-line track 2px, active primary track 3px, rail inset 12px. Tooltip uses primary/primary-contrast, Body 1, 8px/12px padding and 12px × 6px pointer. Endpoint labels use Body 2; fields use the existing outlined field and floating Caption label, a 16px column gap and 57px minimum height. Component-only values belong in `_range-slider.scss`, not global tokens.
- Thumbs have distinct accessible names and stable tab order, dynamic ARIA bounds and formatted value text. Arrow keys move one step, Page Up/Down ten, Home/End to the effective bound. Numeric editors provide a non-drag alternative. Focus-visible and forced-color affordances remain. A browser's native thumb drag and a small track-hit handler provide pointer interaction; module listeners are removed on disposal. No vertical/logarithmic/multi-thumb options or new UI dependency.
- `ShopFilterPanel` retains group keys, the 300ms debounce and conversion from full-range endpoints to nullable/unbounded filters. Unrelated parent renders must not reset pending drafts; external filter/bounds changes, Clear, group removal and disposal cancel obsolete commits. `RangeStep`, `RangeMinimumLabel`, `RangeMaximumLabel` and the existing `RangeFormatter` delegates keep group-specific units outside the generic component. ProductCatalogue and ManageProducts supply CAD labels/formatting and a 0.01 step; other consumers default to plain numbers and a step of 1.
- Responsive adaptation (inferred, not supplied as a separate Figma variant): an auto-fitting grid stacks the editors when two 9rem minimum fields and their 1rem gap no longer fit. This follows actual text sizing without relying on a fieldset container query. Thumb movement is direct; no interpolation delays a dragged value.
- This is a non-form filter control. It does not claim `InputBase<ShopRangeValue>`/EditContext validation integration. A future validated interval form requires that contract explicitly; do not assume internal editor errors validate the parent form.

```razor
<ShopRangeSlider Label="@Strings.Filter_Price"
                 MinimumLabel="@Strings.Range_MinimumCad"
                 MaximumLabel="@Strings.Range_MaximumCad"
                 Min="0m" Max="500m" Step="0.01m"
                 @bind-Value="_range" ValueFormatter="FormatPrice" />
```

The consumer's code-behind owns `_range = new ShopRangeValue(0m, 500m)` and `FormatPrice`; no price/currency state lives in the component. For rollback, restore the former filter range branch and its tests together with the page-specific delegates; no stored data or API contract changes are involved. Leave the package/asset removal until the last unrelated Mud consumer is migrated.

### 6.9 Announcement Bar — batch 33

Design: [Figma Announcement Bar 2985:16495](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2985-16494), inspected 2026-10-05.

- `ShopAnnouncementBar` derives from `ShopComponentBase`; `Class`, `Style` and unmatched attributes target the root `div.shop-native.shop-announcement-bar`. The required server-provided `Message` renders as encoded text in `p.shop-announcement-bar-message`. The public API and `MainLayout`/`AnnouncementState` visibility ownership are unchanged.
- `_announcement-bar.scss`: 32px minimum height (Figma `minHeight`), 4px block / 16px inline padding (`--shop-space-1` / `--shop-space-4`), `--shop-color-primary` fill, `--shop-color-primary-contrast` Subtitle 1 text, centered. Height hugs content.
- Implementation decisions, not Figma variants: long or enlarged text wraps centered (`overflow-wrap: anywhere`) and the bar grows instead of overflowing; no landmark role, dismiss action or marquee is added.

### 6.10 OTP Input — batch 34

Design: [Figma OTP row 2629:1708](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2629-1708), inspected 2026-10-06 through the Desktop Bridge (the REST API was rate-limited).

- Figma: a horizontal row of six Text Field `170:136` instances, fill width, 24px gap, with no label/placeholder/icons. Each box is 63px tall with the standard field chrome: 1px `lines-input` idle and 2px primary when focused. The digit is Typography/H6 (Space Grotesk 20px/500, 0.25px), centered.
- `OtpInput` derives from `ShopComponentBase`; `Class`, `Style` and unmatched attributes target the root `div.shop-native.shop-otp-input`, and its component-owned container ID wins for the JS module. The public parameters are unchanged (`Length`, string `Value`/`ValueChanged`, `OnComplete`, `Disabled`, `AutoFocus`, `HelperText`). Callers pass `data-testid` and other root attributes directly instead of Mud `UserAttributes`.
- Each digit is a label-less `ShopTextField` (section 7.3) with `type="text"`, `inputmode="numeric"`, `autocomplete="one-time-code"` and the resource-backed `OtpInput_DigitAriaLabel` as its `aria-label`. A private per-box slot supplies the member `ValueExpression` that `InputBase` needs. `HelperText` renders as caption text in `p.shop-otp-input-hint`, and each box references it with `aria-describedby`.
- Input ownership: `shopOtpInput.js` owns digit keys (preventing insertion, writing the digit, then calling `HandleDigitKeyAsync`), paste distribution and focus select-all. Its Mud edit-mode workarounds are removed, and modified shortcuts such as Ctrl+digit are no longer intercepted. The input event covers deletion, virtual keyboards that report no key, and one-time-code autofill: a full code fills every box, non-digits re-render the previous digit, and otherwise the newest digit wins. Backspace on an empty box clears the previous one; arrows move focus. Paste and digit keys are ignored while `Disabled`.
- `_otpinput.scss` owns the row and digit typography. Below 600px the gap is 8px and box inline padding is 4px, so the centered digit stays visible in narrow cards. These mobile values are an inferred adaptation, not a Figma variant.

### 6.11 Rich Text Editor wrapper — batch 35

No Figma editor design exists (a 2026-10-06 file search found only storefront "Description" display frames). The wrapper therefore reuses the outlined field chrome of Text Field `170:136`; Quill 2.0.3, its snow toolbar and icons, formats, sanitization path and paste notice are unchanged (section 9, Product editor and rich text).

- `ShopRichTextEditor` derives from `ShopComponentBase`; `Class`, `Style` and unmatched attributes target the empty host `div.shop-native.shop-rich-text-editor`, which Quill alone populates. `Disabled` adds the owned `shop-rich-text-editor-disabled` modifier without remounting Quill. The public parameters and the `ProductContentCard` usage are unchanged.
- `_rich-text-editor.scss`: host 1px `lines-input` inset outline and 2px primary on `:focus-within`, no radius; Quill borders removed and a 1px toolbar separator; 8px/12px toolbar padding; 160px minimum editor with 12px block / field inline padding; Body 1 text in primary text color; secondary non-italic placeholder; forced-colors outline. Disabled: secondary editor text, `not-allowed` cursor, non-interactive toolbar with `text-disabled` icon strokes/fills/picker text (toolbar buttons do not inherit `color`).
- The host chrome owns editing focus, so `.ql-editor:focus` has no outline; toolbar buttons keep the shared `.shop-native` focus ring. Quill's own blue active/hover toolbar state remains under the existing Decision 2 waiver.

### 6.12 Skeleton — batch 36

Design: [Figma Skeleton 3010:17888](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3010-17888), component set `3010:17894`, created and approved 2026-10-06. It has one property, `Shape` = `Rectangle` | `Text`. Both shapes are a flat `Brand/Tertiary` fill with square corners. The owner rejected shimmer: there is no shimmer, pulse or other motion.

- Two layers. `ShopSkeleton` is the only primitive. Content-shaped skeletons live next to the component they stand in for and reuse its classes, so their geometry follows that component instead of copying numbers.
- `ShopSkeleton` derives from `ShopComponentBase` and renders one empty `span.shop-skeleton` with an enforced `aria-hidden="true"`. It deliberately omits the `shop-native` root class, because that class applies Body 1 and would break the text-style inheritance the Text shape relies on. `Shape` (`ShopSkeletonShape`, default `Rectangle`) emits `shop-skeleton-rectangle` or `shop-skeleton-text`. It has no width, height or count parameters (constitution rule 26). Callers size it with their own SCSS class and repeat it with `@for`. Rectangle has no intrinsic block size; the caller class must supply a block size or aspect ratio. Text keeps one line box (`1lh`) of the inherited text style and paints a centered bar `1em` high, so a caller class that applies the real text style gives a matching line. `_skeleton.scss` loads before the other component partials so caller size rules win at equal specificity. Forced colors draw a 1px `CanvasText` outline.
- `ShopTable.Loading` (typically the list's `BusyFor` value) keeps the real header. It replaces the body with `LoadingRowCount` hidden skeleton rows (default 10; one Text skeleton per cell, including the selection column) and sets `aria-busy` on the table. It also disables and guards selection, suppresses `EmptyContent` and fills an always-mounted visually hidden `role="status"` with `Strings.Loading`. The Manage Brands, Categories and Products pages render the table while busy or populated, and hide pagination while busy.
- Content-shaped skeletons (all decorative, `aria-hidden` roots):
  - `ProductCardSkeleton` reuses the `shop-product-card`, media, content, brand, title and price classes. Line widths are 40%, 80% and 30%.
  - `AdminModuleCardSkeleton` and `AdminFormSkeleton`, in `_admin-skeletons.scss`, copy the still-Mud admin card geometry (24px padding, 1px outline, 16px and 48px gaps, H6 18px label, H2 count, 36px action) and the edit-page geometry (32px padding, 24px gap, H3 heading, field-height block, 240px panel) with tokens. EditBrand, EditCategory and EditProduct share `AdminFormSkeleton`.
  - The catalogue filter skeleton is single-use page markup: five rows with the expander's 72px header height and dividers.
  - Historical: batch 36 used skeleton rows in `ShopImageUpload`. Batch 38 replaces those rows with image tiles and a loader only.
- Announcements: only `ShopTable` announces loading, because it stays mounted. Page skeletons that replace content are hidden and do not announce, which matches the previous `MudSkeleton` behavior.

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

#### Outlined-only text-field design — updated 2026-10-03

Source: [Figma Text Field section 2950:17591](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2950-17591), component set `170:136`. Inspected live data and screenshot: `2732:18850` Placeholder, `2732:18856` Normal, `2732:18862` Focused. The set now has only a `State` property, not filled/outlined variants. Native text fields are outlined-only: do not add a `Variant` parameter or filled modifier. Preserve unrelated legacy Mud consumers until their migration batch.

| Property | Inspected Figma value / implementation |
|---|---|
| Frame | 300px sample width; 61px height. Implementation fills its parent, uses scalable minimum height, and permits text growth. |
| Content | 18.5px block / 14px inline padding; 4px gap; 24px optional icons. |
| Surface / border | Transparent surface, square corners; 1px `lines-input` full outline at rest, 2px primary full outline on focus. Inset shadows preserve dimensions. |
| Empty, unfocused | Associated label sits inside at Body 1: Space Grotesk 16px/400, AUTO line height, 0.25px tracking, text-secondary. With a leading icon, text begins at x=42px. |
| Filled, unfocused | Label remains on the top border, Caption 12px/400 and text-secondary; input value uses Body 1/text-primary. |
| Focused | Label floats even when empty; Caption/text-primary, label frame x=10px with 4px inline padding and surface-default background masking the outline. Vertically centered on the top edge (Figma frame y=-7px). |

Use `ShopTextField` for this design. It owns one real `<label for>` inside `.shop-field-control`, after the `.shop-field-input`. Keep the input's stable ID, input type/name, autocomplete, required state, `aria-describedby`, and validation intact. The visually placeholder-like label is the accessible name. There is no public `Placeholder` option and no visible example text: an enforced single-space native placeholder exists only for CSS `:placeholder-shown` detection and remains invisible in every state. Do not animate or replace user-entered values.

Float through CSS `:focus-within`, `:not(:placeholder-shown)`, and `:autofill`, not duplicated Blazor focus/value flags. Keep it floated after blur when populated; return it inside after clearing and blurring. `ShopTextField` replaces `ShopTextInput` directly and derives from `InputBase<string?>`, preserving immediate updates and validation. Shared `_native-field.scss` owns chrome/motion. Do not retain two overlapping input components or add alternate variants.

```razor
<ShopTextField id="signin-email" type="email" name="email"
               @bind-Value="_model.Email"
               Label="@Strings.Email_Label"
               StartIcon="@ShopIcons.Outlined.Mention"
               autocomplete="email" required Disabled="@busy"
               aria-describedby="signin-instruction" />
```

`Label` is nonblank for every visibly labelled field. Only a control whose design has no visible label (currently OTP digit boxes, section 6.10) may omit it. That field then renders no `<label>`, keeps a caller `aria-label`/`aria-labelledby`, and throws when both are blank. `StartIcon` and `HelperText` are optional. Pass resource-backed text and trusted registry icons. `Disabled` is supplied by the form/BusyFor and prevents input-event updates as well as native editing. No BusyState injection or operation ownership in the field. The page still owns its model, EditContext, validation rules, and submission.

Native attributes (including lowercase `class`, `style`, `id`, `type`, `name`, autocomplete, required, and data attributes) target the actual input. The owned input class merges validation/consumer classes. Without a supplied nonblank ID, generate a stable per-instance ID. Derive helper/error IDs as `{inputId}-hint` and `{inputId}-error`; render a primed polite error region only when an EditContext is present, using its standard ValidationMessage. Merge/deduplicate caller `aria-describedby` with the owned IDs. Label-based naming overrides conflicting ARIA naming attributes; enforced disabled, blank placeholder, value binding, and validation-invalid state cannot be negated by unmatched attributes. Do not add separate page-level validation markup for this component. Sign-in's error ID is now `signin-email-error`.

Inferred interaction/accessibility decisions, not measured Figma states:

- A 240ms `cubic-bezier(0.4, 0, 0.2, 1)` position/font-size/color/background transition, in both directions. Honor `prefers-reduced-motion` with no transition or delay. Figma has no prototype reactions/timing for these nodes; batch 18 refines the original 160ms implementation, and batch 19 removes visible example placeholders and their fades.
- Invalid fields retain inline validation and a full error-token outline/label. Disabled labels use text-disabled; native disabled behavior remains intact. Known small-text contrast limitations of the approved palette remain open.
- Forced colors use system-color outlines because box shadows may be suppressed. Inner inputs retain no extra focus rectangle; focus belongs to the whole control.
- `_theme.scss` owns field geometry/motion tokens; `_native-field.scss` consumes them and direct Figma color/typography tokens. Sign-in uses `Email_Label` for the floating label. Existing placeholder resources remain for unmigrated consumers, not the native text-field API.

- Give each field a stable unique ID and a real `<label for="...">`, or another explicit accessible-name association when a visible label is intentionally absent.
- Placeholders are not labels. The unused legacy `ShopFieldLabel` was deleted (batch 32); visible labels belong to the field component, as in `ShopTextField`.
- Associate hints/errors with `aria-describedby`, and invalid inputs with `aria-invalid`.
- Preserve required semantics and error messages. A required boolean confirmation must require `true`; merely decorating a non-nullable bool as required is insufficient.
- Decide native browser validation versus custom validation consistently. If using `novalidate` to avoid duplicate browser bubbles, preserve required semantics and equivalent accessible errors.
- Retain invalid entered values where users need to correct them. Do not silently convert invalid money/date input to zero or today.
- Ensure Enter submits once, non-submit buttons do not submit, busy prevents duplicates, and form-level errors are discoverable.
- Preserve navigation-away/dirty-form warnings where currently implemented.

### 7.4 Specialized inputs

**Numeric and money fields — batch 37:** `ShopNumericField` derives from `InputBase<decimal?>` and owns plain invariant decimal parsing, draft text, optional inclusive `Min`/`Max`, formatting, labels, associated errors, and disabled behavior. `ShopMoneyField` composes that editor with a zero minimum and `CurrencyFormatter.Format` for Canadian-English CAD display. Both use the existing outlined field styles without vendor CSS. No generic numeric-type framework or new rounding rule.

- Idle fields display formatted amounts such as `$1,234.50`; focused fields edit `1234.5`. Input updates the draft. Enter commits while retaining plain-number editing; blur commits and restores display formatting. Escape restores the committed number. Invalid text stays visible without replacing the committed amount. Currency symbols, grouping separators and exponent notation are display-only or unsupported input, not silently stripped.
- Empty optional money fields commit `null`; required fields retain the previous value and report the supplied `RequiredError`. Zero remains valid. Formatting to two decimal places does not round the bound amount. Negative values are valid for an unbounded numeric field and invalid for money.
- `Value`, `ValueChanged`, and `ValueExpression` identify the actual decimal model field. Use `@bind-Value`, or supply all three explicitly. `Label` owns the accessible name; label-less table editors supply `aria-label`. Lowercase class/style, ID, name and description attributes reach the real input. Enforced type, input mode, disabled state, event handlers and validation attributes cannot be replaced through attribute splatting.
- Native form submit handlers await `ValidateAsync()` before `EditContext.Validate()` or dispatch. It commits pending valid edits and awaits consumer callbacks; `EditContext` receives field notifications and parsing errors. External errors join the associated validation messages. The mixed `ProductForm` explicitly validates both money fields and the variant card before its remaining `MudForm` validation; prices no longer depend on Mud registration.
- `ShopRangeSlider` composes two numeric editors and retains decimal step snapping, ordered bounds, thumb behavior and one atomic range callback. Empty range endpoints remain invalid. `ShopFilterPanel` retains group ownership, 300ms debounce and nullable/unbounded filter mapping. Currency formatting stays with the consumer.
- Virtualized variant rows own one `ShopNumericDraft` per price. Draft text and errors survive unmount/remount; submission validates retained editors and awaits callbacks. Copy-to-all resets overwritten row drafts. Keep the virtualized Mud table and unrelated legacy controls until their separate migration.
- `shopNumericField.js` installs one document-level Enter-default guard for numeric inputs. Enter commits the field without implicitly submitting its surrounding form; Tab and other keys keep native behavior. Blazor owns text, parsing and callbacks. The listener holds no component references.

**Date field — batch 39:** `ShopDateField` specializes Blazor `InputDate<DateOnly?>` with native `type="date"`, shared outlined field chrome and an always-floated label. `_date-field.scss` keeps the label clear of the browser's empty date segments. Browser-owned calendar icon, popup, keyboard segments and displayed date format are intentional; no custom calendar, new package or JS module. Native appearance varies across browsers and cannot preserve Mud's `OpenTo.Year` or fixed visible `yyyy-MM-dd` format. The HTML value and bounds use invariant ISO dates.

- `Value`/`ValueChanged`/`ValueExpression` preserve built-in binding, parsing and field notifications. `Label` is required; `HelperText`, inclusive `Min`/`Max`, `Required`/`RequiredError`, `Disabled`, and optional synchronous form-owned `Validation` are the added contract. Lowercase class/style, id/name/autocomplete and merged description IDs reach the actual input. Enforced type/bounds/disabled/event/naming attributes win over unmatched attributes.
- Required and bounds checks participate in `EditContext.Validate()`; parsing errors retain the previous bound value and block submission. Empty input binds null. An external changed value clears an invalid draft. Removal unregisters validation and clears owned messages. `Validate()` supplies the same checks to legacy forms outside `EditForm`.
- Signup now binds `DateOnly?` directly, explicitly checks `ShopDateField.Validate()` before remaining `MudForm` validation, and retains the 19-year cutoff, editable entry, required age confirmation, busy-state freeze, OTP command/state mapping and navigation. Age/business validation remains with the form/Application/Domain, not inside the shared field. No timezone conversion. Other signup fields and MudForm remain a temporary bridge.
- No time-picker consumer exists yet. Add a focused `ShopTimeField` when required; introduce a focused library behind the Shop API only if a custom popup becomes an approved requirement.

**OTP:** keep string representation so leading zeroes survive. Preserve paste, digit replacement, repeated digits, backspace, arrow movement, autofocus policy, disabled state, completion callback, resend behavior, and cleanup. Prefer text inputs with numeric input hints over treating the entire OTP as a number. Re-evaluate existing JavaScript interception because it contains Mud-specific workarounds; do not preserve competing DOM writes and Blazor binding accidentally.

### 7.5 Checkbox contract — Figma 2954:18826

Use `Components/Common/ShopCheckbox.razor` and `.razor.cs`, with `_checkbox.scss` loaded after `_icon.scss`. It derives from Blazor's `InputCheckbox`, retaining `InputBase<bool>` binding, field notifications, validation classes, and disposal. It replaces presentation only; no separate validation framework or checkbox JS module.

Inspected [Figma Checkbox section 2954:18826](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2954-18826), component set `2031:9937`, and all six size/state variants. Checked/unchecked node pairs: Small `2031:9936`/`2031:9938`, Medium `2031:9945`/`2031:9950`, Large `2031:9964`/`2031:9971`.

| Size | Icon viewport | Padding per side | Control target |
|---|---|---|---|
| Small | 20px | 12px | 44px |
| Medium (API default) | 24px | 12px | 48px |
| Large | 36px | 12px | 60px |

- Reuse `ShopIcons.Outlined.Checkbox_Check` / `Checkbox_Unchecked` as decorative SVGs. Their 24-unit view box contains the 16-unit outline. Figma keeps stroke width at 2px in all sizes; `vector-effect: non-scaling-stroke` preserves it. No filled box, new asset, or icon suite.
- Checked icon: `--shop-color-primary`; unchecked: `--shop-color-secondary`; label: `--shop-color-text-primary`. Unchecked label uses Body 1 (16px/400/AUTO/0.25px); checked uses the matching Subtitle 1 weight (500). No gap beyond the control's 12px trailing padding. Scale dimensions in rem through local properties on `.shop-checkbox` in `_checkbox.scss`; label text wraps rather than forcing fixed row height.
- API: required `Label`, `Value`/`ValueChanged`/`ValueExpression` inherited from the built-in control, `Size=ShopSize.Medium`, `Disabled=false`. No `Color`, `Variant`, tri-state, or indeterminate option is introduced: this inspected design and current migrated consumers are two-state only. Use `@bind-Value` normally; callback-only callers also supply `ValueExpression`.
- Native `id`, `name`, `required`, lowercase `class`/`style`, `data-*`, and description attributes target the real checkbox input. Omitted ID gets a stable per-instance ID; explicit IDs must be unique. The visible label owns the accessible name, native checked state owns checkbox semantics, and conflicting ARIA role/name/checked overrides are removed. Enforced input type, binding, and disabled state win over unmatched attributes. The handler also rejects synthetic changes while disabled.
- Inside an `EditContext`, the component renders its associated `{inputId}-error` validation region and merges that ID with caller descriptions. The form owns validation rules. HTML `required` does not itself add a required-true rule to `EditContext`; use an appropriate validator for consent. Do not replace a required Mud checkbox inside `MudForm` without migrating that validation responsibility too.
- Native pointer/label activation, Tab/Space behavior, and checked state remain browser-owned; decorative icons never receive focus. No animation is added. Keyboard focus uses existing focus tokens. Disabled text/icons use `--shop-color-text-disabled`; errors use `--shop-color-error` plus validation text. Forced colors shows the actual platform checkbox and focus ring instead of SVG chrome. These focus/disabled/error/high-contrast treatments are implementation decisions, not supplied Figma states. Existing error-text contrast limitations remain.

```razor
<ShopCheckbox Label="@Strings.SignUp_AgeConfirm"
              @bind-Value="Model.AgeConfirmed"
              Size="ShopSize.Medium"
              Disabled="@busy" />
```

This illustrates a future native form with its own required-true validation; it is not permission to remove Sign-up's current MudForm validation. Batch 20 converts the two `ShopFilterPanel` option branches, preserving callbacks, parent-owned selections, and the existing single-select ability to uncheck/clear. It does not change that interaction to radios. Sign-up's required age-confirmation checkbox and vendor-generated table selection remain temporary bridges for their owning form/table migration. Filter panel expansion/range/layout controls remain legacy; do not remove their providers yet.

### 7.6 Select contract — Figma 2959:19055

Use `Components/Common/ShopSelect.razor` / `.razor.cs`, `ShopSelectOption<TValue>`, `Styles/components/_select.scss`, and `wwwroot/js/shopSelect.js`. This is an outlined **single-select, non-editable** field, not a disabled `ShopTextField`, autocomplete, or multi-select. A custom select-only combobox is justified here because the approved option/checkmark treatment cannot be reliably applied to platform-native select popups across browsers. Do not turn it into a general menu framework.

Inspected [Figma Select section 2959:19055](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2959-19055), set `2960:19240`, collapsed empty/selected `2960:19170` / `2960:19178`, expanded empty/selected `2960:19188` / `2960:19213`, and option set `2960:19169`. Live screenshot and geometry read on 2026-10-03; no Figma changes.

- Field: parent-controlled width (300px in the specimen), 61px minimum height, 14px inline and 18.5px block padding, 1px input-line outline, square corners, 24px caret. Reuses the measured field geometry tokens; does not reuse editable input behavior. Value text uses Body 1, primary text color. One real label is centered while empty and floats to 12px Caption size only when an option matches the value. Figma calls this label “Placeholder”; there is no editable placeholder API or free-text input. Empty expanded state keeps the centered label, as drawn.
- Popup: white/default surface, 1px inside input-line border on left/right/bottom and no top border (the field supplies that seam), 1px inline/bottom padding, no shadow or radius. Figma's 300px popup contains 296px rows and measures 178px for four 44px options. Rows: minimum 44px, 12px inline / 10px block padding, 10px gap. Default Body 1 weight 400; selected weight 500, subtle surface through `--shop-color-surface-subtle`, and decorative 18px `ShopIcons.Outlined.Check` retaining its 2px vector stroke. Row text may wrap; 44px is not a clipping height. Select-specific geometry lives on `.shop-select` in `_select.scss`; field dimensions, color and typography use their existing owners. When viewport positioning flips the menu above, the shared seam moves to its bottom; this flipped treatment is inferred, not a supplied variant.
- API: required resource-backed `Label` and ordered `Options`, inherited `Value` / `ValueChanged` / `ValueExpression`, optional `HelperText`, and `Disabled`. Each `ShopSelectOption<TValue>` has a typed `Value`, already-localized `Label`, and optional `Disabled`. Values must be unique under `EqualityComparer<TValue>.Default`. Use nullable values for an empty selection; an explicit null-valued option can represent a clear/none choice. No Size, Color, Variant, search, clear-button, grouping or multi-select parameters are introduced.
- `InputBase<TValue>` owns typed binding, field notifications, validation classes and associated error messages. Selection maps an opaque stable option ID to its typed value; there is no culture-sensitive text parsing. Reordering preserves identity. Removed/unavailable options and disabled/disposed callbacks cannot change the value. If a supplied value has no matching option, show the empty label without silently clearing the model; the owning form decides whether that value is valid.
- Native `id`, `name`, lowercase `class`/`style`, `data-*` and relevant ARIA attributes target the actual `button[role=combobox]`. IDs are stable/generated unless supplied by the caller. The real label supplies the accessible name. Hint/error IDs merge with caller descriptions; validation-invalid overrides a caller's false value. The control enforces button type, role, non-editability, disabled state, identity and interaction handlers. Use form validators for required selection; `required` on a button does not create an HTML or EditContext validation rule. `aria-required` may communicate the form's rule.
- Browser module owns only transient open/active-option state, focus, keyboard behavior and numeric viewport placement; Blazor owns committed value, selected label/checkmark and validation. It uses a manual native popover in the top layer, sized to the trigger and constrained/flipped to available viewport space. Application menu cap is 20rem, not a Figma measurement. Page scrolling dismisses the popup; scrolling its own list does not. No vendor popover provider or new dependency.
- Keyboard: Tab focuses/leaves; Space/Enter opens or commits; arrows and Home/End navigate enabled options; printable characters find option labels without editable text. Escape cancels pending navigation; Tab commits active choice and moves onward. Pointer selection commits, outside pointer/focus departure dismisses. Focus stays on the combobox with `aria-activedescendant`. Escape while open is consumed before the enclosing dialog; a subsequent Escape can dismiss the dialog. Listener/observer/reference cleanup handles disposal and removed DOM roots.
- Hover, active-option focus outline, disabled, error, forced-colors and reduced-motion treatments are accessibility implementation decisions, not additional Figma variants. Focus remains visible. Existing palette contrast limitations still apply; this does not constitute screen-reader certification. Target browsers must support the native Popover API.

```razor
<ShopSelect TValue="Guid?"
            Label="@Strings.AddProduct_CategoryLabel"
            Options="@CategoryOptions"
            @bind-Value="Model.CategoryId"
            Disabled="@busy" />
```

The snippet illustrates a future migrated owning form; use its actual resource/model names. `CategoryOptions` is an `IReadOnlyList<ShopSelectOption<Guid?>>` created from existing feature data, never fetched by the control. Batch 23 migrates `ShopSortSelect<TSort>` to this control while preserving localized option keys, enum values, callbacks and wrapper styling. ProductForm's category/brand MudSelect inputs remain until their owning validation migration; do not remove the remaining Mud providers/packages yet.

## 8. Dialogs, notifications, loading, and browser modules

### 8.1 Host lifetime

`ShopUiHost` composes `ShopDialogHost`, `ShopNotificationHost`, and the native `ShopLoadingOverlay`. It is mounted once in each mutually exclusive Main/Auth layout. Do not also mount duplicate hosts in `App` or every page. If layouts can nest, ensure only the outer owner mounts hosts.

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

Current Figma dialog chrome at `2946:14712` supplies **16px header/action padding and 24px body padding**, dividers, and H5 title styling (batch 22 supersedes the original uniform 24px sections). Width remains caller-owned: omitted/null/None fits content within viewport gutters, with the image picker explicitly choosing Small/600px. Confirmation copy/action ordering is retained from the application. The shared Text/Medium icon-button is 48px after batch 9 (previously 36px), making the header taller than Figma's smaller dialog close icon. This uses the inspected icon-button variant rather than bespoke dialog geometry. Backdrop opacity, destructive-action hover, Cancel-first focus, and responsive/overflow behavior are explicit implementation/accessibility decisions, not additional Figma tokens. Shared destructive button treatment belongs in `_button.scss`, not `_dialog.scss`. Runtime typography and colors consume the existing Figma tokens directly.

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
| Omitted / `null` | `None`: content-sized within viewport gutters |
| `ExtraSmall` | 444px |
| `Small` | 600px |
| `Medium` | 960px |
| `Large` | 1280px |
| `ExtraLarge` | 1920px |
| `ExtraExtraLarge` | 2560px |
| `None` | Content-sized; no named cap; viewport gutters remain |

The six named values live only in `tokens/_sizing.scss`, generating `--shop-max-width-{kebab-case-name}`. They are owner-approved sizes, not claimed Figma measurements or responsive breakpoints. The separate 500px fallback is removed. `MaxWidth` defaults to `ShopMaxWidth.None`; explicit `null` also resolves to None for compatibility. `_dialog.scss` uses `inline-size: fit-content` for None, with a viewport-gutter maximum; its backdrop-specific color is declared directly on `::backdrop`. Named width choices continue to consume these tokens and separate available width from the cap: `inline-size: calc(100% - 2 * var(--shop-space-4))`. With the current spacing scale, every choice retains 16px per-side gutters; `None` does not mean edge-to-edge. Confirmation omits `MaxWidth`; the owner's subsequent picker change explicitly selects `ShopMaxWidth.Small` (600px). Preserve that choice; the Figma 500px picker measurement is historical, not its current cap.

Only the open dialog uses column flex layout. Header and actions do not shrink or scroll; only `.shop-dialog-content` scrolls, using `min-block-size: 0` and `overflow: auto`. It is a named, keyboard-focusable region so text-only bodies can be scrolled without a mouse. The dialog itself clips overflow and respects the dynamic viewport height; short content remains content-sized. Do not apply `display: flex` unconditionally to a closed native dialog. Keep titles/actions concise enough to fit the viewport: fixed chrome taller than the entire viewport cannot leave useful body space. Preserve modal inertness, safe-action focus, Escape/backdrop/close cancellation, and focus restoration.

#### Variant-image picker ownership

- `VariantImageDialog.OnCompleted` is a typed `EventCallback<VariantImagePinResult?>`: null cancels; a nonnull result with null ImageId explicitly clears the pin. The picker dispatches completion once per mounted instance. Clicking the selected image toggles it off; Save remains available for an empty gallery.
- `ProductVariantsCard` snapshots the selected variant and shared option-value scope in private request state. A request-identity guard ignores stale callbacks after reopening, disables opening a second picker, and rejects saves when disabled, when the variant has disappeared, or when a returned image no longer belongs to the gallery. Only Save invokes the existing state callback. No command or database contract changes.
- Ownership ends when the card unmounts; there is no outstanding dialog-result task to strand. Normal dismissals return null. Existing `ShopDialog` teardown restores focus and releases scroll locking. If another native modal opens, the shared module notifies the previous owner before closing it, so a picker cannot remain logically open behind an unsaved-changes confirmation.
- Native buttons represent gallery tiles with `aria-pressed`, unique resource-backed names (`VariantImage_ImageLabel`), and visible selection/focus cues. Following the owner's Figma `2946:14712` clarification, the Apply To fieldset uses two Medium `ShopCheckbox` controls (batch 21 supersedes batch 10's radios). This Variant Only is selected initially. Selecting either scope deselects the other; activating the selected scope leaves it selected, so an ambiguous empty/both-scopes result cannot occur. Scope state remains owned by `VariantImageDialog`, not the shared checkbox. Both controls use Tab/Space instead of radio arrow-key navigation.
- `_variant-image.scss` owns picker layout and image-selection treatment, not shared checkbox/button styles. Current Figma `2946:14712` is a 600px-wide instance, matching `ShopMaxWidth.Small`, with 32px body gaps, 16px gallery gaps, and 8px label/name gap. Each tile is 120px square at the default text size, with a tertiary background and 10px inset around the parent-width-driven 100px `ShopImage` Thumbnail. Only the selected tile has a 1px primary inside border, painted by a border-box pseudo-element without layout shift. Its decorative `ShopIcons.Outlined.Check` is 18px, 3px from the top/right, equivalent to the Figma 24px corner slot; no circle, white icon background, or nested button. Picker dimensions live on `.shop-variant-image` in `_variant-image.scss` and scale in rem. Scope rows use 24px checkbox icons, 12px padding, 48px targets, and Body 1/Subtitle 1 labels owned by `_checkbox.scss`. The gallery wraps on narrow screens. The existing 48px close control remains an intentional difference from Figma's smaller close control. Shared image fallback, aspect ratio, and preset behavior remain unchanged.

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
- Figma source: [Snackbars, section 2947:15676](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2947-15676), component `2948:17575`, reinspected 2026-10-03. Use `--shop-color-primary` background, `--shop-color-primary-contrast` text/icon, `subtitle-2` typography (14px/500), 16px padding, 24px gap, square corners, and an 18px Close_MD icon. No border or shadow. The example is 225×50px; this is content-sized, not a fixed width/height for every message. This supersedes batch 14's historical 24px padding and 241×66px measurements.
- `_notification.scss` owns bottom-center placement, responsive wrapping, and overflow. `_notification.scss` owns the application 24rem width cap on `.shop-notification-host` and 18px Figma icon size on `.shop-notification`. `_theme.scss` retains the application-wide notification layer `1500` (raised from `1400`). The 16px viewport gutter and width cap are application choices, not measured Figma geometry. Native modal top-layer ordering still takes precedence over z-index.
- Dismiss chrome is a native button local to the notification, not a shared button color/variant override. A 24px hit target extends around the 18px icon footprint without changing Figma padding or gap. The focus ring uses primary contrast for visibility on the dark bar. Long copy and 200% text wrap; stacked notifications scroll within the viewport.
- Both `MudSnackbarProvider` mounts are removed. `AddMudServices`, remaining providers, and Mud `Severity` values in inline `MudAlert` markup remain for unconverted controls; do not remove those until their consumers migrate. The E2E page object's `Snackbar` property temporarily retains its name but now locates `[data-testid=notification]`.

Browser verification must run **headed** (visible browser), per owner instruction. Set `E2E_HEADLESS=0`; do not use headless runs for subsequent migration batches unless the owner changes this preference.

### 8.4 Loading

Retain `BusyState`, `BusyKeys`, and `BusyFor`. Replace only their visual children and overlay implementation. A small CSS spinner or skeleton is sufficient.

Sign-in keeps its inline spinner and `aria-busy`/disabled behavior. Its resource-backed `role="status"` message uses `.shop-visually-hidden` from `base/_accessibility.scss`: available to assistive technology, without a visible extra “Loading…” label beside Login.

Do not add competing per-page busy flags. Pass `BusyFor`'s value into `ShopButton.Loading` or `ShopIconButton.Loading`; the controls centralize spinner-only appearance, disabling, and hidden status text, not operation tracking. Keep form inputs and related controls bound to the same busy value. Component-local interaction state such as disclosure expansion is unrelated and can remain local. Preserve cleanup on success, failure, and cancellation. Verify global blocking behavior separately from inline button loading.

Native loader (batch 40): `ShopLoader` uses the geometry of Figma Loader `2996:28796` in section `2996:28794`: 48px outer diameter, 80% inner radius (4.8px stroke), rounded ends in primary color, and an owner-adjusted 80% arc (288° sweep, 72° gap before rounded caps). It is decorative (`aria-hidden=true`) and has no busy-state subscription, text, size/color enums, percentage, or JavaScript. Class, Style and unmatched attributes target its root. Caller SCSS may resize it or set `--shop-loader-color`; buttons inherit their foreground, image tiles use 32px, and the overlay uses 48px. Rotation at 0.8s is an implementation decision; reduced motion stops it, and forced colors use CanvasText. Buttons and image tiles retain their existing loading announcements.

The app-blocking `ShopLoadingOverlay` still observes only `BusyKeys.Global` and mounts once through `ShopUiHost`. Figma Overlay `2996:28795` confirms its 40% primary scrim and centered loader. It retains its always-mounted hidden status, `cursor: progress`, loading layer 2000, pointer blocking and native top-layer modal ordering. Keyboard focus behind it remains unchanged. Batch 40 replaces the historical CSS border spinner with the shared rounded SVG loader.

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

Use `ShopTable<TItem>` for shared semantic table rendering and optional controlled selection. Keep feature-specific cell templates and data operations in their owners; do not build a generic grid engine.

Preserve selection by stable entity ID, select-all scope, clearing selection after operations, partial bulk failures, reference-blocked deletions, row-specific errors, and keyboard access. Preserve current sort/filter/query-state behavior rather than replacing it with client-only filtering. Use proper headers and accessible checkbox labels. Give overflow containers usable keyboard/scroll behavior on narrow screens.

#### Native table contract (batch 26)

Design: [Figma Table section 2967:1518](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2967-1518), inspected 2026-10-04. Cell set `2574:7507`, default/header `2574:7501` / `2574:7504`, specimen `2574:7648`.

- `Components/Common/ShopTable.razor` / `.razor.cs` own native table/head/body/rows and a focusable, named horizontal scroll region. `Styles/components/_table.scss` owns presentation. No Th/Td/Tr wrappers or cell-type enum.
- `Items` is the current `IReadOnlyList<TItem>`. Required `ItemKey` returns a non-null, unique stable key with value equality (boxed Guid/int/string are supported). Duplicate/null keys fail clearly. Keyed rows retain child identity when reordered or refreshed.
- Required `Caption` supplies a visually hidden native caption and the scroll-region name. `HeaderContent` contains native `th scope="col"`; `RowTemplate` contains native `td` cells, not another tr. Required positive `ColumnCount` excludes the optional generated selection column. Optional `EmptyContent` spans the effective count.
- `Class`, `Style`, and unmatched attributes target the table. The surrounding scroll wrapper has owned semantics and geometry. Static presentation stays in SCSS. Width follows the parent; cells/rows grow for text, controls and errors. The specimen's 120px columns and 38px text rows are not fixed sizing constraints.
- Body 2 / Subtitle 2 typography; 10px block and 12px inline padding; primary text/default surface/default line tokens. Separate borders with zero spacing use one owner per 1px grid edge: every cell owns its inline-start and block-end, the last cell closes inline-end, and the first header row closes block-start. Body cells have no top border; the header owns the header/body divider. Header fill clips to the padding box rather than painting under border edges. Header component `2574:7504` now uses Figma `Brand/Tertiary` (`#e8e8e8`, rechecked 2026-10-04); consume `--shop-color-tertiary` directly. The former black-at-6% fill and `--shop-table-header-background` alias are removed.
- Add `shop-table-column-compact` to both the header and body cells of content-sized columns. It requests `inline-size: 1%` plus `white-space: nowrap`; intrinsic content still sets the minimum width (this is not a hard 1% cap or literal fit-content). All Status and Actions columns in the three admin tables use it; the legacy variant Status column shares the class as a temporary bridge. Other columns consume the remaining space. Keep this opt-in class rather than adding width parameters or column-wrapper components.
- Optional `Selectable`, `SelectedKeys`, `SelectedKeysChanged`, `RowSelectionLabel`, and `SelectionDisabled` enable checkbox-only selection. The parent commits the emitted fresh `IReadOnlySet<object>`; the component never mutates its input set. Select-all touches only displayed keys and preserves off-page keys. No row-click selection. Disabled and removed-row callbacks are guarded.
- `ShopCheckbox.Indeterminate` is presentation over the existing boolean value, not nullable/tri-state form data. The parent derives partial selection; `HideLabel` keeps the real label accessible without visible cell text. Existing `ShopIcons.Outlined.Add_Minus_Square` supplies the inferred mixed-state glyph (not measured from the table design). A lazy, disposed `shopCheckbox.js` module synchronizes the native `indeterminate` DOM property; ARIA mixed state and forced-colors native presentation agree. Ordinary two-state checkboxes do not import the module.
- Horizontal overflow, keyboard-focusable scroll region, hidden captions, mixed selection and forced-colors treatment are implementation accessibility decisions, not Figma variants. Do not hide columns or convert rows into cards without an approved design.
- Search/sort/filter/query state, fetching, authorization, BusyState, confirmations, row mutations and `ShopPagination` remain page-owned. The three admin lists project existing selected DTO IDs into table keys; refreshes do not lose checked state through changed DTO equality. Existing clearing/blocked-deletion behavior stays with each page.
- Batch 26 converts Brands, Categories and Products table shells/selection only. Their remaining Mud cell content, actions and page layouts remain temporary bridges; batch 36 replaced the loading skeletons with `ShopTable.Loading`. `ProductVariantsCard` still uses MudTable with virtualization; batch 37 migrated its money fields while preserving row drafts and save validation. Migrate the remaining table separately with explicit virtualization/editing/validation preservation proof. Do not remove Mud packages/providers/assets yet.

```razor
<ShopTable TItem="BrandListItemDto"
           Items="@_brands.Items"
           ItemKey="@(brand => brand.Id)"
           Caption="@Strings.ManageBrands_Heading"
           ColumnCount="2">
    <HeaderContent>
        <th scope="col">@Strings.ManageBrands_ColumnName</th>
        <th scope="col">@Strings.ManageBrands_ColumnDescription</th>
    </HeaderContent>
    <RowTemplate Context="brand">
        <td>@brand.Name</td>
        <td>@brand.Description</td>
    </RowTemplate>
    <EmptyContent>@Strings.ManageBrands_EmptyTitle</EmptyContent>
</ShopTable>
```

Rollback: revert the batch's native table call sites and their selection adapters together, restoring the previous MudTable markup/tests. No schema, stored data, query contract or package removal is involved. Never discard unrelated working-tree changes.

### Filters, sorting, pagination, and breadcrumbs

- Keep `ShopSortSelect` typed enum/option contracts and localized labels.
- Preserve `ShopFilterPanel` single-select, multi-select, clear, and range behavior. A single-select group should have intentional radio/clear semantics; do not accidentally change its clear behavior.
- `MudRangeSlider` has two thumbs; a single native range input is not equivalent. Keep both ends, prevent crossing, and avoid duplicate/debounced query dispatch changes without verification.
- Keep catalogue and admin query-state records and `QueryStatePageBase`. Verify Back/Forward and cancellation of superseded loads.
- Preserve `ShopPagination` one-based `Page`, `TotalPages`, and `PageChanged`. Test zero results, one page, boundaries, and changing totals.
- Batch 27 replaces Mud `BreadcrumbItem` in both `BreadcrumbTrail` and `BreadcrumbState` with `Common/UI/ShopBreadcrumbItem`. Native `ShopBreadcrumbs` uses CSS-responsive ancestor disclosure; preserve every intermediate destination and current-page semantics. See section 6.6.

### Menus and navigation

`ProfileDrawer` replaces the old `ProfileMenu` dropdown. MainLayout owns its open state and mounts it outside the scrolling shell, only for authenticated users. ShopAppBar emits `OnAccountClick` and reflects `AccountOpen`; it does not own profile data or a drawer instance. AuthLayout has no profile drawer. Keep ordinary anchors and a logout button, not an ARIA application menu. Preserve authorization-controlled destinations, sign-out, and accessible account labels. The identity block links to My Profile so the existing destination remains available without adding an extra row to the Figma design.

### Drawer and layer ownership

`ShopDrawer` is a controlled reusable modal with `Open` / `OpenChanged` and three fragments: `HeaderContent`, `DrawerContent`, `ActionContent`. HeaderContent supplies a localized semantic heading; the component supplies the close button and unique accessible naming. Only DrawerContent scrolls. ActionContent is optional and an omitted fragment reserves no space. Keep the instance mounted while closing so the transition can finish; removal/disposal cleans up immediately. Page-specific drawers may remain page-owned; do not introduce a global drawer service without another actual need.

Figma references inspected 2026-10-04: section `2976:16449`, component `2976:16454`, account screen `2986:26176`. Desktop width is 400px; height uses `100dvh`. Header/action padding is 16px, with 8px gaps and one shared edge divider. Header uses H5 uppercase and the Small/Text close icon button (24px target, 18px icon). Content has no implicit padding. Account identity uses 24px padding, H5 name and Subtitle 1 email; links use H6 with 16px × 24px padding and an 18px trailing arrow. Profile content omits the footer, matching the inspected visible account design; logout remains in its navigation list.

Implementation decisions, not additional measured Figma states: full viewport width below 600px, 280ms slide/backdrop fade, reduced-motion bypass, focus-visible styling, safe-area footer inset, long-content wrapping and native modal focus behavior. The backdrop uses the primary color at 40%, covering the appbar as well as page content. Escape, the close button, and pointer sequences that start and end on the backdrop request `OpenChanged(false)`. Navigation closes the profile drawer. The shared `shopDialog.js` lifecycle handles focus restoration, disposal, animation reversal and one-active-modal replacement with existing ShopDialog. Exit transitions retain browser inertness until complete. A narrowly scoped overflow override temporarily beats the shell's legacy Mud overflow utility while the drawer is modal.

`Styles/tokens/_layers.scss` owns shared document layers: sticky 1000, appbar 1100, notification 1500, loading 2000. These values moved out of `_theme.scss` without changing the existing sticky/notification values. Components consume the shared tokens; drawer width, padding and motion remain in `_drawer.scss`. Native modal dialogs/drawers use the browser top layer, not numeric z-index; do not add ineffective drawer/dialog tokens or claim a high notification number can overtake a native modal. Existing unmigrated vendor popovers retain their legacy stacking until migrated; the native loading overlay uses `--shop-layer-loading` (batch 31); this batch does not remove providers or create a duplicate C# layer scale.

### Images

Keep the eight `ShopImagePreset` treatments (`SquareContain`, `SquareCover`, `Banner`, `Hero`, `PortraitCover`, `Editorial`, `BrandLogo`, `SocialSharing`), including presets whose eventual pages are absent. Presets are named by geometry and fit, not by use case; reuse one for any matching placement. Preserve contain/cover, aspect ratios, source changes, mobile artwork selection, meaningful versus decorative alternatives, and stable fallback geometry.

Native image error events may simplify the current observer workaround. Verify late failures from an obsolete source cannot replace a newer valid image. Avoid downloading duplicate desktop/mobile artwork unnecessarily where a compatible `<picture>` design can preserve behavior. Lazy-load offscreen images; do not automatically lazy-load the primary above-the-fold image. Reserve geometry and verify broken-image behavior in the browser.

### Uploads

Native composition — batch 38:

| Component | Owns |
|---|---|
| `ShopFileDropzone` | Native `InputFile`, browse/keyboard activation, file drops, drag-over treatment, awaited batch callback, reset for same-file reselection. No retained files or image validation. |
| `ShopImageTile` | `ShopImage` presentation, primary badge, optional selected state, loader, error, remove affordance and controlled callbacks. No URL ownership or collection state. |
| `ShopImageUpload` | Ordered `ShopUploadedImage` selection, file validation/read limits, preview URLs, count limit, removal, primary allocation, reordering and `FilesChanged`. |
| Owning form | Echoes `FilesChanged`, waits for `WaitForPendingFilesAsync()` before Save, maps valid selections into existing commands, persists uploads/deletions. |

Defaults remain 10 entries and 2 MiB per file. ProductForm keeps its explicit unlimited-count override; brand/category forms keep single-selection replacement and their preview presets. Stored entries retain `ExistingId`; moves retain `ClientId` and byte-array identity. No backend contract changes.

Figma source: [Image Upload section](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=3013-17940), inspected 2026-10-08. Dropzone set `3013:17971` uses a 448×200 default frame, 32px/24px padding, 24px icon, Subtitle 1 title and Caption hint. Tile set `3013:18188` uses 144px squares, tertiary background, whole-image fitting, 12px primary-badge inset and 8px remove inset. The grid has three equal columns and 8px gaps; its first valid product image spans two columns and rows. Width adapts to the container; responsive behaviour is inferred from this geometry.

- Idle desktop removal is hidden. Hover/focus reveals it; touch and validation-error tiles keep it visible. `Removable=false` hides it in every state. Loading/disabled blocks all actions. Removal promotes the next valid image; removing the last restores the empty dropzone.
- Updated Figma hover treatment: the normal tile's 4px bottom bar uses Primary (`3016:20008`); Error Hover retains Error (`3013:18394`).
- The uploader has no visible heading, per the approved follow-up. `PreviewAlt` still names the image list and persisted-image controls accessibly.
- Dragging reorders existing entries. Focus/tap selects the image for visible **Move earlier / Move later** actions; Alt+Left/Right also moves it. Live status announces movement/removal and focus follows the item. The shared action row and shortcut are accessibility adaptations, not Figma-measured elements.
- `ShopImageTile.Selected` is nullable: null means ordinary activation, true/false means a toggle with `aria-pressed`. `Primary` is independent. The standalone tile can be used without removal or uploader state.
- Processing shows a loader with an accessible loading name; no percentage, processing copy, skeleton row or filename caption. Filenames identify image/remove controls accessibly. Validation failures remain visible as error tiles.
- Class/Style/unmatched attributes target each component's outer frame; native input IDs and enforced disabled attributes remain internal. Dropzone `ChildContent` can provide its own `data-file-picker` button. Its consumer must finish reading files inside `FilesSelected` before returning.
- Removed legacy `PreviewSize`, `DropzoneHeight` and `FileSizeFormat` parameters; SCSS now owns tile geometry. All repository callers migrated. `Files`, `FilesChanged`, validation overrides and save-time ownership remain.

- Read selected `IBrowserFile` streams before resetting/replacing their underlying input; references to files from a replaced selection may become invalid.
- Keep explicit `OpenReadStream` limits and do not trust `accept` or browser-reported MIME type as the sole security check.
- Reject type/size failures before buffering. Read/preview failures become removable errors. Count overflow is reported; excess files are not read. Server validation remains authoritative.
- Revoke owned object URLs on removal, replacement, external reset and disposal; never revoke borrowed stored URLs. Disposal cancels preparation. An external selection reset wins over an in-flight batch. No large base64 strings in component state.
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
| P1 Foundation | In progress | Scoped tokens/base SCSS, ShopComponentBase, ShopCssClass, ShopButton, ShopIconButton, ShopIcon, label-only ShopTextField, two-state ShopCheckbox; canonical native guidance | Implemented subset passes build, component tests, and focused browser checks. Remaining services/controls are not implied complete. |
| P2 First slice | In progress | ProductCard, ShopImage, SignIn markup/form, native confirmations and variant-image picker | Existing callbacks, image behavior, validation, dialog results, and BusyState preserved and tested. SignIn still uses the snackbar bridge; this is not a fully vendor-free route yet. |
| P3 Shell/services | In progress | Native dialogs, notification service/host and loading overlay; dialog/snackbar providers removed | Next: MainLayout/AuthLayout shell presentation. Other Mud providers still have consumers. |
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

### Batch 17 — Outlined-only field and animated floating label — 2026-10-03

- Inspected Figma section `2950:17591`, set `170:136`, all three Placeholder/Normal/Focused variants and a live screenshot. The set has no filled variant and no prototype timing. Section 7.3 records exact node IDs, measurements, token mapping, and inferred accessibility/motion states.
- Replaced native field fill/bottom-line styling with a transparent surface and full inset outline (1px idle / 2px focused). Moved Sign-in's real associated label into the field shell; it animates from Body 1 inside the empty field to Caption across the top outline on focus, remains floated with a value, and returns inside only when empty and blurred. No `Variant` parameter, new field wrapper component, or Blazor focus flag was introduced.
- Existing `ShopTextInput` binding, EditContext validation, ID/type/name/autocomplete/required/ARIA contracts, BusyFor state, button loading, and submission behavior remain intact. The example placeholder is shown only for a focused empty field. Geometry/motion live in `_theme.scss`; presentation lives in `_native-field.scss`. Legacy Mud forms remain outside this batch.
- Verification: **975 Web tests passed**; E2E build succeeded; **12 headed browser checks passed** with `E2E_HEADLESS=0`, filter `FullyQualifiedName~NativeUiJourneyTests`, result `outlined-field-headed.trx`. The two added cases check 390px/1440px layouts, vendor-free styles, measured 61px height/label offsets/font/colors/outlines, active 160ms transitions, focus/blur/clear, retained values, native value restoration, invalid states, reduced motion, forced colors, accessible names, and 200% root-text scaling. Existing button loading/geometry/icon-focus tests also pass. Backend traffic was mocked or blocked; no OTP was sent.
- Reviewed screenshots `outlined-field-390-placeholder.png`, `outlined-field-390-focused-empty.png`, and `outlined-field-1440-normal.png` under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/`. They match the inspected field state treatment; the initial page-heading focus ring is existing route-focus behavior, not a field outline regression. No screen-reader certification or real credential-manager autofill test is claimed; restored native values were checked without focus flags.
- Existing AngleSharp NU1902 and unrelated E2E xUnit1051 warnings remain. Approved palette contrast limitations remain documented. Roll back only this batch's Sign-in label position, native-field styles/theme tokens, tests, and docs together; preserve earlier loading/focus fixes. Next broad migration scope remains the native loading overlay.

### Batch 18 — Smoother floating-label motion — 2026-10-03

- Refined batch 17's 160ms transition to 240ms with `cubic-bezier(0.4, 0, 0.2, 1)`. Position, size, text color, and label background now ease together in both directions; the surface mask no longer appears/disappears instantly. Focused example text fades in over the second half of the transition and fades out without delay on blur. Reduced-motion rules disable transitions and delays.
- Kept the same outlined-field geometry, end-state typography/colors, label association, validation, and native value behavior. Only motion tokens/SCSS, the motion regression test, and guide changed in this refinement.
- E2E build, design gate, and whitespace checks passed. **6 headed Sign-in checks passed** (`field-smooth-transition-headed.trx`, `E2E_HEADLESS=0`, filter `FullyQualifiedName~NativeUiJourneyTests.SignIn_`). Motion tests sample the actual reverse transition at 120ms and require intermediate vertical/horizontal positions, font size, and background alpha; they also verify active forward transitions, endpoint states, responsive geometry, and reduced motion. No backend writes. The 975-test Web-suite result belongs to batch 17; it was not rerun for this SCSS-only behavior adjustment. Existing unrelated analyzer warnings remain.
- Rollback only the duration/easing token changes, background/placeholder transition changes, amended motion assertions, and this documentation. Keep batch 17's outlined/floating-label implementation intact.

### Batch 19 — Label-only ShopTextField component — 2026-10-03

- Replaced `ShopTextInput.razor`/code-behind with `ShopTextField.razor`/code-behind and migrated its sole native consumer, Sign-in. The owner explicitly requested this extraction. The component owns the outlined shell, required floating `Label`, optional `StartIcon`/`HelperText`, stable generated IDs, and inline validation messages. Native attributes, `class`, and `style` target the actual input. No `Variant` or `Placeholder` API, duplicate busy store, JavaScript focus tracking, or new framework.
- Removed visible example placeholders. An enforced single-space internal placeholder supports CSS empty-state detection only; the real associated label supplies the accessible name. Preserved 240ms eased label motion, populated/restored-value states, reduced motion, forced colors, and existing geometry. Helper text uses the existing caption/text-secondary tokens.
- Preserved `InputBase<string?>` binding/parsing/field notifications and immediate `oninput` updates. The form still owns validation rules, submission, and `BusyFor`. Disabled inputs reject synthetic updates. Caller attributes cannot replace the label's accessible name or negate enforced disabled/validation state; caller description IDs merge with component-owned hint/error IDs without duplicates. Sign-in's error ID intentionally changes from `signin-error` to `signin-email-error`.
- Added seven component test cases for IDs, native attributes, labels, hints, binding, disabled updates, EditContext validation/recovery, and required-label enforcement. Updated Sign-in assertions and added a headed two-field specimen covering independent generated IDs/descriptions, optional icons, label activation, and disabled populated fields. Updated section 7.3 and canonical component/page examples. Legacy forms and resource keys remain unchanged.
- Verification: baseline **12 Sign-in tests passed**; after extraction **19 focused tests passed**; full Web suite **982 passed / 0 failed / 0 skipped**. E2E project build succeeded. **13 headed browser checks passed / 0 failed / 0 skipped**, with `E2E_HEADLESS=0`, filter `FullyQualifiedName~NativeUiJourneyTests`, and result `tests/TheShop.E2E.Tests/TestResults/shop-text-field-headed.trx`. Backend requests were mocked or blocked; no OTP was sent.
- Visually reviewed `text-fields-label-only.png` and `outlined-field-390-focused-empty.png` under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/`: only associated labels render, with no example placeholder; outlined focus, icon placement, and helper text remain intact. Browser coverage also preserves existing button loading/focus and responsive field checks. No screen-reader certification, real credential-manager autofill, or authenticated/backend-writing journey is claimed. Existing unrelated analyzer/package warnings remain.
- Design-rule and whitespace checks passed. The temporary agent-owned server was stopped after headed verification. The knowledge graph was refreshed; existing empty-file/SQL-parser/community-label warnings remain. Final self-review also adopted the project's collection-expression syntax without changing field behavior; the Web suite was rerun afterward.
- Rollback only this batch's component replacement, Sign-in substitution, placeholder/helper SCSS, corresponding tests, and documentation together; restore the old primitive and manual field composition from Git. Preserve earlier motion/loading/focus changes and all unrelated work. Next broad migration scope remains the native loading overlay.

### Batch 20 — Figma checkbox and filter option migration — 2026-10-03

Scope: reusable checkbox plus `ShopFilterPanel`'s multi-select and clearable single-select option branches. Branch `refactor/ui-refactoring`, starting commit `a582660`; preserved all uncommitted batch-17/18/19 field/component/test/documentation changes. Used constitution, graph lookup, and safe-refactor boundaries; no SDD-stage artifacts, business/data changes, new dependencies, commit, or deployment.

- Inspected Figma section `2954:18826`, component set `2031:9937`, all six size/state variants, vector geometry, bound text/paint styles, and screenshots. Section 7.5 records measurements, public API, inferred accessibility states, and remaining bridges. No Figma document changes.
- Added `ShopCheckbox.razor`/code-behind and `_checkbox.scss`; added four checkbox geometry tokens to `_theme.scss` and the SCSS entry-point import. Reused existing `ShopSize`, `ShopCssClass`, `ShopIcon`, and checked/unchecked `ShopIcons` fragments. No copied SVG asset, filled treatment, color/variant enum expansion, or JS module. `InputCheckbox` specialization retains true-valued form submission, input element reference, bool binding, EditContext notification/validation/disposal; it guards disabled changes and enforces native checkbox semantics.
- Replaced both filter option branches without changing filter/range callbacks, state ownership, clear behavior, or backend contracts. Stable option keys retain input identity when options reorder. Native label typography intentionally changes the old 14px filter labels to Figma's 16px Body 1 / Subtitle 1 treatment. Existing single-select options remain uncheckable to clear; no radio-interaction change. Required age confirmation remains in Sign-up's MudForm until its form migration preserves required-true validation; table-generated selection checkboxes and remaining filter chrome/ranges are also outside this batch.
- Added 10 component test cases and one filter identity/reset/reorder case; converted filter checkbox assertions to native input events/state. Baseline **21 filter tests passed**. Initial focused component/filter run **31 passed**; full final Web suite after all code changes **993 passed / 0 failed / 0 skipped** (`dotnet test tests/TheShop.Web.Tests/TheShop.Web.Tests.csproj --no-restore --nologo -m:1 -p:UseSharedCompilation=false`). E2E project build succeeded; design-rule and whitespace checks passed.
- Added two headed checkbox cases covering all six size/state specimens, 390px/1440px viewports, with/without vendor and template CSS, measured targets/icons/padding/typography/colors/stroke behavior, stable accessible names, label/icon-coordinate activation, Tab/Space, disabled clicks/skipped tab stops, forced colors, and 200% text wrapping. Initial run: **13 existing checks passed / 2 new checks failed** because Playwright intentionally refuses label clicks for disabled inputs. Changed that assertion to a real mouse-coordinate click; no product styling change was needed. Final complete rerun: **15 passed / 0 failed / 0 skipped**, with `E2E_HEADLESS=0`, filter `FullyQualifiedName~NativeCheckboxJourneyTests|FullyQualifiedName~NativeUiJourneyTests`, result `tests/TheShop.E2E.Tests/TestResults/checkbox-batch20-final-headed.trx`. The initial result remains `checkbox-batch20-headed.trx`.
- Reviewed `checkboxes-1440-figma.png`, `checkboxes-1440-keyboard.png`, `checkboxes-390-figma.png`, `checkboxes-390-forced-colors.png`, and `checkboxes-390-large-text.png` in `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/` against the inspected Figma screenshot. Checkbox browser specimens are actual component-rendered HTML exercising native browser/CSS behavior; C# callbacks, validation, filter state, and ordering are verified by bUnit. This does not claim authenticated catalogue/admin journeys, screen-reader certification, or native controls inside an already-converted Sign-up form. Backend traffic was mocked/blocked; no OTP or database writes. Temporary agent-owned servers were stopped; no Visual Studio process was terminated.
- `graphify update .` completed: **8,858 nodes / 22,595 edges**. Existing zero-node configuration files, missing SQL parser, and community-label fallback warnings remain. Existing AngleSharp NU1902 and unrelated E2E xUnit1051 warnings remain; no dependency work was added. Existing palette contrast limitations remain documented.

Rollback: reverse only this batch's checkbox files, checkbox token/import additions, the two filter substitutions, corresponding tests, section 7.5/canonical guidance, and execution record together. Restore Mud checkbox option markup from Git; preserve all earlier migration work. Next broad batch remains the native loading overlay, followed by owning form/shell migrations; migrate Sign-up's required checkbox as part of its validated form, not by dropping required-true enforcement.

### Batch 21 — Variant-image Apply To checkboxes — 2026-10-03

- Owner clarified that current Figma instance `2946:14712` uses checkboxes, not radios. Inspected its live tree and screenshot: scope instances `I2946:14712;2946:14560;2946:14750` and `I2946:14712;2946:14560;2946:14751` are unchecked/checked Medium checkboxes, 48px targets with 24px icons. No Figma document changes.
- Replaced the two scope radios in `VariantImageDialog.razor` with the existing `ShopCheckbox`, explicitly Medium, and removed `_scopeName` plus obsolete `.shop-variant-image-choice` radio styles. Shared checkbox SCSS owns all control appearance; picker SCSS retains layout only. This is a scoped design correction, not a dialog/gallery redesign. Preserved every pre-existing uncommitted migration change.
- Kept one dialog-owned scope flag, default This Variant Only, mutual exclusion, selected-scope reactivation retaining selection, hidden-scope reset, and unchanged `VariantImagePinResult`/caller fan-out behavior. Tab/Space replaces the old radio arrow-key interaction. Updated dialog/caller tests, the E2E page-object selector names/caller, and the picker browser assertions. Section 8.2 now supersedes batch 10's recorded radio deviation; historical execution records remain unchanged.
- Baseline dialog/caller tests: **22 passed**. Initial expanded focused run: **31 passed / 1 failed**, due solely to a caller test's stale radio-value selector; updated it to the native checkbox test ID and boolean change event. Final full Web suite: **993 passed / 0 failed / 0 skipped**. E2E project build succeeded; design/whitespace gates passed; no stale picker radio-value selectors or choice classes remain.
- Headed browser run: **4 passed / 0 failed / 0 skipped**, with `E2E_HEADLESS=0`, filter `FullyQualifiedName~NativeVariantImageJourneyTests|FullyQualifiedName~NativeCheckboxJourneyTests`, result `tests/TheShop.E2E.Tests/TestResults/variant-checkbox-batch21-headed.trx`. Covers desktop/mobile picker geometry, 48px/24px checkbox sizing, default scope, accessible names, Tab/Space/focus, modal dismissal/restoration, and shared checkbox states/forced colors/large text. Reviewed `variant-image-390.png` and `variant-image-1440.png` in `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/`. Their outer checkbox outline is deliberate keyboard focus, not resting chrome. Gallery specimens intentionally use image fallbacks; this patch does not claim a complete visual redesign of the dialog.
- Browser specimens use component-rendered HTML and production dialog JS; bUnit verifies actual Blazor exclusivity and caller updates. No authenticated backend-writing create-product journey, screen-reader certification, or backend mutation was run. Backend traffic was blocked. Temporary server was stopped. Graph refreshed (**8,859 nodes / 22,596 edges**); existing parser/community-label and package/analyzer warnings remain.
- Rollback only this batch's scope markup/field removal, obsolete-choice SCSS removal, related tests/page-object names, and guide updates together. Restore the earlier radio contract if reverting; preserve the shared checkbox and all preceding batches.

### Batch 22 — Image-selection treatment and dialog padding — 2026-10-03

- Reinspected live Figma `2946:14712` and its screenshot: 600×548 instance, header/actions 16px on every side, body 24px; 120px tertiary tiles containing 100px images inset 10px; selected-only 1px primary inside stroke; plain Check in an 18px icon footprint, inset 3px within a 24px top-right slot. No Figma document changes.
- `VariantImageDialog` now uses decorative `ShopIcons.Outlined.Check`, not Circle_Check. `_variant-image.scss` removes the white icon backing, applies the measured image inset/background/indicator geometry, and explicitly contains the selection border in its border box. Four picker geometry tokens live in `_theme.scss`. `ShopImage` remains parent-width-driven with the Thumbnail aspect ratio; no shared image implementation changes or nested interactive controls.
- Shared `_dialog.scss` now uses 16px header/action padding, retaining 24px body padding and body-only scrolling. Existing 48px shared close-button sizing remains unchanged and is still larger than this dialog's Figma close control. No full-dialog pixel-parity claim. Selection toggling, deselection/save, exclusive scope checkboxes, caller fan-out, keyboard focus, dismissal, and maximum-width choices are preserved.
- Focused baseline: **31 passed**. Final full Web suite: **993 passed / 0 failed / 0 skipped**. E2E build, design-rule gate, and whitespace checks passed. Added selected-icon/state assertions to bUnit and image inset, indicator geometry, inside-border color/placement, unselected border absence, and section-padding assertions to the browser tests.
- Headed browser result: **7 passed / 0 failed / 0 skipped**, `E2E_HEADLESS=0`, filter `FullyQualifiedName~NativeVariantImageJourneyTests|FullyQualifiedName~NativeDialogJourneyTests`, report `tests/TheShop.E2E.Tests/TestResults/variant-selection-batch22-headed.trx`. Covers mobile/desktop picker and confirmation dialogs, width choices through a 3000px viewport, keyboard activation/focus restoration, dismissal, wrapping, and fixed chrome/body scrolling at 200% text. Reviewed `variant-image-1440.png`, `variant-image-390.png`, and `confirmation-390.png` under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/` against the inspected Figma treatment. Image specimens intentionally use shared fallbacks; these are not production product-photo screenshots.
- Browser specimens use real component-rendered HTML plus production modal JS; bUnit covers live Blazor selection behavior. No backend-writing journey or screen-reader certification was run. The existing debug server was left untouched; after the owner stopped it, the temporary fresh-build server was started and cleaned up. Graph refreshed: **8,860 nodes / 22,597 edges**. Existing AngleSharp advisory, unrelated E2E analyzer warnings, zero-node configuration files, missing SQL parser, and fallback community labels remain unchanged limitations.
- Rollback only this batch's icon change, picker geometry tokens/styles, shared dialog padding, associated assertions, and guide updates together. Preserve batch 21's checkbox migration and all other pre-existing changes.

### Batch 23 — ShopSelect and sort-picker migration — 2026-10-03

- Added `ShopSelect<TValue>` / `ShopSelectOption<TValue>` with the non-editable Figma `2959:19055` selection contract described in section 7.6. Inspected all four collapsed/expanded empty/selected variants and both option variants. Final per-edge inspection confirmed the popup has no top stroke, 1px left/right/bottom inside strokes, and 1px inline/bottom padding. No Figma file edits.
- Component files own typed value mapping, stable option IDs, attribute precedence and InputBase validation. `_select.scss` owns appearance; `_theme.scss` adds only measured option geometry and the documented application popup-height cap. `shopSelect.js` owns transient keyboard/pointer/popover behavior and cleans up listeners/observers. No new package, shared base-class expansion, editable text, vendor dependency or generic menu framework.
- Migrated `ShopSortSelect<TSort>` from Mud to the new control and `ShopComponentBase`, preserving localized labels, enum contracts, parent callbacks and wrapper attributes/styles. Existing catalogue/admin sort consumers need no API changes. ProductForm's two validated MudSelect consumers remain intentionally untouched; owning form migration still pending.
- Preserved the pre-existing dirty/staged work for text fields, checkboxes, variant images and dialog geometry. No commit, deployment, database writes or dependency contraction. Rollback only this batch's new select files, sort-picker changes, select import/tokens, tests and guide sections together; keep all previous batches.
- Focused baseline: **10 passed**. Expanded focused tests: **20 passed**. Final full Web suite: **1,003 passed / 0 failed / 0 skipped**. Test coverage includes empty/selected values, stable unique IDs, typed enum/nullable GUID/null selection, disabled/removed/unknown callbacks, reordering, enforced attributes, validation/recovery and disposal. Existing sort-consumer tests remain green.
- E2E build and design-rule gate passed. Initial headed run: two component checks passed; live sorting failed because the mock response did not expose `Content-Range` to the browser, triggering an aborted fallback count request. Added the correct CORS exposure header; rerun **3 passed / 0 failed / 0 skipped**, including real Blazor catalogue sorting and Back navigation with intercepted backend reads. Specimens use real component-rendered markup plus production browser JS; the live sort test exercises actual Blazor binding. No production API traffic or database mutation.
- Final headed border/short-viewport/shared-text-field verification: **4 passed / 0 failed / 0 skipped**, `E2E_HEADLESS=0`, filter `FullyQualifiedName~NativeSelectJourneyTests|FullyQualifiedName~NativeUiJourneyTests.TextFields`. Covers 390px/1440px specimens without vendor CSS, exact field/menu/check geometry, keyboard navigation and type-ahead, disabled-option skipping, Escape/Tab/pointer dismissal, focus, forced colors, reduced motion, 200% text, upward flipping/list scrolling in a 300px-tall viewport, disposal, live Blazor sorting/history, and existing independent text-field labels/descriptions. Evidence lives under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/` (`select-390-*`, `select-1440-*`); report `tests/TheShop.E2E.Tests/TestResults/select-batch23-final-headed.trx`. Reviewed desktop/mobile Figma screenshots and the mobile 200% text screenshot. Final design and whitespace gates clean; temporary test-server processes cleaned up without touching any user server.
- Graph AST refresh completed: **8,928 nodes / 22,705 edges / 434 communities**. Existing 31 zero-node configuration inputs and 33 SQL files without `tree_sitter_sql` remain; 116 community names use hub fallbacks. No semantic/API-cost graph refresh. Existing AngleSharp advisory and three unrelated ProductDescription E2E analyzer warnings remain. Chromium checks are not cross-browser or screen-reader certification; native Popover API support is required.

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

### Batch 24 — Component-local variables, current snackbar spacing, and content-sized dialog default

- Moved component-only Select, Checkbox, variant-image, notification and dialog values out of global `_theme.scss` into their owning component selectors. Shared TextField/Select chrome and motion, control radius, focus/action states and application stacking remain global. Colors, typography, spacing and named maximum widths keep their existing shared owners. Backdrop color is local to `::backdrop`; do not assume pseudo-element inheritance. Existing component override values must now be applied on the owning component, not `:root` or an ancestor.
- Updated canonical constitution/style guidance and current guide contracts to preserve that ownership boundary. Earlier execution records describing component measurements in `_theme.scss` are historical and superseded by this batch. No new abstraction, dependency, palette, or Sass token layer.
- Reinspected live Figma section `2947:15676`, Snackbar `2948:17575`: 16px padding on every side, 24px message/icon gap, 18px icon, 225×50px sample. Existing SCSS already used the approved 16px padding; corrected stale browser assertions and documentation rather than reverting the UI to 24px. All notification kinds still share one appearance; bottom-center placement, layer 1500, accessible dismiss target, and lifetime behavior are unchanged.
- Owner approved `ShopMaxWidth.None` as the dialog default. Removed the separate 500px fallback. Omitted, explicit None and explicit null now select content-sized `fit-content` width bounded by viewport gutters. Nullable parameter compatibility is retained; undefined enum values still fail. Named widths keep their existing caps; the variant picker still explicitly uses Small/600px. Header/actions remain fixed while the body scrolls.
- Baseline headed run: 11 passed, 2 failed on the pre-existing snackbar 24px expectation. After component-variable relocation and correction to the inspected 16px design: 13 passed. Added omitted/default dialog unit coverage and browser checks for short-content shrink, long-content expansion, gutters and all named caps.
- Final verification: **1,004 Web tests passed; 13 headed browser checks passed**, with `E2E_HEADLESS=0`. Result: `tests/TheShop.E2E.Tests/TestResults/component-tokens-none-dialog-headed.trx`. Browser coverage includes Checkbox, Select, Dialog, variant-image picker and snackbar at mobile/desktop sizes, plus dialog caps at a 3000px viewport. Reviewed `notifications-390.png` and `confirmation-1440.png` under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/`. Build, design-rule gate and diff whitespace check passed; temporary server cleaned up. Existing AngleSharp NU1902 and three ProductDescriptionJourney xUnit1051 warnings remain unrelated.
- AST-only graph refresh completed: 8,931 nodes / 22,709 edges / 431 communities. Existing extraction limitations remain: 31 zero-node configuration files, 33 SQL files without the optional parser, and 135 fallback community names. No semantic/API-cost rebuild or dependency installation.
- Rollback only this batch's owner relocations, dialog fallback/geometry, related assertions and documentation together; preserve earlier staged migration work. No commit, deployment, database write or Figma mutation.

### Batch 25 — Native Expander, Chip and Pagination

- Applied project constitution, lean-build and migration guidance: implemented the three requested controls and their immediate integration, without a new component suite or unrelated editor/form migration. Baseline focused Web tests: 26 passed. Figma measurements and API/ownership contracts are recorded in section 6.5.
- Added ShopExpander/ShopChip, replaced ShopPagination's Mud implementation while retaining its consumer API, and migrated ShopFilterPanel's expansion headers/count chips/layout/clear action. Filter options retain ShopCheckbox and the range branch retains MudRangeSlider. Catalogue and admin pagination callers need no changes. Updated the catalogue page object to target the native disclosure trigger idempotently.
- Preserved catalogue/admin state stores, paging/filter/sort callbacks, range debounce, form validation and backend contracts. All browser catalogue responses are intercepted test data; no database or authentication writes. Legacy product/admin accordions, chips/ChipSets and their scoped overrides remain explicit migration bridges.
- Unit coverage includes Figma page windows, bounded large totals, empty/single/clamped page states, selection and disabled guards, expander bindings/independent state/stable IDs, filter reordering and retained selection chips, chip size/semantics/attribute precedence. Browser specimens use real component markup; bUnit proves their .NET callbacks, and real WASM catalogue checks prove disclosure/filter/pagination/history integration.
- Verified 2026-10-03: full Web suite **1,023 passed**; scoped headed browser suite **6 passed, 0 skipped**. The four visual cases cover 390px/1440px with and without vendor CSS, keyboard, forced colors, reduced motion and 200% text; two live WASM catalogue cases cover retained disclosure/selection, URL paging, clear and Back navigation. Reviewed normal desktop/mobile, live catalogue, forced-colors and enlarged-text screenshots. Build, design-rule gate and whitespace checks pass. Known unrelated build warnings remain (AngleSharp NU1902 and three xUnit1051 warnings in ProductDescriptionJourneyTests).
- Evidence: `tests/TheShop.E2E.Tests/TestResults/native-components-batch25-headed-final.trx`; screenshots under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/components-*.png`. Initial runs exposed a test selector ambiguity, an unconstrained showcase grid column, and sandbox-blocked Google fonts; corrected the fixture and required loaded web fonts, then reran headed with font access. No overflow was hidden by clipping. AST graph refresh completed; existing missing SQL parser/zero-node warnings remain. No full backend E2E suite was claimed.
- Ellipsis follow-up (2026-10-03): replaced the text glyph with Figma's 22px More_Horizontal icon, retaining the existing slot, color, non-interactive semantics and page-window behavior. Focused pagination tests: **13 passed**; headed real-markup visual cases: **4 passed**, desktop/mobile with/without vendor CSS, including forced colors and enlarged text. Reviewed updated desktop/mobile screenshots. Evidence: `tests/TheShop.E2E.Tests/TestResults/pagination-ellipsis-icon-headed.trx`. Build/design/whitespace checks pass; AST graph refreshed. The existing server was left untouched; specimens were rendered from the freshly built component assembly. No full-suite rerun for this markup-only follow-up.
- Expander follow-up (2026-10-04): added natural-height CSS expand/collapse motion with reduced-motion support and immediate inert/ARIA collapse semantics; removed duplicate dividers between contiguous siblings; centered custom title/overview slot contents independently of the default H4 baseline. Focused expander/filter tests: **27 passed**. Headed desktop/mobile suite: **6 passed**, including intermediate opening/closing heights, collapsed focus exclusion, shared divider strokes, title/overview alignment, retained state, vendor-free styling and reduced motion. Evidence: `tests/TheShop.E2E.Tests/TestResults/expander-motion-alignment-headed.trx` and refreshed `native-ui-evidence/components-*.png`; screenshots reviewed. Initial browser startup failures were resolved by restarting the agent-owned server after the build completed. Build/design/whitespace checks pass; AST graph refreshed with existing parser warnings. No full-suite rerun for this focused follow-up.
- Rollback this batch's three component implementations, filter integration, styles/imports, pagination resource keys, related tests/page object and guide together. Keep prior migration batches intact. No package removal, commit, deployment, database reset or Figma mutation.

### Batch 26 — Native table foundation and admin selection — 2026-10-04

- Added `ShopTable<TItem>` with the approved native header/row templates, stable keys, caption, empty cell spanning, parent-controlled width and optional key-based selection. No column/grid engine, fetch service, sorting or pagination state inside the component.
- Replaced MudTable shells/Th/Td/selection in ManageBrands, ManageCategories and ManageProducts. Their current server paging, query state, authorization, row mutations, partial bulk outcomes and external ShopPagination remain. Cell controls and page layout still have legacy dependencies; this is not a claim that those pages are fully native.
- Added boolean-checkbox mixed presentation and visually hidden labels. Native indeterminate state is synchronized by a lazy disposable JS module; existing two-state consumers remain unchanged. Mixed glyph and accessibility states are documented in the contract above as inferred decisions.
- Baseline: **155 focused tests passed** before edits. Final focused table/checkbox/admin coverage: **152 passed**. Final full Web suite: **1,036 passed, 0 skipped**, including admin bulk tests now using rendered checkboxes rather than dispatching the table callback directly. Added component coverage for keys, stable child identity on DTO refresh/reorder, empty state, attribute forwarding, page-only select-all, disabled/stale callbacks and checkbox interop.
- Headed Chromium: **5 passed, 0 skipped**. Four 390px/1440px cases check real SSR component markup with/without vendor CSS, typography/padding/collapsed borders, focus, forced colors, 200% text and contained horizontal overflow. One live WASM Brands journey exercises mocked sign-in, native Space selection, mixed/all/none and selection reset through real pagination. All auth/data responses are intercepted; no backend writes or genuine sessions. Reviewed desktop/mobile, forced-colors, enlarged-text and live Brands screenshots.
- Evidence: `tests/TheShop.E2E.Tests/TestResults/native-table-headed.trx`; `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/table-*.png`. Mobile stress screenshots intentionally show horizontal scroll offsets, not hidden columns. Early browser runs exposed missing mock auth responses, fractional display-pixel border rounding, and breakable fixture text; corrected those tests. Browser review also caught the scroll root missing the project focus ring; fixed it in `_table.scss`. The subsequent full Web and headed runs pass.
- Build, design-rule and whitespace gates pass. AST graph refreshed; existing missing SQL-parser/zero-node warnings remain. Known unrelated AngleSharp NU1902 and ProductDescriptionJourneyTests xUnit1051 warnings remain. No full backend E2E suite, deployment, package removal or database migration was performed. Test-owned dev servers are disposed by the fixture.
- Compact-column/divider follow-up (2026-10-04): applied `shop-table-column-compact` to Status/Actions headers and cells across all three admin lists, plus the remaining variant Status column. Replaced collapsed borders with explicit single-edge ownership and padding-box header fill; the body has no top strokes to overlap the header divider. Added rendered-markup coverage for all consumers and browser assertions for compact widths, nowrap, zero border spacing, absent body top/inner right strokes, and consistent row strokes. **160 focused tests, 1,039 full Web tests, and 5 headed browser tests passed**, with no skips. Reviewed desktop/mobile, vendor-free, forced-colors and enlarged-text screenshots. Evidence: `tests/TheShop.E2E.Tests/TestResults/table-compact-divider-headed.trx` and refreshed `native-ui-evidence/table-*.png`. Design/whitespace gates pass; graph refreshed with existing parser warnings. User-staged work preserved; test-owned server disposed.
- Header-color follow-up (2026-10-04): re-inspected live Figma header `2574:7504` in section `2967:1518`; its fill is now bound to `Brand/Tertiary`, opaque `#e8e8e8`. `_table.scss` consumes `--shop-color-tertiary` directly; removed the former local header-background alias. Updated the browser color expectation and reviewed fresh desktop/mobile screenshots. **5 headed table tests passed, 0 skipped** (including vendor-free styling, forced colors, overflow and live selection). Evidence: `tests/TheShop.E2E.Tests/TestResults/table-tertiary-header-headed.trx`. Build/design/whitespace checks pass; graph refreshed. No full Web-suite rerun for this color-only follow-up.
- Next table work: ProductVariantsCard, preserving virtualization, price editing/validation, copy-to-all, availability and image-picker state. Keep its MudTable bridge until that editor is verified.

### Batch 27 — Native Appbar, Breadcrumb and Badge — 2026-10-04

- Implemented the three live-inspected Figma contracts in section 6.6. Native header/nav/links reuse existing button/icon/image styles; all appbar actions use the outlined Medium treatment. Logo size is owned by its parent (52px square), not an inline image override. Desktop actions use 24px gaps; mobile uses 12px to preserve space at enlarged text sizes. Page/link destinations and the existing unwired search action are unchanged.
- Replaced breadcrumb vendor markup, breakpoint provider/builders, and the shared Mud item type. Builder/state/page ownership and event lifetimes stay intact. Preserved removed-parent text, encoded labels, link destinations, nav naming, truncation and access to intermediate items. Only the final item is marked current. Native collapse/expand retains focus and resets on a changed trail.
- Added `ShopBadge` with six inspected color roles, Filled/Outlined treatments, three sizes and direct base/contrast-token usage. Primary outlined uses the inspected default-line stroke. Replaced only the existing image-upload Primary chip with the badge, preserving file/order behavior. No new package, JS module, general menu framework, global component token, database change or SDD artifact.
- Baseline breadcrumb/profile/upload tests: **60 passed**. Final full Web suite: **1,049 passed, 0 skipped**. Added appbar anonymous/authenticated branch and root-attribute checks, all 36 badge combinations/defaults/rejection checks, breadcrumb disclosure/reset/ARIA/root-attribute checks, and native badge consumer assertions. Existing profile tests now click the rendered trigger instead of calling the vendor menu method.
- Final headed Chromium run: **5 passed, 0 skipped**. Four 390px/1440px cases verify real appbar/breadcrumb markup and actual SSR badge components with/without vendor CSS, Figma geometry/colors/font family, logo visibility, focus, forced colors, enlarged text, overflow and Home-link trail clearing. One mocked-auth WASM journey preserves table selection/pagination and exercises the account trigger, Add Brand navigation, Enter/Space ancestor disclosure with retained focus, parent navigation and trail reset. API calls are intercepted; no backend writes or genuine sessions. Visual shell specimens intentionally stub catalogue data rather than testing catalogue results; the generic failure notification visible in some evidence is from that stubbed catalogue response, not a claimed successful catalogue-data test. The runtime error element is checked hidden before isolating vendor-free screenshots.
- Evidence: `tests/TheShop.E2E.Tests/TestResults/native-shell-badge-headed.trx`; `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/shell-*.png`, including `shell-breadcrumb-expanded-live.png`. Desktop/mobile, forced-colors, enlarged-text and live disclosure screenshots reviewed. Early checks caught the logo parent-height issue and fixture selector/session assumptions; corrected before the final passing run. Existing palette contrast limitations remain documented; this is not a WCAG color-compliance claim.
- Build/design/whitespace gates pass. AST graph refreshed with existing SQL-parser/empty-file warnings. Existing AngleSharp NU1902 and unrelated ProductDescriptionJourneyTests xUnit1051 warnings remain. Test-owned servers disposed; no user process stopped, no commit/staging operation performed.
- Remaining bridge: signed-in `ProfileMenu` still owns its Mud popup, activation wrapper, keyboard/focus behavior and permission/sign-out logic. MainLayout, other shell surfaces and upload chrome still contain Mud controls. Native appbar anonymous rendering, breadcrumbs and badges do not require vendor CSS; this is not a claim that the entire authenticated shell is vendor-free.
- Rollback boundary: revert this batch's appbar/breadcrumb/badge files, shared breadcrumb model migration, profile trigger and Primary-label substitutions, stylesheet imports/resources, tests and guide record together. Preserve earlier native table/color/selection work. No stored-data rollback is needed. Next work remains the remaining shell/profile menu/loading overlay and the separately planned money/variant editor migration.

- Breadcrumb disclosure follow-up (2026-10-04): rechecked Figma ellipsis instance `2974:9343`; replaced bespoke button chrome with Small / Text / Primary `ShopIconButton` and `ShopIcons.Outlined.More_Horizontal` (24px button, 18px icon). Expansion now removes the trigger and its separator, then focuses the first revealed ancestor; disabled ancestors support programmatic focus without becoming tab stops. Equivalent trails retain expansion; changed trails reset it. Removed the unused collapse label and updated section 6.6. **52 focused breadcrumb tests, 1,051 full Web tests, and 1 headed live browser test passed, 0 skipped**. Reviewed collapsed/expanded mobile screenshots; the browser checks shared button/icon sizes, keyboard expansion, trigger removal, focus transfer, separator count and parent navigation. Evidence: `tests/TheShop.E2E.Tests/TestResults/breadcrumb-expander-headed.trx` and `native-ui-evidence/shell-breadcrumb-{collapsed,expanded}-live.png`. Design/whitespace gates pass; AST graph refreshed with existing parser warnings. Test-owned server disposed; unrelated migration work preserved.

### Batch 28 — Page-owned native Bulk Action Bar and smooth docking — 2026-10-04

- Reinspected Figma section `2976:9566`, component `2976:12393`, and Manage Brands instance `2976:12472`. Migrated the existing shared bar and the three management-page action slots; moved each bar above its table inside the list container. Removed fixed-bottom styles and their conditional bottom-padding compensation. Section 6.7 records API, appearance, docking geometry, lifecycle and inferred responsive/motion behavior.
- Reused native buttons/icons, color/typography tokens, page selection and permission gates. MainLayout only exposes its scroll boundary; it does not own actions or selection. One DOM instance, reserved inline space, measured scrollport geometry, cancelable 200ms animation, reduced motion, resize/text-size remeasurement and disposal cleanup keep docking local to the control. Application commands, confirmations, partial outcomes, busy-state keys, routes and backend data remain unchanged.
- Baseline focused tests: **142 passed**. Final full Web suite: **1,048 passed, 0 skipped**; consolidated 10 legacy component cases into 7 native cases while retaining visibility/count/slot/style contracts and adding close/lifecycle/accessibility coverage. Existing admin tests cover permission gates, busy-state disabling, confirmations, mixed outcomes, clearing and page-local selection. All three pages also assert native buttons and placement beside the table.
- Final headed Chromium suite: **5 passed, 0 skipped**. Four real-component specimens at 390px/1440px with/without vendor CSS verify Figma geometry, intermediate dock/undock animation widths, reserved table position, retained focus/node identity, rapid reversal, reduced motion, resize, 200% text, forced colors and removal cleanup. One mocked-auth live Brands journey verifies actual MainLayout docking, full scrollport width, rendered Close clearing selection, paging and navigation disposal. No backend writes or genuine authentication calls; API requests are intercepted.
- Evidence: `tests/TheShop.E2E.Tests/TestResults/bulk-action-docking-headed.trx` and `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/bulk-*.png`. Reviewed live inline/docked, desktop/mobile, vendor-free, enlarged-text and forced-color captures. Isolated specimens use a test scroll container; live Brands covers integration. Existing palette contrast limitations remain, and remaining page layout/search/row/footer Mud consumers are not claimed migrated.
- Build/design/whitespace checks pass. AST graph refreshed with existing empty-file/SQL-parser warnings. Existing AngleSharp NU1902 and unrelated ProductDescriptionJourneyTests xUnit1051 warnings remain. Earlier staged migration changes preserved; test-owned servers cleaned up, no commits/staging/deployments.
- Rollback: revert this batch's bar/module/styles, layout scroll hook, three page placements/action changes, resources, tests and guide record together. Restore the prior fixed-bottom styles and padding only as a unit; no stored-data rollback is required.

### Batch 29 — Generic native Range Slider — 2026-10-04

- Tooltip follow-up: the first arrow-only fix passed its box-centering checks but still displaced the box from the thumb. Superseded: center the whole box over the active thumb with a fixed -50% translation and keep the arrow at 50% inside it. A component-owned manual native popover escapes the expander's clipping boundary without moving Blazor DOM nodes or changing the track. The small range module measures the active thumb position and refreshes on render/scroll/resize; disposal removes observers/listeners. The tooltip may extend beyond the track at endpoints; do not independently clamp the box away from its thumb. Revised headed assertions compare all three centers, not only arrow-to-box alignment, and check scroll/collapse cleanup.
- Keep the tooltip at intrinsic (`max-content`) width, with the viewport-based maximum retained: shrink-to-fit sizing near a mobile endpoint can otherwise wrap a normal price. The box and arrow remain centered on the thumb, including when the box extends outside its parent. Hosts need enough viewport gutter for the half-box overhang; removing the legacy page-layout stylesheet can still clip an endpoint tooltip at the viewport edge.
- Focused numeric text omits insignificant trailing decimal zeros. Existing CAD consumers use Step=0.01, so drag values round to cents and edit as `25.74`, not `25.7400000000000`. Preserve meaningful finer precision for non-price ranges; do not hardcode currency rounding into generic numeric state. Added regression cases for both editors, rounding, Escape and finer non-price steps.
- Follow-up verification: 51 focused slider/filter Web tests and all four headed range journeys passed (390px/1440px, with/without vendor CSS). Browser assertions cover single-line currency text, box/arrow/thumb centering, focused editor precision, scrolling, and collapse cleanup. Reviewed mobile and desktop endpoint screenshots with the application styles enabled: prices remain single-line and centered above their thumbs. Design-rule and whitespace gates passed. Full Web suite was not rerun for these follow-ups.

- Added `ShopRangeSlider`, `ShopRangeValue`, component SCSS and a small pointer/keyboard module; replaced the final `MudRangeSlider` in `ShopFilterPanel`. Both catalogue and admin product filters use the same native control with page-owned CAD formatting and cent precision. Existing query/URL/null-boundary behavior remains consumer-owned.
- Added tests for atomic updates, decimal snapping, crossing/equal/negative bounds, invalid editor drafts, Enter/Escape, external resets, disabled behavior, lifecycle cleanup, pending-draft preservation and cancellation. **1,073 full Web tests passed, 0 skipped.** The panel tests no longer register Mud services.
- **6 headed Chromium tests passed, 0 skipped**: four live range cases at 390/1440px with/without vendor CSS, plus two existing populated-catalogue navigation journeys. Range coverage includes arrows/Page/Home/End, stable tab order, numeric commits/errors/Escape, query updates, overlap separation, track clicks and native dragging, Clear/null-boundary restoration, track/field geometry and 200% root text sizing. Reviewed normal and enlarged-text screenshots. Evidence: `tests/TheShop.E2E.Tests/TestResults/range-slider-headed.trx`, and `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/range-{width}-{withoutVendorCss}.png` / `range-large-text-{width}-{withoutVendorCss}.png`. Design and whitespace gates passed; AST graph refreshed with existing empty-file/SQL-parser warnings. Test-owned server disposed.
- Verification limitations: physical touch-screen/screen-reader combinations and Firefox/WebKit have not been tested. Native range inputs and the numeric-editor alternative reduce custom interaction requirements but are not a claim of full accessibility certification. Existing AngleSharp package advisory and unrelated E2E analyzer warnings remain; no dependency versions changed.
- Separate existing defect found during rapid product-result reloads: `ShopImage.OnAfterRenderAsync` can resume its module import after disposal and pass a disposed DotNetObjectReference to `observe` (line 159). This caused one headed rendering failure; no ShopImage files were changed in this batch. The focused range journey uses an empty mocked result set to isolate the control; the existing populated-catalogue navigation journey remains part of regression coverage. Fix image lifecycle separately before treating rapid populated-catalogue reloads as fully verified.
- No SDD artifacts, database changes, broad UI framework, package deletion or unrelated component migration in this batch.

### Batch 30 — Native Drawer and profile migration — 2026-10-04

- Added reusable `ShopDrawer` and migrated `ProfileMenu` to `ProfileDrawer` in MainLayout. AuthLayout remains unchanged. Preserved customer name/email, My Profile (identity link), Orders, Wishlist, permission-gated Admin Console and the existing sign-out command/notification/navigation flow. Profile loading now cancels on disposal; logout retains its own completion across auth-driven component removal.
- Inspected Figma drawer and account nodes using the desktop bridge and reviewed the account screenshot. Added full-height right-side chrome, 400px desktop/full-width mobile treatment, header/body/optional-action slots, full-screen overlay and smooth reversible motion. Shared modal interop handles native inertness, focus restoration, explicit Tab wrapping, modal replacement and disposal. No global drawer service, additional UI package, database change or SDD artifacts.
- Added `_layers.scss` for sticky/appbar/notification document layers and updated token ownership guidance. Native modal ordering is explicitly separate from numeric stacking. Remaining vendor providers/global loading overlay are not removed by this batch.
- Unit verification: 20 focused component tests and 1,082 full Web tests passed. Headed verification is recorded below after the final run. Initial native browser checks exposed a missing Tab wrap and drove a shared modal fix. The local real-auth journey was unavailable because the OTP inbox on 127.0.0.1:54324 refused connections; profile integration uses intercepted test-only authentication responses instead, with no live-backend writes.
- Rollback: revert only this batch's Drawer/ProfileDrawer additions, MainLayout/AppBar wiring, shared modal changes, layer-token move and paired tests/docs together to restore the prior ProfileMenu. No stored-data rollback or package restore is needed; preserve unrelated working-tree edits.

### Batch 31 — Native loading overlay — 2026-10-05

- Replaced `MudOverlay`/`MudProgressCircular` in `ShopLoadingOverlay` with a native fixed backdrop, reused `.shop-spinner` and a primed visually hidden status. Code-behind subscription, `BusyKeys.Global` scope and `ShopUiHost` mounting are unchanged. Added `_loading-overlay.scss` and `--shop-layer-loading` in `_layers.scss`. Contract recorded in section 8.4. Only live consumer: `ProfileDrawer` sign-out.
- Baseline: 1,082 Web tests (batch 30). Added `ShopLoadingOverlayTests` (6): idle primed status, busy overlay/announcement and release, failed-operation release, other-key isolation, already-busy initial render, disposal unsubscription. **Full Web suite: 1,088 passed, 0 skipped.**
- Headed Chromium: **8 passed, 0 skipped**. New `NativeLoadingOverlayJourneyTests` renders real component markup at 390px/1440px with and without vendor CSS: fixed full layout-viewport coverage, z-index 2000, scrim color, centered 48px primary-color spinner with 6px stroke, blocked clicks on an underlying full-page button, status text, reduced motion and forced colors. `ProfileDrawerJourneyTests` (4 cases) now holds the intercepted logout response and asserts the live overlay appears during sign-out and disappears afterwards. Auth/data responses are intercepted; no backend writes or genuine sessions.
- Browser review caught forced colors painting the spinner's transparent arc (static full ring); fixed with `forced-color-adjust: none` and system colors, with a browser assertion. Headed display scaling produced fractional CSS-pixel viewport/border values; assertions compare with the layout viewport and allow device-pixel stroke snapping.
- Evidence: `tests/TheShop.E2E.Tests/TestResults/loading-overlay-headed.trx`; `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/loading-overlay-*.png` and `profile-drawer-*.png`. Desktop/mobile, vendor-free and forced-colors screenshots reviewed.
- Follow-up (2026-10-05): spinner switched to `--shop-color-primary` with a thicker 0.375rem (6px) stroke at user request.
- Limitations: keyboard focus behind the overlay remains reachable (parity with MudOverlay). Screen-reader announcement timing is not certified. Existing AngleSharp NU1902 and unrelated E2E xUnit1051 warnings remain.
- Rollback: revert this batch's overlay markup/doc comment, `_loading-overlay.scss`, its `TheShop.scss` import, the layer token, both test files, the profile-journey logout gate and this guide record together. No stored-data rollback.

### Batch 32 — Remove unused ShopFieldLabel — 2026-10-05

- Deleted `Components/Common/ShopFieldLabel.razor` (Mud `MudStack`/`MudText`, no code-behind). Repository search found no consumers or tests; the `.shop-field-label` class is owned by `ShopTextField`/`_native-field.scss` and is unchanged. Removed its target-tree, form-contract and known-trap entries.
- Rollback: restore the single Razor file from Git. No behavior, style or data change.

### Batch 33 — Native Announcement Bar — 2026-10-05

- Replaced `MudComponentBase`, `MudPaper`, `MudStack`, `MudText`, `CssBuilder` and the `mud-theme-dark` class in `ShopAnnouncementBar` with native markup on `ShopComponentBase`, `ShopCssClass` and `_announcement-bar.scss`. Contract recorded in section 6.9. Only consumer: `MainLayout`; its usage is unchanged.
- Added `ShopAnnouncementBarTests` (2): encoded message in native markup without vendor classes, and root class/style/attribute forwarding. **Full Web suite: 1,090 passed, 0 skipped.**
- Headed Chromium: **4 passed, 0 skipped**. `NativeAnnouncementBarJourneyTests` renders real component markup at 390px/1440px with and without vendor CSS: Figma fill, text color, Subtitle 1 family/size/weight/tracking with loaded web font, padding, 32px minimum height (exact at 1440px), full width, centered text, and long-message wrapping at 200% root text without horizontal overflow. At 390px the Figma sample message wraps to two lines and the bar hugs it; the first run's fixed-32px expectation was corrected to a wrap-aware assertion.
- No live announcement journey: no current code calls `AnnouncementState.Set`, so the live shell never shows the bar. Evidence: `tests/TheShop.E2E.Tests/TestResults/announcement-bar-headed.trx`; `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/announcement-*.png`. Desktop/mobile, vendor-free and enlarged-text screenshots reviewed. Design-rule and whitespace gates pass.
- Rollback: revert this batch's component markup/code-behind, `_announcement-bar.scss`, its `TheShop.scss` import, both test files and this guide record together. No stored-data rollback.

### Batch 34 — Native OTP Input on label-less ShopTextField — 2026-10-06

- Replaced `MudComponentBase`, `MudStack`, `MudNumericField`, `MudText`, `CssBuilder`/`StyleBuilder` and `UserAttributes` in `OtpInput` with native markup on `ShopComponentBase`, label-less `ShopTextField` digit boxes, `ShopCssClass` and `_otpinput.scss`. Contract recorded in section 6.10. `SignInVerify` now passes `data-testid` directly; `SignUpVerify` usage is unchanged.
- `ShopTextField.Label` is now optional and nullable, no longer `EditorRequired`. Without a label the field renders no `<label>`, keeps caller ARIA naming, and rejects a field with no accessible name (section 7.3). Labelled behavior is unchanged.
- `shopOtpInput.js` selectors moved to `input.shop-otp-digit`, and its Mud edit-mode workarounds were removed. Sanitization now accepts ASCII digits only.
- Added `OtpInputTests` (5): label-less native markup, ARIA names, root forwarding, typed/rejected/autofilled digits with one completion per fill, backspace navigation, value/paste distribution, and disabled/hint association. Added two `ShopTextFieldTests` label-less cases and updated the blank-name rejection case. **Full Web suite: 1,124 passed, 0 skipped.**
- Headed Chromium: `NativeOtpInputJourneyTests` **3 passed, 0 skipped**. It ran on the real `SignInVerify` page with backend requests aborted, at 390px and 1440px, and at 1440px with vendor/app CSS disabled. It checks: autofocus; no labels; H6 font/size/weight/tracking/centering with loaded web font; idle 1px and focused 2px outlines; 24px/8px gaps; equal 63px boxes; no horizontal overflow; repeated digits advancing; letters rejected; backspace and arrow navigation; paste distribution; and the page's verify button enabling on completion. The first run's banner assertion failed only in the vendor-free case, because disabling `app.css` reveals `#blazor-error-ui`. No page errors occurred, and that assertion is now limited to vendor-CSS runs.
- Not verified: `SignInJourneyTests` and `AuthenticationJourneyTests.AC11` failed before typing any OTP. The local Supabase mail inbox (`127.0.0.1:54324`) refused connection, so a real emailed-OTP sign-in was not run. No database reset was performed. Evidence: `tests/TheShop.E2E.Tests/TestResults/otp-input-headed.trx`; `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/otp-*.png` (desktop/mobile idle and filled, vendor-free, all reviewed). Design-rule gate passes.
- Rollback: revert `OtpInput` markup/code-behind, the `ShopTextField` label change, `shopOtpInput.js`, `_otpinput.scss`, the `SignInVerify` attribute, the test files and this guide record together. No stored-data rollback.

### Batch 35 — Native Rich Text Editor wrapper — 2026-10-06

- Replaced `MudComponentBase`, `CssBuilder`/`StyleBuilder` and every `--mud-palette-*` consumer in `ShopRichTextEditor` and `_rich-text-editor.scss` with `ShopComponentBase`, `ShopCssClass` and `--shop-*` field tokens. Contract recorded in section 6.11. `shop-rich-text-editor.js`, the vendored Quill assets and `ProductContentCard` are unchanged.
- `ShopRichTextEditorTests`: removed `AddMudServices`; added root class/style/attribute forwarding with an empty host and no vendor classes, and a Disabled modifier toggle that calls `setDisabled` without a second `init`. **Full Web suite: 1,126 passed, 0 skipped.**
- Headed Chromium: `NativeRichTextEditorJourneyTests` **4 passed, 0 skipped**. Real host markup plus the real Quill module on the sign-in page (backend aborted) at 390px/1440px, with and without vendor/app CSS, enabled and disabled: chrome and separator, removed Quill borders, 160px minimum, padding, Body 1 family/size/tracking with loaded font, placeholder color/style, 2px focus chrome without an inner editor ring, toolbar keyboard focus ring, bold toolbar formatting through to the `OnTextChanged` payload, `aria-labelledby`, disabled `contenteditable=false`, toolbar pointer-events and icon color, no horizontal overflow.
- Fixed during browser review: disabled icons stayed black because buttons do not inherit `color` (now direct token strokes/fills), and adding `shop-native` drew the shared focus ring on Quill's contenteditable inside the host chrome (now suppressed on `.ql-editor:focus` only).
- Pre-existing, not changed: Quill 2.0.3 `getSemanticHTML()` serializes spaces as `&nbsp;` (for example `<p><strong>Bold&nbsp;copy</strong></p>`), so persisted descriptions carry that entity. The journey asserts the current payload rather than normalizing it.
- Not verified: the authenticated product create/edit journey. The admin form needs a signed-in staff session, and the local Supabase stack was not running in batch 34. No database reset was performed. Evidence: `tests/TheShop.E2E.Tests/TestResults/rich-text-editor-headed.trx`; `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/rich-text-*.png` (idle, focused, disabled, mobile and vendor-free reviewed). Design-rule gate passes.
- Rollback: revert the component markup/code-behind, `_rich-text-editor.scss`, both test files and this guide record together. No stored-data rollback.

### Batch 36 — Native Skeleton and table loading state — 2026-10-06

- Design: created Figma Skeleton set `3010:17894` in section `3010:17888` with Rectangle and Text shapes, usage examples and rules. The shimmer variants and keyframes were removed after owner review; the approved design is static. Contract recorded in section 6.12.
- Replaced all 15 `MudSkeleton` uses:
  - ProductCatalogue: 12 `ProductCardSkeleton`s plus a 5-row filter skeleton.
  - AdminConsole: 5 `AdminModuleCardSkeleton`s.
  - EditBrand, EditCategory and EditProduct: `AdminFormSkeleton`.
  - Manage Brands, Categories and Products: `ShopTable.Loading`.
  - ShopImageUpload: pending rows.
- Page Mud layout (`MudGrid`, `MudStack`) around these skeletons is unchanged.
- Added `ShopSkeleton`, `ShopSkeletonShape`, `ProductCardSkeleton`, `AdminModuleCardSkeleton`, `AdminFormSkeleton`, `_skeleton.scss` and `_admin-skeletons.scss`. Extended `_producttile.scss`, `_filter-panel.scss` and `_imageupload.scss`. Added `ShopTable.Loading`/`LoadingRowCount`.
- Tests:
  - `ShopSkeletonTests` (6): primitive forwarding, enforced `aria-hidden`, shape modifiers, undeclared-shape rejection, and composite class reuse and hiding.
  - `ShopTableTests` (+5): loading rows under the real header, with and without selection; busy and status lifecycle; selection guard while loading; row-count validation.
  - Updated loading tests in ProductCatalogue, AdminConsole and ManageProducts.
  - **Full Web suite: 1,137 passed, 0 skipped.**
- Added `NativeSkeletonJourneyTests`:
  - The catalogue case runs on the real anonymous page with data reads held pending, at 390px and 1440px. It checks the square media, line widths and bar heights, the 72px filter rows, no animation, no overflow and the forced-colors outline.
  - The live admin case uses mocked auth with no backend writes. It covers the Manage Brands loading-to-loaded transition (header position, busy and status, disabled select-all, hidden pagination), admin console card geometry and edit-brand form geometry.
  - **3 passed, 0 skipped.** The first attempts hit a Visual Studio debug session holding port 5218 and were rerun after the owner stopped it.
- Fixed during browser review: the primitive first carried `shop-native`, whose Body 1 reset made table-cell Text bars 16px instead of following the 14px cells. The class was removed. The edit-page heading line is capped at 20rem to match the short real headings.
- Regression: `NativeTableJourneyTests` geometry (4) and `NativeBulkActionJourneyTests` (4) passed. `NativeTableJourneyTests.Brands_RealBlazorSelection…` passes its selection and pagination steps, then fails at its stale "My Profile" account-dropdown step. That step fails identically on the stashed baseline, so the failure predates this batch.
- Known difference: loading rows use the table's own cell padding and one text line (about 39px). The loaded admin rows are about 68px because of their 40px avatar, so the body grows when data arrives; the header and its position stay fixed. Evidence: `tests/TheShop.E2E.Tests/TestResults/skeleton-headed.trx`, `skeleton-table-regression.trx`; `native-ui-evidence/skeleton-*.png` (catalogue 390/1440 and forced colors, table loading/loaded, admin console, edit form; all reviewed). Design-rule gate passes.
- Rollback: revert the new components, `ShopTable` loading, page markup, SCSS partials, tests and this guide record together. No stored-data rollback.

### Batch 37 — Shared numeric editor and native money fields — 2026-10-07

- Added `ShopNumericField` on `InputBase<decimal?>`; replaced the Mud numeric implementation of `ShopMoneyField` with a focused CAD wrapper. Both reuse existing outlined field SCSS. Contract and ownership are recorded in section 7.4.
- Replaced the range slider's two string editors and duplicated parsing/edit handlers with shared numeric fields. Step snapping and atomic pairs remain in the slider; filter grouping, debounce and unbounded mapping remain unchanged. Regression cases cover rounding to a new value and rounding back to the existing value without leaving unsnapped editor text.
- ProductForm awaits price commits before its remaining Mud validation and dispatch. Variant rows retain `ShopNumericDraft` instances across virtualization, validate pending/offscreen edits and reset overwritten drafts during copy-to-all. Required, optional-null, zero, negative, invalid and high-precision inputs are covered. Existing virtualized table and unrelated Mud fields remain temporary bridges.
- Removed money-only Mud typography/density parameters from callers. Variant inputs now have accessible names identifying both the row and price column. Currency display and plain editing remain independent of ambient culture; bound amounts are not rounded to display precision.
- Verification: solution build passed with six existing dependency/analyzer warnings; design-rule gate passed. Focused suite: **126 passed**. Full Web suite: **1,165 passed, 0 skipped**. Browser suite: **6 passed, 0 skipped**, covering live catalogue range behavior at 390px/1440px with/without vendor CSS, and native money field geometry, naming, disabled/error states, Enter/Tab, large text, reduced motion and forced colors.
- Money browser specimens use production-rendered markup and the real keyboard module; C# editing and product submission are covered by bUnit. Range browser tests exercise live Blazor callbacks and query updates. Reviewed `native-ui-evidence/money-390-False.png`, `money-1440-True.png`, and `range-390-True.png` under the E2E output directory. No new Figma measurements or backend writes.
- Logs: `.sdd/.test-work/numeric-build.log`, `numeric-final-focused.log`, `numeric-web-all.log`, `numeric-browser.log`. Code graph refreshed; Graphify reports missing SQL parser coverage and zero-node configuration files, so graph output remains incomplete for those file types.
- Rollback: revert this batch's numeric/draft files, money/range changes, product caller validation, keyboard module/index registration, resources, tests and guide record together. No package or stored-data changes.

### Batch 38 — Native image upload, dropzone and tile — 2026-10-08

- Rebuilt `ShopImageUpload` from the inspected Figma section `3013:17940`; added standalone `ShopFileDropzone` and `ShopImageTile`. Native controls, scoped SCSS and small JS bridges replace the uploader's Mud dependencies. Existing `ShopImage` presets and product/brand/category persistence contracts remain.
- Adopted drag ordering, keyboard/touch Move earlier/later, first-valid-image primary allocation, hover/focus/touch removal rules and loader-only preparation. See section 9, Uploads, for ownership and removed parameters.
- ProductForm and four brand/category forms now await pending file preparation before validating and saving. Rejected files are not buffered; errors remain removable. Input reset waits for consumption, overlapping picks are blocked, external resets win, and owned preview URLs are released on removal/replacement/reset/disposal.
- Verification: **124 focused tests passed; final full Web suite 1,175 passed, 0 skipped.** Tests include identity/bytes through ordering, invalid files without reads, single replacement, stored URLs, count overflow, disabled controls, pending Save, external reset, cancellation and independent tile/dropzone use. Final review added coverage for releasing an earlier owned preview when the selection resets during another file's failed read.
- **Three live-browser tests passed, 0 skipped.** Actual Blazor ProductForm with intercepted local auth/data reads; no backend writes. Covered 390px/1440px, vendor CSS removed, touch removal without hover, visible Move actions, keyboard/native dragging, picker/drop, same-file reselection, input reset, URL release, large text, reduced motion and forced colors. These checks do not establish backend storage integration.
- Reviewed screenshots in `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/upload-*.png`: empty, grid, focus, error, touch and accessibility states. Test PNGs are deterministic tall/wide fixtures, not Figma product artwork. Grid geometry and containment match; the action row/responsive sizing are documented adaptations. Initial touch setup was blocked by a sign-in notification; the test now dismisses it normally before continuing.
- Solution build: **0 errors**, existing dependency/analyzer warnings remain. Design-rule gate and whitespace check pass. No new packages. Logs: `.sdd/.test-work/upload-focused-final.log`, `upload-web-all.log`, `upload-browser-final.log`, `upload-build.log`, `upload-design.log`; browser results: `tests/TheShop.E2E.Tests/TestResults/native-image-upload.trx`. The additional real-column mobile overflow assertion passed in `native-image-upload-touch.trx`.
- Code graph refreshed. Existing limitations remain: missing SQL parser and zero-node configuration files; no semantic relabeling requested.
- Rollback: revert this batch's uploader/primitives, callers, SCSS/JS, resources, tests and guide record together. No database or stored-data rollback.

### Batch 39 — Native date field and signup migration — 2026-10-09

- Added `ShopDateField` on Blazor `InputDate<DateOnly?>`, `_date-field.scss` and four resource messages. The Shop component owns field chrome/validation; the browser owns the picker. Contract and native-display differences are recorded in section 7.4. No package or JavaScript additions.
- Replaced signup's MudDatePicker, retained required/age/confirmation checks and busy disabling, and removed the DateTime-to-DateOnly submission conversion. Explicit `Validate()` bridges the native field into the remaining MudForm submission path. No backend contract change.
- **32 focused tests passed; full Web suite 1,194 passed, 0 skipped.** Covered nullable/required input, leap dates, invariant HTML values across cultures, invalid-date recovery, inclusive bounds, external reset, disabled event guards, attribute precedence, EditContext notifications/unmount cleanup, and signup command/pending-state dates at the age boundary.
- **Two live Chromium browser tests passed, 0 skipped**, at 390px/1440px, with vendor CSS removed in the desktop check. Verified accessible naming, date entry/clear, underage errors, required-date blocking, exact cutoff acceptance, disabled state during a held request and navigation to OTP verification. Local API interception prevents real email/backend writes. Native calendar popups and other browser/OS combinations were not separately automated.
- Reviewed `native-ui-evidence/date-390.png`, `date-1440.png` and `date-accessibility-390.png` under the E2E output directory. At 200% text, capped date-field inline padding preserves the entire year beside the native calendar control. Forced-colors focus and reduced-motion checks pass. Screenshots of the field alone crop the portion of its floating label outside the element bounds.
- Solution build: **0 errors**, existing dependency/analyzer warnings remain. Design-rule and whitespace checks pass. Evidence: `.sdd/.test-work/date-focused.log`, `date-web-all.log`, `date-build.log`, `date-browser.log`, `date-design.log`; `tests/TheShop.E2E.Tests/TestResults/native-date-field.trx`.
- The existing dev server referenced stale build assets. Added optional `E2E_APP_URL` to the test fixture and verified on a separate temporary port, preserving the default localhost:5218 and the existing server. No production configuration changes.
- Code graph refreshed; existing SQL-parser/configuration coverage limitations remain. Rollback: revert this batch's component/style/resources, signup changes, tests/fixture override and guide record. No stored-data migration.

### Batch 40 — Shared native ShopLoader — 2026-10-10

- Inspected the supplied Figma section `2996:28794`, Overlay `2996:28795` and Loader `2996:28796` through the connected plugin after the REST API returned 429. Reviewed the exported ring screenshot: 48px diameter, 80% inner radius, 90% arc, rounded ends, primary fill; the overlay has a 40% primary scrim. The SVG uses a 21.6px centerline radius and 4.8px stroke to preserve the outer/inner diameters. Contract updated in section 8.4.
- Added decorative `ShopLoader` on `ShopComponentBase`, owned `_loader.scss` and replaced `_spinner.scss`. Reused it in ShopButton (and ShopIconButton), ShopImageTile and ShopLoadingOverlay, then replaced all 15 remaining MudProgressCircular call sites in auth/admin/product forms, admin rows and AuthorizingView. No live MudProgressCircular or shop-spinner references remain in source/tests.
- Follow-up (2026-10-10): owner requested a wider gap. Reduced the arc from 90% to 80% (324° to 288° sweep) and centered the 72° gap on the right. Rounded caps occupy part of that gap; size, stroke and motion are unchanged. Updated the browser expectation.
- The default is 48px/primary. Existing callers own smaller geometry: 24px inline controls, button icon dimensions, 32px image tiles. Button loaders inherit foreground; deleting rows retain error color. Loading visibility, disabling, callbacks and BusyState ownership are unchanged. Row status labels use Strings.Loading; the shared loader is always aria-hidden and has no additional announcement. Other Mud controls/providers remain migration bridges.
- Motion at 0.8s, reduced-motion stopping and CanvasText forced-color treatment are implementation decisions. No new JavaScript, package, resource, data or backend contract changes.
- Verification: solution build passed (0 errors; existing package advisories remain); **77 initial focused tests passed**; final full Web suite **1,196 passed, 0 skipped**; final Chromium browser suite **11 passed, 0 skipped**. Browser coverage includes 390px/1440px, vendor-free overlay/buttons, rounded arc/stroke/color, loading geometry and accessible names, pointer blocking, reduced motion, forced colors, live sign-in busy recovery and image-upload regressions. API traffic is intercepted; this does not establish real emailed-OTP or backend storage integration.
- Reviewed loading-overlay desktop/mobile/vendor-free and forced-color screenshots plus buttons-loading-390.png under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/`. The overlay probe intentionally has an underlying test button. Browser tests run headlessly. Evidence: `tests/TheShop.Web.Tests/TestResults/shop-loader-focused.trx`, `shop-loader-web-final.trx`; `tests/TheShop.E2E.Tests/TestResults/shop-loader-browser-final.trx`; `.sdd/.test-work/shop-loader-build.log`.
- Initial verification caught blocked Google Fonts, an ambiguous button SVG test selector and an old AuthorizingView Mud selector; corrected selectors and reran with font access. A build attempted during browser execution hit a locked test assembly; the final sequential rebuild passed. An interrupted upload check stalled before OTP submission during rebuilding; all three upload cases passed after a clean server restart.
- Design-rule and whitespace checks pass. Code graph refreshed; existing SQL-parser and zero-node configuration limitations remain. No commit or publication. Rollback: revert this batch's loader, callers, SCSS, test updates and guide record together; restore the historical spinner partial/import. No stored-data rollback.

### Batch 41 — Sign-in Figma layout and native authentication container — 2026-10-10

- Inspected the supplied Figma section `3025:24411` and Login frame `2341:2278`, including its screenshot and exported brand mark. Replaced the two-column sign-in layout with the centered, bordered panel: 656px wide at 1440px, 64px viewport gutters, 48px content inset, 64px gray brand mark and outlined Create Account navigation. Heading, subtitle and bottom form follow the design. The existing “Email address” resource remains the input label.
- AuthLayout now renders a semantic native `main` instead of MudLayout/MudMainContent. ShopUiHost remains mounted. Mud theme/popover providers remain temporary bridges for signup and verification; those screens' visual migration is pending.
- Sign-in retains its existing EditForm, native controls, validation, busy state, dispatch and navigation. The explanatory instruction remains available through aria-describedby and is visually hidden. The brand SVG lives in ShopIcons. Authentication-owned SCSS supplies the layout and responsive geometry; mobile uses 16px outer gutters and 24px panel insets. No business or backend contracts changed.
- Browser review found the empty live validation region reserved an extra 4px gap. Scoped sign-in styling removes that gap while the region is empty and keeps the live region mounted; error spacing returns when validation renders.
- Verification: solution build passed with 0 errors and existing package warnings; full Web suite **1,197 passed, 0 skipped**. Final **headed Chromium suite: 16 passed, 0 skipped**, covering the new desktop/mobile layout, vendor-free styling, 200% text, keyboard order/focus, email validation, sign-in busy recovery, native date fields and OTP input regressions. All subsequent browser verification uses visible Chromium as requested by the owner.
- Reviewed `signin-layout-1440-False.png`, `signin-layout-1440-True.png`, `signin-layout-390-True.png` and `signin-layout-large-text-390-True.png` under `tests/TheShop.E2E.Tests/bin/Debug/net10.0/native-ui-evidence/`. Geometry assertions verify the panel, logo, email field and submit button against the desktop frame. Evidence: `tests/TheShop.Web.Tests/TestResults/signin-layout-web.trx`, `tests/TheShop.E2E.Tests/TestResults/signin-layout-headed.trx`, `.sdd/.test-work/signin-layout-build.log`.
- An initial server used stale fingerprinted assets; restarting the isolated local server resolved it. API requests are intercepted in browser checks; real emailed OTP delivery and backend integration are not established by this batch. Design-rule and whitespace checks pass. Code graph refreshed; existing SQL-parser/configuration coverage limitations remain.
- Rollback: revert AuthLayout, SignIn markup, authentication SCSS, the brand icon, tests and this guide record together. No stored-data rollback.

- Follow-up (2026-10-10): applied the owner's revised Login frame `2341:2278`. Desktop now uses a 2:3:2 grid, 32px viewport/panel insets and a 52px logo. At 1440×900 the panel is approximately 590×836 at (425,32), with a 526px-wide email field at y=713 and Login at y=790. Below 1200px the panel remains centered and capped; mobile retains 16px outer gutters and 24px insets. Updated the existing geometry assertions and settled animations for screenshot capture. Solution build and design/whitespace checks pass; **4 headed layout cases passed**, including vendor-free desktop/mobile, keyboard navigation, validation and 200% text. Evidence: `signin-size-headed.trx`, `.sdd/.test-work/signin-size-build.log` and refreshed `signin-layout-*.png`. A fresh server on port 5228 avoided the occupied prior test port.

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

# Design — CSS and SCSS

Implementation guidance for project-owned styling. Use CSS-first SCSS, semantic custom properties, and the approved Figma component design. Styles require neither a UI framework nor a separate workflow gate.

## Ownership

All authored application styles live under `src/TheShop.Web/Styles/`:

```text
Styles/
  TheShop.scss
  abstracts/    # Sass helpers and compile-time constants; no emitted CSS
  tokens/       # --shop-* colors, typography, spacing, and theme values
  base/         # Document defaults, reset, typography, accessibility
  components/   # Owned component appearance and states
  layouts/      # Page and shell geometry, responsive layout
  utilities/    # Small genuinely shared utility families
```

Reuse existing partials where ownership matches. New partials use lowercase kebab-case with a leading underscore, such as `_product-card.scss`. `TheShop.scss` loads emitting modules in an intentional order. Each partial loads its own Sass dependencies; imports elsewhere do not establish a global namespace.

Static presentation belongs in SCSS even with one caller. `base/` contains document defaults, not product-card or button rules. During coexistence, scope reset changes carefully to avoid changing unconverted controls.

Button-related ownership is split: `_icon.scss` owns standalone `.shop-icon` defaults; `_button.scss` owns the shared button foundation, color/variant treatments, text-button sizes, and `.shop-button-icon` adornments; `_icon-button.scss` owns icon-only geometry. Load them in that order in `TheShop.scss` to preserve icon sizing. Surface is a shared button color role, not an icon-only recipe. Product-card styles position cart/wishlist actions, but do not redefine shared button colors or padding. Use direct Figma-backed colors and explicit accessibility tokens in owned treatment classes, not product-level button-color aliases.

Do not add Razor `<style>` blocks, `.razor.css` isolation files, or handwritten page CSS under `wwwroot/`. Never edit generated CSS. Existing vendor overrides and template CSS are transitional migration inputs, not templates for new styles.

## Naming: component-prefixed kebab-case

Use single hyphens in class names. Do not introduce BEM `__` or `--` inside class names.

```scss
.shop-button { }
.shop-button-icon { }
.shop-button-filled { }
.shop-button-small { }

.shop-product-card { }
.shop-product-card-image { }
.shop-product-card-title { }
```

Combine base and variant classes. Prefer native state selectors such as `:hover`, `:focus-visible`, and `:disabled`. Avoid global names such as `.title`, `.primary`, and `.active`. Namespace layout and utility selectors with `shop-` too.

The leading double hyphen in a CSS custom property is required syntax, not BEM:

```scss
.shop-button-filled {
    color: var(--shop-color-primary-contrast);
    background-color: var(--shop-color-primary);
}
```

Token names in examples are illustrative: use the actual semantic token for that role, or add an intentional token in `tokens/`. Never silently reference an undefined custom property.

## CSS tokens versus Sass values

CSS custom properties own runtime design values: colors, typography, spacing, radii, shadows, and theme roles. Components consume semantic roles rather than copying palette values or binding to legacy `--mud-*` properties. Keep raw palette values in their token owner, not repeated through Razor and component partials.

Global tokens are shared design contracts, not a collection of every component measurement. Component-only geometry and appearance values belong in their owning partial, as ordinary declarations or component-scoped custom properties when reuse, variants, or dynamic overrides need them. Do not emit component-private defaults on `:root`. Keep genuinely shared field chrome/motion, focus, interaction states, and layer ordering shared; consume global color/typography/spacing scales directly. Define pseudo-element-specific defaults on the pseudo-element when inheritance is not guaranteed.

Sass variables own compile-time concerns such as breakpoints, mixin arguments, and utility-generation maps. They are not a second permanent copy of the runtime theme. Media-query breakpoints need compile-time values; ordinary `var()` substitution cannot be used as a media-query condition.

## Modern Sass modules

Use `@use` with explicit namespaces. Use `@forward` only to expose an intentional shared module API, not as another entry point that repeats emitted CSS. Do not add Sass `@import` or rely on global built-in functions.

```scss
@use 'sass:list';
@use '../abstracts/breakpoints' as bp;

$image-ratio: (4, 5);

.shop-editorial-image {
    aspect-ratio: #{list.nth($image-ratio, 1)} / #{list.nth($image-ratio, 2)};
}

.shop-product-catalogue {
    grid-template-columns: repeat(3, minmax(0, 1fr));
}

@media (width < bp.$desktop) {
    .shop-product-catalogue {
        grid-template-columns: repeat(2, minmax(0, 1fr));
    }
}

@media (width < bp.$tablet) {
    .shop-product-catalogue {
        grid-template-columns: minmax(0, 1fr);
    }
}
```

`abstracts/_breakpoints.scss` owns the two viewport thresholds: `$tablet: 768px` and `$desktop: 1024px`. Desktop styles are the default. Put overrides for widths below `$desktop` next, then mobile overrides below `$tablet`. Mobile inherits both sets of overrides. Use strict `<` comparisons to avoid gaps at fractional viewport widths; do not duplicate numeric thresholds in consumers. Not every component needs both queries.

Figma reference frames are desktop 1440 × 900, tablet 834 × 1194, and mobile 390 × 844; page height can grow with content. These are review canvases, not additional breakpoints. Check 320px and both sides of 768px/1024px as well as the reference widths. Shared maximum content widths remain sizing tokens, independent of breakpoints.

Prefer `map.get()` over deprecated global `map-get()`.

Generate repeated utility families from a map/list and `@each`, not copied rules per value. Do not build a general utility framework for hypothetical callers. Ordinary component/layout selectors need no generation loop.

## Selectors and formatting

- Four spaces, opening braces on the same line, one declaration per line, trailing semicolons, and a final newline.
- Shallow component selectors. Nest states, pseudo-elements, or responsive rules when useful; do not mirror the DOM tree in SCSS.
- Prefer classes. Avoid ID selectors, deep descendants, tag-qualified component selectors, and routine `!important`.
- Consistent declaration grouping: layout/position, dimensions/spacing, borders/background, typography, interaction, then transitions.
- Prefer logical properties such as `padding-inline` and `margin-block` when they express intent.
- Scalable text/spacing units, `normal` line-height for Figma AUTO (unitless ratios for explicit heights), and no fixed-height text containers. `px` remains appropriate for thin borders and Figma's measured tracking.
- Desktop-default responsive layout with shared tablet/mobile overrides. Choose Grid/Flexbox from layout needs, not a vendor wrapper API.
- Preserve visible keyboard focus, usable targets, reduced-motion preferences, and text/state contrast. Validation cannot rely on color alone.
- Animate named properties, not `transition: all`; do not add motion without a design or interaction purpose.

```scss
.shop-button {
    display: inline-flex;
    align-items: center;
    justify-content: center;

    &:focus-visible {
        outline: 2px solid var(--shop-color-focus-ring);
        outline-offset: 2px;
    }

    &:disabled {
        cursor: not-allowed;
    }
}

.shop-button-icon {
    inline-size: 1em;
    block-size: 1em;
    flex-shrink: 0;
}
```

Place variant selectors beside the base selector, not behind deeply nested ancestors. CSS specificity, cascade order, and layers determine precedence; the order of names in the HTML `class` attribute does not.

## Composition and dynamic styles

Keep conditional composition in code-behind using `ShopCssClass.Join`:

```csharp
private string ClassName => ShopCssClass.Join(
    "shop-product-card",
    Compact ? "shop-product-card-compact" : null,
    Class);
```

Forward classes and exposed styles to the documented root. Do not retain a vendor dependency for `CssBuilder`/`StyleBuilder` or build a replacement framework.

Inline styles are for genuinely dynamic values, not fixed one-off layout. Prefer constrained custom properties populated from validated numeric values, with culture-invariant formatting and fixed units. Do not interpolate untrusted CSS, resource text, URLs, or arbitrary server content. Static width limits, spacing, and typography stay in SCSS.

## Coexistence and verification

Native controls must work without vendor CSS. Existing Mud consumers may temporarily retain their classes/overrides; remove each bridge with its final consumer rather than deleting all shared styling at once. Do not add new `mud-*` or `--mud-*` dependencies to native components.

Check converted and unconverted screens after shared changes. Verify responsive geometry, focus/disabled/error states, overflow, and wrapping in the browser. Use Figma for measured appearance without sacrificing semantic behavior. Sass compilation proves syntax, not visual fidelity or accessibility.

# Design — Theme Setup (tokens and icon registry)

Implementation guide for Rule 13 and the ownership behind Rules 15, 16, 18, 19. Load when editing `Styles/tokens/` or `Theme/ShopIcons.cs`. For applying these values, see `design-theme.md`.

## Ownership

All paths below are relative to `src/TheShop.Web/`.

| File / folder | Responsibility |
|---|---|
| `Styles/tokens/_colors.scss` | Semantic text, surface, border, action, and status colors |
| `Styles/tokens/_typography.scss` | Font families, sizes, weights, line heights, tracking |
| `Styles/tokens/_spacing.scss` | Shared spacing scale, emitted as CSS custom properties |
| `Styles/tokens/_sizing.scss` | Shared named maximum widths, emitted as `--shop-max-width-*` |
| `Styles/tokens/_theme.scss` | Theme decisions such as control radii, focus and interaction tokens |
| `Styles/abstracts/` | Sass-only helpers, functions, maps, breakpoint constants; emits no CSS |
| `Styles/base/` | Reset, document, typography, accessibility defaults |
| `Styles/components/` | Component appearance and states |
| `Styles/layouts/` | Page and shell geometry |
| `Styles/utilities/` | Small justified, generated utility families |
| `Styles/TheShop.scss` | Ordered `@use` entry point |
| `Theme/ShopIcons.cs` | Trusted application SVG registry |

Do not introduce replacement C# color/typography registries or a runtime theme service to hold static CSS values. Existing `ShopColors`, `ShopTypography`, and `ShopTheme` remain only while unconverted Mud consumers need them.

## Token setup

`ShopMaxWidth` carries width choices only; `tokens/_sizing.scss` owns their numeric caps. These owner-approved values are not Figma breakpoints. `_theme.scss` owns the separate 500px dialog default (`--shop-dialog-max-width`); dialog modifiers consume the shared scale locally. `None` removes the cap, not viewport gutters.

Use `--shop-*` CSS custom properties for values consumed at runtime. Sass variables are for compile-time helpers and generation, not a second manually maintained palette. Current declarations include:

```scss
// Styles/tokens/_colors.scss
:root {
    --shop-color-text-primary: #171717;
    --shop-color-surface-default: #fff;
    --shop-color-primary: #171717;
    --shop-color-primary-contrast: #fff;
}
```

This is an excerpt, not a replacement file. The full palette mirrors 22 Figma paint styles. Omit the `Brand/` and `Semantic/` groups in CSS names; retain `text-`, `surface-`, and `lines-`. Consume these tokens directly. The seven migration aliases (`action`, `on-action`, `field`, `surface`, `surface-muted`, `surface-placeholder`, and `border` under `--shop-color-`) have been removed; do not recreate them. The four `--shop-button-*` color override properties have also been removed: button and icon-button treatments consume Figma colors directly. Internal size properties and the distinct state/accessibility tokens in `_theme.scss` remain supported. Reuse existing tokens; add a semantic token only when a real design need exists. Do not add empty values or speculative dark-mode tokens.

Typography mirrors the 13 `Typography/*` Figma text styles as `--shop-typography-{style}-{property}`: H1–H6, Subtitle 1/2, Body 1/2, Button, Caption, Overline. `_typography.scss` owns the map and emits family, size, weight, line-height, letter-spacing, and text-transform properties. Sizes use rem; AUTO line height maps to `normal`. `abstracts/_text-style.scss` applies those variables without alias tokens or standalone selectors. Keep component-specific exceptions documented in their owner; do not change a named style to fit one consumer. See `UI_MIGRATION_GUIDE.md` section 5.1 for the inspected values and mapping.

The owner also approved four white semantic contrast defaults in `_colors.scss`: info-contrast, success-contrast, warning-contrast, and error-contrast. These extend the 22 inspected styles; they are not claimed Figma measurements. Filled button labels consume their role's contrast token, while outlined/text labels consume the base role color. Do not add separate button text-color overrides. `_theme.scss` owns interaction backgrounds; the separate error-text token was removed at the owner's request. Native form validation messages and invalid borders use `--shop-color-error` directly. That text on white measures approximately 3.44:1, below the 4.5:1 small-text threshold. White defaults and bright base-colored text have further documented contrast gaps in `UI_MIGRATION_GUIDE.md` section 6.2; token naming is not accessibility proof. Resolve these palette limitations before production accessibility sign-off.

Use lowercase partial names with leading `_`, four-space indentation, semicolons, and one declaration per line. Load modules with `@use`; use `@forward` only for a deliberate shared API. Keep `abstracts/` free of emitted rules. Each partial imports the helpers it uses. Never edit generated `wwwroot/css/TheShop.css`.

Classes use single-hyphen, component-prefixed kebab-case (`.shop-button-filled`, `.shop-button-icon`), not BEM `__`/`--`. This restriction does not apply to the required leading `--` of custom properties.

## Figma and interaction decisions

Visual source: [The Vape Shop component foundations](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=197-2570).

Inspect the relevant component node before assigning measured values. Preserve existing rendered behavior where the supplied design does not specify a replacement. Record missing or ambiguous states and chosen treatments separately from measured design values. Accessibility requirements still apply: do not suppress focus, remove labels, or assume a bright status swatch has sufficient text contrast.

Component resting variants, interactive states, and responsive behavior require browser verification. Token extraction alone is not proof of Figma parity.

## Base styles and coexistence

Base styles provide shared document defaults; they do not own component variants or page layouts. While vendor components remain, scope native defaults to migrated surfaces and test mixed screens before changing global selectors.

Keep existing Mud providers, styles, scripts, registrations, and C# theme values until their final consumers are replaced and verified. Track those temporary bridges explicitly. The final native theme comes from compiled SCSS, without Mud theme DI or a permanent parallel C# palette.

## `ShopIcons` registry

Keep `Theme/ShopIcons.cs` as the custom SVG registry. New icons use semantic names describing their action or meaning. Reuse existing entries instead of duplicating paths or introducing another icon suite.

When adding an icon:

1. Obtain the approved SVG asset and inspect its markup.
2. Store the trusted SVG fragment expected by `ShopIcon`, not an extra outer `<svg>` root.
3. Preserve the component's view box contract, stroke attributes, and `currentColor` where appropriate.
4. Exclude scripts, event handlers, external resource references, and untrusted markup.
5. Add a semantic constant and consume it through the shared icon component.

`ShopIcon` is decorative. The containing control owns its resource-backed accessible name. Its raw-markup rendering is safe only for reviewed application constants; never route product descriptions, uploaded SVGs, API responses, or user input through that sink.

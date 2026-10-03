# Design — Theme (applying color, typography, icons, imagery)

Implementation guide for Rules 15, 16, 18, 19. For token ownership and icon registration, see `design-theme-setup.md`. Native components use semantic HTML and project SCSS; vendor classes are not the native design API.

## Design source

Use the supplied [Figma component foundations](https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=197-2570) for inspected visual values, variants, and geometry. Record the specific node and evidence when verifying a component. Do not copy generated Figma CSS or claim visual parity without rendering and comparison.

Preserve native semantics, keyboard access, visible focus, text resizing, and accessible names. If Figma omits hover, disabled, loading, validation, or focus states, record the chosen treatment as an implementation decision—not a Figma measurement. Flag unresolved design conflicts rather than silently inventing variants.

## Applying color (Rule 15)

Choose a semantic token by its role and consume it through the component's own SCSS class. Current tokens live in `src/TheShop.Web/Styles/tokens/`; inspect their declarations before use.

```scss
.shop-product-card {
    color: var(--shop-color-text-primary);
    background-color: var(--shop-color-surface-default);
    border: 1px solid var(--shop-color-lines-default);
}

.shop-product-card-description {
    color: var(--shop-color-text-secondary);
}

.shop-button-filled {
    color: var(--shop-color-primary-contrast);
    background-color: var(--shop-color-primary);
}
```

- Classes use component-prefixed kebab-case: `.shop-button-icon`, `.shop-button-filled`, `.shop-product-card-title`. No BEM `__` or `--` inside class names.
- CSS custom properties retain mandatory leading `--`: `--shop-color-primary`.
- Keep selectors shallow. Prefer native `:hover`, `:focus-visible`, and `:disabled` states.
- No hex values in Razor, new `--mud-*` consumers, or permanent palette copies in C#.
- Check contrast for actual foreground/background pairs. Decorative status swatches are not automatically suitable for small text; use the purpose-specific text token where available.
- Convey errors and status with text or other cues as well as color.

## Typography usage (Rule 16, Rule 18)

Use semantic headings, paragraphs, and inline elements. Heading level expresses document hierarchy; a component class determines its visual size. Keep a page heading available for navigation focus.

`Styles/tokens/_typography.scss` owns values. `Styles/base/_typography.scss` applies shared defaults; component classes apply meaningful variations:

```scss
@use '../abstracts/text-style';

.shop-product-card-title {
    @include text-style.apply(h6);
    // Figma's product title is an unbound 20px Regular text node.
    font-weight: var(--shop-typography-body-1-font-weight);
}
```

Use the 13 Figma-named styles through `text-style.apply(style)` or their direct `--shop-typography-{style}-{property}` variables. No application typography aliases. Use `rem` for scalable type and `normal` for Figma AUTO line height; use unitless ratios only when the design specifies an explicit line height. Preserve semantic text decoration and component spacing. Do not fix the height of text containers or inline static font styles. Small shared utility families, if justified, are generated from Sass maps/lists under `utilities/`, not emitted from `abstracts/`. Do not recreate `MudText` as a `ShopText` wrapper.

During coexistence, shared native defaults are scoped to migrated surfaces (currently `.shop-native`). Do not broaden resets or typography selectors until remaining vendor screens have been checked.

## Icons — custom SVG only (Rule 19)

Use the existing `Theme/ShopIcons.cs` registry. New entries use semantic names; do not replace existing consumers merely to rename unrelated entries.

`ShopIcon` renders a trusted registry fragment inside its SVG root. It is decorative and hidden from assistive technology; the containing button/link owns its accessible name. Visible resource-backed text is preferred; icon-only controls need a resource-backed accessible label.

Never pass user-provided or remote SVG/HTML into `ShopIcon`, `StartIcon`, `EndIcon`, or another `MarkupString` sink. Do not add Material Icons or embed duplicate paths in page markup. Preserve the fragment's view box assumptions and `currentColor` behavior.

## Imagery & static assets

| Asset type | Location | Format |
|---|---|---|
| Product images | Supabase Storage (`products/` bucket) | WebP preferred; supported existing formats retained |
| Category banners | Supabase Storage (`categories/` bucket) | WebP preferred |
| Brand logos | `wwwroot/images/brands/` | SVG |
| Site logo | `wwwroot/images/logo/` | SVG |
| Favicon | `wwwroot/favicon.svg` | SVG |
| Hero images | `wwwroot/images/heroes/` | WebP |
| Static UI graphics | `wwwroot/images/ui/` | SVG / WebP |
| Open Graph image | `wwwroot/images/og/` | PNG |

Reuse `ShopImage` and its presets for product/category imagery. Presets own aspect ratio, object fit, and responsive source behavior; do not replace them with arbitrary dimensions or change existing stored assets during this UI migration. Native images outside that component need intrinsic dimensions or a reserved aspect-ratio container to prevent layout shift.

Meaningful alternative text is resource-backed, with product data interpolated where appropriate. Decorative images have empty alt text. Preserve responsive sources, loading priorities, image-error fallbacks, and meaningful placeholder labels; lazy-load below-the-fold images, not the page's important initial image.

## Transitional vendor theme

Unconverted MudBlazor components may continue using their existing `ShopTheme`, providers, and C# values until their replacements are verified. This is a temporary compatibility bridge, not guidance for new native components. Remove bridges with their last consumer; do not prematurely remove providers, assets, or registrations that still serve working screens.

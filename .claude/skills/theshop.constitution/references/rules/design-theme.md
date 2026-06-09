# Design — Theme (applying color, typography, icons, imagery)

> Implementation guide for Rules 15, 16, 18, 19 from `SKILL.md`. Covers **applying** the theme in pages and components — the color priority hierarchy with the full MudBlazor class families, the typography utility classes, icon usage, and the imagery pattern. For **building** the `Shop`-prefixed theme classes (`ShopColors` / `ShopIcons` / `ShopTypography` token registries + the `ShopTheme` MudBlazor wiring), see `design-theme-setup.md`. The rules themselves live in `SKILL.md`; this file does not restate them.

---

## Applying color (Rule 15)

Rule 15 sets the priority; this section spells out the MudBlazor class families so you can pick the right one. The `Color` enum values and Mud classes are wired through `ShopTheme` to the `ShopColors` tokens (see `design-theme-setup.md`).

### Step 1 — `Color` enum parameter

Most MudBlazor components accept `Color`. Use it first.

```razor
<MudButton Color="Color.Primary" Variant="Variant.Filled">Save</MudButton>
<MudIcon Color="Color.Secondary" Icon="@ShopIcons.Cart" />
<MudChip Color="Color.Tertiary">New</MudChip>
<MudProgressCircular Color="Color.Primary" />
<MudAlert Severity="Severity.Success">Saved</MudAlert>
```

Available values: `Default`, `Primary`, `Secondary`, `Tertiary`, `Info`, `Success`, `Warning`, `Error`, `Dark`, `Inherit`, `Surface`, `Transparent`. These are wired through `ShopTheme` to your `ShopColors` values.

### Step 2 — most-specific auto-generated MudBlazor class

When the `Color` enum doesn't fit (custom `MudPaper` styling, a plain `<div>`, a property that doesn't accept the enum), pick the **most specific** family that matches the facet you need:

| Family | Use for | Examples |
|---|---|---|
| `mud-theme-{name}` | matched bg + contrasting text | `mud-theme-primary`, `mud-theme-secondary`, `mud-theme-tertiary`, `mud-theme-info`, `mud-theme-success`, `mud-theme-warning`, `mud-theme-error`, `mud-theme-dark`, `mud-theme-surface` |
| `mud-{name}-text` | text colour only | `mud-primary-text`, `mud-secondary-text`, `mud-error-text` |
| `mud-{name}-bg` / `mud-bg-{name}` | background only | `mud-primary-bg`, `mud-error-bg` |
| `mud-{name}-hover` | hover-state colour | `mud-primary-hover` |
| `mud-border-{name}` | border colour | `mud-border-primary`, `mud-border-lines-default` |
| `mud-icon-{name}` | icon-specific palette | `mud-icon-default`, `mud-icon-primary` |
| `mud-text-{slot}` | palette text shortcuts | `mud-text-primary`, `mud-text-secondary`, `mud-text-disabled` |
| Lighten/darken variants | where MudBlazor emits them | per the SCSS source |

The canonical source: [`MudBlazor/Styles/abstracts/_colors.scss`](https://github.com/MudBlazor/MudBlazor/blob/dev/src/MudBlazor/Styles/abstracts/_colors.scss).

```razor
<MudPaper Class="mud-theme-secondary">Themed paper (bg + text)</MudPaper>
<MudText Class="mud-error-text">Validation failed</MudText>
<div class="mud-border-primary">Bordered block</div>
```

**Pick the most specific.** If you only need text, use `mud-{name}-text` — not `mud-theme-{name}`. `mud-theme-*` is for the matched background + foreground pair, nothing else.

### Step 3 — ask user (last resort)

When neither the `Color` enum nor any MudBlazor auto-generated class can produce the result, describe the limitation and propose alternatives. Proceed only with the user's chosen approach. Never silently hardcode a hex.

---

## Icons — custom SVG only (Rule 19)

This project uses **custom SVG icons only**. Material Design icons are not used. Every icon comes from the `ShopIcons` registry and is referenced by its **semantic** name. To add a new icon constant to the registry, see `design-theme-setup.md` §ShopIcons.

```razor
@* ✅ *@
<MudIcon Icon="@ShopIcons.Cart" />
<MudButton StartIcon="@ShopIcons.CartAdd">Add to Cart</MudButton>
<MudIconButton Icon="@ShopIcons.Close" OnClick="@Close" />

@* ❌ Inline SVG path *@
<MudIcon Icon="<path d='M12 2L2 7v10c0...'/>" />

@* ❌ Material Icons reference *@
<MudIcon Icon="@Icons.Material.Filled.ShoppingCart" />
```

**Why no Material Icons:** This project uses a custom icon set designed for the brand. Mixing Material with custom creates visual inconsistency.

---

## Typography usage (Rule 16, Rule 18)

The per-`Typo` sizes/weights are defined in `ShopTypography` and wired via `ShopTheme` (see `design-theme-setup.md`); this section covers how to apply them in markup.

### Step 1 — pick a `Typo`

```razor
<MudText Typo="Typo.h1">Premium Disposables</MudText>
<MudText Typo="Typo.h4">Product Name</MudText>
<MudText Typo="Typo.body1">Description text</MudText>
<MudText Typo="Typo.caption">Footnote text</MudText>
<MudText Typo="Typo.overline">CATEGORY</MudText>
<MudText Typo="Typo.subtitle1">Section subtitle</MudText>
```

### Combine `Typo` with `Color`

```razor
<MudText Typo="Typo.h2" Color="Color.Primary">Featured</MudText>
<MudText Typo="Typo.body2" Color="Color.Secondary">Subtitle text</MudText>
<MudText Typo="Typo.caption" Color="Color.Error">Error message</MudText>
```

### Step 2 — off-spec sizes/weights via `fs-*` / `fw-*` (Rule 18)

If the design needs a font size or weight that no `Typo` value cleanly produces, **do not** inline-style and **do not** invent a one-off CSS class. Compose with utilities from `Styles/abstracts/_typography.scss`:

- `fs-{n}` — font-size in px. Already-generated: `$font-sizes: (10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 34, 48, 60, 96)`.
- `fw-{n}` — font-weight. Already-generated: `$font-weights: (400, 500, 600, 700)`.

```razor
<MudText Typo="Typo.h4" Class="fs-22 fw-600">Off-spec heading</MudText>
<MudText Typo="Typo.body1" Class="fw-500">Emphasized body</MudText>
```

To add a new size/weight: add it to the `$font-sizes` or `$font-weights` list in `_typography.scss` — the `@each` loop generates the class. **Never hand-write a `.fs-{n}` selector.**

### Need a structural new typography variant (font family, line-height)?

**Stop and ask the user first** (Rule 14 applies to typography too). Do not introduce custom CSS classes or inline styles for structural typography changes.

---

## Imagery & static assets

| Asset type | Location | Format |
|---|---|---|
| Product images | Supabase Storage (`products/` bucket) | WebP preferred, JPG fallback |
| Category banners | Supabase Storage (`categories/` bucket) | WebP, 1920×600 |
| Brand logos | `wwwroot/images/brands/` | SVG |
| Site logo | `wwwroot/images/logo/` | SVG |
| Favicon | `wwwroot/favicon.svg` | SVG |
| Hero images | `wwwroot/images/heroes/` | WebP |
| Static UI graphics | `wwwroot/images/ui/` | SVG / WebP |
| Open Graph image | `wwwroot/images/og/` | PNG, 1200×630 |

Requirements:
- **Always WebP** for raster images (smaller, same quality). JPG fallback only if needed.
- **Always set `width` and `height`** to prevent layout shift.
- **Alt text from resources only** — never hardcoded (Rule 11 applies).
- **`loading="lazy"`** for images below the fold.

```razor
<MudImage Src="@_product.ImageUrl"
          Alt="@string.Format(Strings.Product_ImageAlt, _product.Name)"
          Width="400"
          Height="400"
          ObjectFit="ObjectFit.Cover" />
```

**Product image guidelines:** 1:1 aspect ratio for catalog, min 800×800, white or transparent background. File naming: `{product-slug}-{variant}-{angle}.webp` (e.g. `northdrift-disposable-blue-front.webp`).

---

## Common mistakes

| Mistake | Fix |
|---|---|
| `<MudButton Style="background: #101010">` | `<MudButton Color="Color.Primary">` |
| `<MudText Class="mud-theme-error">Error</MudText>` (only wanted text colour) | `<MudText Class="mud-error-text">Error</MudText>` — pick the most specific class |
| `<MudIcon Icon="@Icons.Material.Filled.ShoppingCart" />` | `<MudIcon Icon="@ShopIcons.Cart" />` |
| `<span>Footnote</span>` | `<MudText Typo="Typo.caption">Footnote</MudText>` |
| `<MudText Style="font-size: 22px; font-weight: 600;">` | `<MudText Typo="Typo.h4" Class="fs-22 fw-600">` — extend the SCSS `$list` if 22/600 aren't already generated |
| Hand-writing `.fs-22 { font-size: 22px; }` | Add `22` to `$font-sizes` and let `@each` generate it |
| Hardcoded `alt="Product image"` | `Alt="@string.Format(Strings.Product_ImageAlt, _product.Name)"` |

> Creating or renaming a `Shop*` theme class? See `design-theme-setup.md` for the `Shop` prefix convention (Rule 13) and registry shapes.

# Design — Theme (Colors, Icons, Typography, MudBlazor wiring)

> Implementation guide for Rules 13, 15, 16, 18, 19 from `SKILL.md`. Covers the `Shop`-prefixed theme classes (`ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`), the color priority hierarchy with the full MudBlazor class families, the typography utility classes, and the imagery pattern. The rules themselves live in `SKILL.md`; this file does not restate them.

---

## `Shop` prefix convention (Rule 13)

- Uppercase `S`: `ShopColors`, not `shopColors` or `SHOP_COLORS`.
- Singular concept + plural collection: `ShopColors`, not `ShopColor`.
- Place under `src/TheShop.Web/Theme/`.
- One class per concept, one file per class.
- `static` class for token registries; instance class only for `ShopTheme` (which builds the MudBlazor theme object).

| Concept | Class | Type |
|---|---|---|
| Color tokens | `ShopColors` | static |
| Icon registry | `ShopIcons` | static |
| Typography tokens | `ShopTypography` | static |
| MudBlazor theme | `ShopTheme` | instance |

```csharp
// ✅
public static class ShopColors { }
public static class ShopIcons { }
public class ShopTheme { }            // instance — produces MudTheme

// ❌
public static class Colors { }         // collides with MudBlazor
public static class AppColors { }      // doesn't match convention
public static class Color { }          // singular
```

---

## `ShopColors` — token registry

```csharp
namespace TheShop.Web.Theme;

public static class ShopColors
{
    // Brand
    public const string Primary = "#101010";        // confirmed
    public const string Secondary = "";             // TODO: from Figma
    public const string Tertiary = "";              // TODO: from Figma
    public const string Accent = "";                // TODO: from Figma

    // Backgrounds
    public const string Background = "";            // TODO: from Figma
    public const string Surface = "";               // TODO: from Figma

    // Text
    public const string TextPrimary = "";           // TODO: from Figma
    public const string TextSecondary = "";         // TODO: from Figma
    public const string TextDisabled = "";          // TODO: from Figma

    // Borders
    public const string BorderPrimary = "";         // TODO: from Figma
    public const string BorderSecondary = "";       // TODO: from Figma

    // Semantic
    public const string Success = "";               // TODO: from Figma
    public const string Warning = "";               // TODO: from Figma
    public const string Error = "";                 // TODO: from Figma
    public const string Info = "";                  // TODO: from Figma

    // Dark mode
    public static class Dark
    {
        public const string Background = "";        // TODO: from Figma
        public const string Surface = "";           // TODO: from Figma
        public const string TextPrimary = "";       // TODO: from Figma
        public const string TextSecondary = "";     // TODO: from Figma
    }
}
```

---

## Applying color (Rule 15)

Rule 15 sets the priority; this section spells out the MudBlazor class families so you can pick the right one.

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

## `ShopIcons` — custom SVG only (Rule 19)

This project uses **custom SVG icons only**. Material Design icons are not used.

```csharp
namespace TheShop.Web.Theme;

public static class ShopIcons
{
    // Custom SVG icon paths. Each constant holds the SVG <path d="..."/> markup.
    //
    // Adding an icon:
    //   1. Get the SVG file from your designer / export from Figma
    //   2. Extract the <path d="..."/> markup
    //   3. Add it as a new constant with a SEMANTIC name (Cart, not ShoppingBag)
    //
    // Semantic names (Cart, Login) survive icon swaps. Visual names (ShoppingBag, Door) don't.
}
```

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

## `ShopTypography` — token registry

```csharp
namespace TheShop.Web.Theme;

public static class ShopTypography
{
    // TODO: Update all values from the Figma typography styles.
    // Values are wired into MudBlazor via ShopTheme so <MudText Typo="Typo.h1"> picks up H1 automatically.

    public const string FontFamilyPrimary = "";     // TODO: from Figma
    public const string FontFamilyHeading = "";     // TODO: from Figma (or same as Primary)

    public const string WeightLight = "300";
    public const string WeightRegular = "400";
    public const string WeightMedium = "500";
    public const string WeightSemibold = "600";
    public const string WeightBold = "700";

    // Per-Typo tokens — H1..H6, Body1..2, Subtitle1..2, Caption, Overline, Button
    public const string H1_Size = "";
    public const string H1_Weight = "";
    public const string H1_LineHeight = "";
    // ... etc.
}
```

Available `Typo` values: `h1`, `h2`, `h3`, `h4`, `h5`, `h6`, `subtitle1`, `subtitle2`, `body1`, `body2`, `button`, `caption`, `overline`, `inherit`.

---

## Typography usage (Rule 16, Rule 18)

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

## `ShopTheme` — wires the tokens into MudBlazor

```csharp
namespace TheShop.Web.Theme;
using MudBlazor;

public class ShopTheme
{
    public MudTheme BuildTheme() => new()
    {
        PaletteLight = BuildLightPalette(),
        PaletteDark = BuildDarkPalette(),
        Typography = BuildTypography(),
        LayoutProperties = BuildLayout(),
    };

    private PaletteLight BuildLightPalette() => new()
    {
        Primary = ShopColors.Primary,
        Secondary = ShopColors.Secondary,
        Tertiary = ShopColors.Tertiary,
        Background = ShopColors.Background,
        Surface = ShopColors.Surface,
        AppbarBackground = ShopColors.Background,
        DrawerBackground = ShopColors.Surface,
        TextPrimary = ShopColors.TextPrimary,
        TextSecondary = ShopColors.TextSecondary,
        Success = ShopColors.Success,
        Warning = ShopColors.Warning,
        Error = ShopColors.Error,
        Info = ShopColors.Info,
        LinesDefault = ShopColors.BorderSecondary,
        LinesInputs = ShopColors.BorderSecondary,
    };

    private PaletteDark BuildDarkPalette() => new()
    {
        Primary = ShopColors.Primary,
        Background = ShopColors.Dark.Background,
        Surface = ShopColors.Dark.Surface,
        TextPrimary = ShopColors.Dark.TextPrimary,
        TextSecondary = ShopColors.Dark.TextSecondary,
    };

    private Typography BuildTypography() => new()
    {
        Default = new DefaultTypography
        {
            FontFamily = [ShopTypography.FontFamilyPrimary],
            FontSize = ShopTypography.Body1_Size,
            LineHeight = ShopTypography.Body1_LineHeight,
            FontWeight = ShopTypography.WeightRegular,
        },
        H1 = new H1Typography { FontSize = ShopTypography.H1_Size, FontWeight = ShopTypography.H1_Weight, LineHeight = ShopTypography.H1_LineHeight },
        H2 = new H2Typography { FontSize = ShopTypography.H2_Size, FontWeight = ShopTypography.H2_Weight, LineHeight = ShopTypography.H2_LineHeight },
        H3 = new H3Typography { FontSize = ShopTypography.H3_Size, FontWeight = ShopTypography.H3_Weight, LineHeight = ShopTypography.H3_LineHeight },
        H4 = new H4Typography { FontSize = ShopTypography.H4_Size, FontWeight = ShopTypography.H4_Weight, LineHeight = ShopTypography.H4_LineHeight },
        H5 = new H5Typography { FontSize = ShopTypography.H5_Size, FontWeight = ShopTypography.H5_Weight, LineHeight = ShopTypography.H5_LineHeight },
        H6 = new H6Typography { FontSize = ShopTypography.H6_Size, FontWeight = ShopTypography.H6_Weight, LineHeight = ShopTypography.H6_LineHeight },
        Body1 = new Body1Typography { FontSize = ShopTypography.Body1_Size, FontWeight = ShopTypography.Body1_Weight, LineHeight = ShopTypography.Body1_LineHeight },
        Body2 = new Body2Typography { FontSize = ShopTypography.Body2_Size, FontWeight = ShopTypography.Body2_Weight, LineHeight = ShopTypography.Body2_LineHeight },
        Button = new ButtonTypography { FontSize = ShopTypography.Button_Size, FontWeight = ShopTypography.WeightMedium, TextTransform = "none" },
    };

    private LayoutProperties BuildLayout() => new()
    {
        DefaultBorderRadius = "8px",
        AppbarHeight = "64px",
    };
}
```

Registered in DI and consumed by `MainLayout.razor`:

```razor
@inject ShopTheme Theme

<MudThemeProvider Theme="@Theme.BuildTheme()" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    @* layout content *@
</MudLayout>
```

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
| Icon named `ShoppingBag` (visual) | Rename to `Cart` (semantic) — survives icon swaps |
| `<span>Footnote</span>` | `<MudText Typo="Typo.caption">Footnote</MudText>` |
| `<MudText Style="font-size: 22px; font-weight: 600;">` | `<MudText Typo="Typo.h4" Class="fs-22 fw-600">` — extend the SCSS `$list` if 22/600 aren't already generated |
| Hand-writing `.fs-22 { font-size: 22px; }` | Add `22` to `$font-sizes` and let `@each` generate it |
| Hardcoded `alt="Product image"` | `Alt="@string.Format(Strings.Product_ImageAlt, _product.Name)"` |
| `public static class AppColors { }` | `public static class ShopColors { }` (Rule 13) |

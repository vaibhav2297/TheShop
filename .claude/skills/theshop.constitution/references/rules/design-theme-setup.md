# Design — Theme Setup (building the `Shop*` theme classes)

> Implementation guide for Rule 13 from `SKILL.md`, plus the registry/wiring shape behind Rules 15, 16, 18, 19. Covers **building** the `Shop`-prefixed theme classes — `ShopColors`, `ShopIcons`, `ShopTypography` (token registries) and `ShopTheme` (MudBlazor wiring). Load this **only** when creating or editing files under `src/TheShop.Web/Theme/`. For **applying** the theme in pages and components (color priority, typography utilities, icon usage, imagery), see `design-theme.md`. The rules themselves live in `SKILL.md`; this file does not restate them.

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

## `ShopIcons` — registry (custom SVG only, Rule 19)

This project uses **custom SVG icons only**. Material Design icons are not used. Each constant holds the SVG `<path d="..."/>` markup, named by **semantics** (`Cart`), never visuals (`ShoppingBag`).

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

For how to reference these constants in markup (`<MudIcon Icon="@ShopIcons.Cart" />`), see `design-theme.md` §Icons.

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

## Common mistakes (setup)

| Mistake | Fix |
|---|---|
| `public static class AppColors { }` | `public static class ShopColors { }` (Rule 13) |
| `public static class Colors { }` | Collides with MudBlazor — use `ShopColors` |
| Icon constant named `ShoppingBag` (visual) | Rename to `Cart` (semantic) — survives icon swaps (Rule 19) |
| Editing `Strings.Designer.cs`-style generated theme output by hand | Theme classes are hand-authored registries — edit `ShopColors` / `ShopTypography` source directly |

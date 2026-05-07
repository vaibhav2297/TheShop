# Vape E-commerce — Design System & Resource Conventions

> **Audience:** AI coding agent (Claude, Copilot, Cursor, etc.)
> **Project:** The Vape Shop Sarnia
> **Companion file:** `ARCHITECTURE.md` (architecture rules)

This document is the canonical reference for all visual, theming, and resource concerns. **Read this entire file before generating any UI code.** Any code that hardcodes strings, colors, icons, or typography values must be flagged and refactored.

For architecture, layer separation, and code structure rules, see `ARCHITECTURE.md`.

---

## Table of Contents

1. [The Non-Negotiable Rules](#the-non-negotiable-rules)
2. [Naming Convention — The `Shop` Prefix](#naming-convention--the-shop-prefix)
3. [String Resources (Localization)](#string-resources-localization)
4. [Colors — `ShopColors`](#colors--shopcolors)
5. [Icons — `ShopIcons`](#icons--shopicons)
6. [Typography — `ShopTypography`](#typography--shoptypography)
7. [MudBlazor Theme — `ShopTheme`](#mudblazor-theme--shoptheme)
8. [Imagery & Static Assets](#imagery--static-assets)
9. [Component Design Rules](#component-design-rules)
10. [Anti-Patterns to Reject](#anti-patterns-to-reject)
11. [Design Checklist](#design-checklist)

---

## The Non-Negotiable Rules

### Rule 1 — No hardcoded user-facing strings

**Every** string a user reads must come from `Strings.resx` via `IStringLocalizer<Strings>`. This includes:

- Page titles, headings, body text
- Button labels and link text
- Form field labels, placeholders, helper text
- Validation messages
- Toast/snackbar messages
- Modal/dialog titles and content
- Empty state messages
- Loading text
- Error messages
- ARIA labels and accessibility text
- Tooltip content
- Email subject lines and body templates
- Meta tags and SEO descriptions

**Exempt:** Constants (`API_VERSION = "v1"`), log messages, exception messages thrown internally (caught and converted to resource keys), test assertions.

### Rule 2 — All theme classes use the `Shop` prefix

Every project-specific theme/design class begins with `Shop`. This prevents collision with MudBlazor's built-in types (`Colors`, `Icons`, `Typography`) and makes project-specific tokens immediately identifiable in code reviews.

### Rule 3 — MudBlazor components only

All UI must be built using MudBlazor components. Never invent custom buttons, inputs, cards, dialogs, or any other UI primitive when MudBlazor provides an equivalent.

**If MudBlazor cannot meet a specific design requirement:**
1. **Stop.** Do not implement a custom alternative.
2. **Ask the user first.** Describe the limitation clearly and propose at least one possible alternative approach (e.g. "MudBlazor's `MudCarousel` doesn't support X. We could either compose it from `MudPaper` + `MudButton`, or use a third-party library like Y. Which would you prefer?").
3. **Wait for explicit confirmation.** Only proceed with the user's chosen approach.

This rule prevents inconsistent UI patterns from creeping into the codebase and ensures every design decision is intentional.

### Rule 4 — Color usage hierarchy

When applying color to any MudBlazor component, follow this strict order of preference:

1. **First — use the `Color` enum parameter on the MudBlazor component.**
   ```razor
   <MudButton Color="Color.Primary">Save</MudButton>
   <MudIcon Color="Color.Secondary" Icon="@ShopIcons.Cart" />
   ```

2. **Second — if the `Color` enum doesn't have what you need, use MudBlazor's generated CSS theme classes.**
   ```razor
   <div class="mud-theme-primary">...</div>
   <MudPaper Class="mud-theme-secondary">...</MudPaper>
   ```

3. **Last resort — Ask User.** Only when neither the `Color` enum nor the `mud-theme-*` CSS classes can produce the required result. Ask user with detailed expaination why and proceed with user choice.

**Never hardcode hex values directly in `.razor` files.**

### Rule 5 — Typography rules

All text rendering must use `MudText` with the `Typo` parameter. Never use `<span>`, `<p>`, `<h1>`, or any other native HTML text element for displaying content.

```razor
@* ✅ Good *@
<MudText Typo="Typo.h4">Product Name</MudText>
<MudText Typo="Typo.body1">Description here</MudText>
<MudText Typo="Typo.caption">Small print</MudText>

@* ❌ Bad *@
<span>Product Name</span>
<h4>Product Name</h4>
<p style="font-size: 14px">Description</p>
```

**Exception:** Native HTML elements are acceptable inside MudBlazor components where required by the component's API (e.g. when building a custom child template).

---

## Naming Convention — The `Shop` Prefix

### Rules
- Always uppercase `S`: `ShopColors` (not `shopColors` or `SHOP_COLORS`)
- Always singular concept + plural collection: `ShopColors` not `ShopColor`
- Place in `TheShop.Web/Theme/` folder
- One class per concept, one file per class
- Use `static` classes for token-only registries (no instance state)
- Use instance class only for `ShopTheme` (which builds the MudBlazor theme object)

### Theme classes in this project

| Concept | Class name | Type |
|---|---|---|
| Color tokens | `ShopColors` | static |
| Icon registry | `ShopIcons` | static |
| Typography styles | `ShopTypography` | static |
| MudBlazor theme | `ShopTheme` | instance |

### Example

```csharp
// ✅ Good
public static class ShopColors { }
public static class ShopIcons { }
public class ShopTheme { }            // instance — produces MudTheme

// ❌ Bad
public static class Colors { }         // collides with MudBlazor
public static class AppColors { }      // doesn't match convention
public static class Color { }          // singular
```

---

## String Resources (Localization)

### File structure

```
TheShop.Web/Resources/
├── Strings.resx              // Default (English)
└── Strings.fr.resx           // French translations
```

A single `Strings.resx` file holds all string resources for the entire application. Use clear, scoped key names to avoid collisions (see naming convention below).

### Why French resources from day one

This is a Canadian e-commerce business. Quebec's Bill 96 strengthens French-language requirements for businesses operating in Quebec. Even if French translations are added later, **scaffold the file structure now** so adding `Strings.fr.resx` later is a content task, not an architectural one.

### Naming conventions for resource keys

Use a `{Context}_{Purpose}` pattern to keep keys scoped and discoverable:

| Type | Pattern | Example |
|---|---|---|
| Page title | `{Page}_PageTitle` | `ProductDetail_PageTitle`, `Cart_PageTitle` |
| Button | `{Action}` or `{Page}_{Action}` | `AddToCart`, `Save`, `Checkout_PlaceOrder` |
| Label | `{Field}_Label` | `Email_Label`, `Password_Label` |
| Placeholder | `{Field}_Placeholder` | `Email_Placeholder` |
| Helper text | `{Field}_Hint` | `Password_Hint` |
| Error/validation | `{Field}_{Rule}` | `Email_Required`, `Password_TooShort` |
| Empty state | `Empty_{Context}` | `Empty_Cart`, `Empty_NoResults` |
| Toast success | `{Action}_Success` | `AddedToCart`, `OrderPlaced` |
| Toast error | `{Action}_Failed` | `AddToCart_Failed` |
| Confirmation | `Confirm_{Action}` | `Confirm_Delete`, `Confirm_Logout` |
| Common/shared | (single word) | `Cancel`, `Save`, `Continue` |

### Usage in pages

```razor
@page "/products/{Slug}"
@inject IStringLocalizer<Strings> Localizer

<PageTitle>@Localizer["ProductDetail_PageTitle"]</PageTitle>

<MudButton OnClick="@AddToCart">
    @Localizer["AddToCart"]
</MudButton>

<MudTextField Label="@Localizer["Email_Label"]" 
              Placeholder="@Localizer["Email_Placeholder"]"
              HelperText="@Localizer["Email_Hint"]" />
```

### Usage with placeholders

```xml
<!-- Strings.resx -->
<data name="Product_StockWarning">
    <value>Only {0} left in stock</value>
</data>
```

```razor
<MudAlert>@string.Format(Localizer["Product_StockWarning"], _product.Stock)</MudAlert>
```

### Application layer error keys

The Application layer returns resource KEYS (not English text) in `Result.Fail()`:

```csharp
// Application layer
return Result.Fail<CartDto>("ProductNotFound");  // resource KEY
```

```razor
@* Web layer translates the key *@
@if (!result.IsSuccess)
{
    <MudAlert Severity="Severity.Error">@Localizer[result.Error]</MudAlert>
}
```

This keeps the Application layer language-agnostic and testable.

---

## Colors — `ShopColors`

### Definition

```csharp
namespace TheShop.Web.Theme;

public static class ShopColors
{
    // ============================================================
    // TODO: Update all values from the Figma color styles.
    // The values below are placeholders — replace with the exact
    // hex codes defined in the project's Figma file.
    // Only "Primary" is finalized per the Design Guidelines doc.
    // ============================================================

    // Brand
    public const string Primary = "#101010";        // confirmed from Design Guidelines
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

    // Dark mode (if applicable)
    public static class Dark
    {
        public const string Background = "";        // TODO: from Figma
        public const string Surface = "";           // TODO: from Figma
        public const string TextPrimary = "";       // TODO: from Figma
        public const string TextSecondary = "";     // TODO: from Figma
    }
}
```

### Color usage rules (priority order)

When you need to apply color to any MudBlazor component, **follow this order strictly**:

#### 1. First choice — `Color` enum parameter

Most MudBlazor components accept a `Color` parameter that uses the `MudBlazor.Color` enum. This is the cleanest, most idiomatic approach.

```razor
@* ✅ Best — uses Color enum *@
<MudButton Color="Color.Primary" Variant="Variant.Filled">Save</MudButton>
<MudIcon Color="Color.Secondary" Icon="@ShopIcons.Cart" />
<MudChip Color="Color.Tertiary">New</MudChip>
<MudProgressCircular Color="Color.Primary" />
<MudAlert Severity="Severity.Success">Saved</MudAlert>
```

Available `Color` enum values: `Default`, `Primary`, `Secondary`, `Tertiary`, `Info`, `Success`, `Warning`, `Error`, `Dark`, `Inherit`, `Surface`, `Transparent`.

These are wired to your `ShopColors` values inside `ShopTheme.cs` — so when MudBlazor renders `Color.Primary`, it uses your brand's primary color automatically.

#### 2. Second choice — MudBlazor's generated CSS theme classes

When the `Color` enum doesn't fit (e.g. styling a `MudPaper`, `MudStack`, custom div, or applying color to a property that doesn't accept a `Color` enum), use MudBlazor's auto-generated CSS classes:

```razor
@* ✅ Good — uses generated theme CSS classes *@
<div class="mud-theme-primary">Primary background with contrasting text</div>
<MudPaper Class="mud-theme-secondary">Secondary themed paper</MudPaper>
<MudStack Class="mud-theme-tertiary">...</MudStack>
```

Available CSS classes generated by MudBlazor from your theme:
- `mud-theme-primary`
- `mud-theme-secondary`
- `mud-theme-tertiary`
- `mud-theme-info`
- `mud-theme-success`
- `mud-theme-warning`
- `mud-theme-error`
- `mud-theme-dark`
- `mud-theme-surface`

These classes apply both background color and contrasting text color appropriately for the theme value.

#### 3. Last resort — Ask User.

Describe the limitation clearly and Ask the user for the alternative. Only proceed with the user's chosen approach.

### What never to do

```razor
@* ❌ NEVER — hardcoded hex value *@
<MudButton Style="background: #101010">Save</MudButton>

@* ❌ NEVER — hardcoded named color *@
<div style="color: black">Text</div>

@* ❌ NEVER — using MudBlazor's MudColor() with a hex string *@
<MudPaper Style="background-color: #101010">...</MudPaper>
```

---

## Icons — `ShopIcons`

### Definition

This project uses **custom SVG icons only**. Material Design icons are not used.

```csharp
namespace TheShop.Web.Theme;

public static class ShopIcons
{
    // ============================================================
    // Custom SVG icon paths.
    // Each constant holds the SVG <path d="..."/> string content
    // (or full SVG markup) that will be rendered by MudIcon.
    //
    // To add an icon:
    //   1. Get the SVG file from your designer
    //   2. Extract the <path d="..."/> markup
    //   3. Add it as a new constant below with a semantic name
    //
    // Use semantic names (Cart, Login) not visual names (ShoppingCart, Door).
    // ============================================================


}
```

### Usage rules

```razor
@* ✅ Good — uses ShopIcons registry *@
<MudIcon Icon="@ShopIcons.Cart" />
<MudButton StartIcon="@ShopIcons.CartAdd">Add to Cart</MudButton>
<MudIconButton Icon="@ShopIcons.Close" OnClick="@Close" />

@* ❌ Bad — hardcoded SVG path inline *@
<MudIcon Icon="<path d='M12 2L2 7v10c0...'/>" />

@* ❌ Bad — direct Material icon reference *@
<MudIcon Icon="@Icons.Material.Filled.ShoppingCart" />
```

### Adding a new icon

1. Get the SVG markup from your designer (or export from Figma)
2. Open `ShopIcons.cs`
3. Add a new constant with a **semantic name** (what the icon represents) not a **visual name** (what it looks like)
   - ✅ `Cart` (semantic) — can be swapped to a different shopping icon later
   - ❌ `ShoppingBag` (visual) — locks you into one specific look
4. Place the constant under the appropriate section comment
5. Paste the SVG path string (or full SVG content) as the value

### Why no Material Icons

This project uses a custom icon set designed specifically for the brand. Mixing Material Design icons with custom icons creates visual inconsistency. All icons must come through `ShopIcons` so the project maintains a unified iconographic style.

---

## Typography — `ShopTypography`

### Definition

```csharp
namespace TheShop.Web.Theme;

public static class ShopTypography
{
    // ============================================================
    // TODO: Update all values from the Figma typography styles.
    // The values below are placeholders — replace with the exact
    // font families, sizes, weights, line heights, and letter
    // spacing defined in the project's Figma text styles.
    //
    // These tokens are wired into MudBlazor via ShopTheme.cs so
    // that <MudText Typo="Typo.h1"> uses the H1 token automatically.
    // ============================================================

    // Font families
    public const string FontFamilyPrimary = "";     // TODO: from Figma
    public const string FontFamilyHeading = "";     // TODO: from Figma (or same as Primary)

    // Font weights (numeric)
    public const string WeightLight = "300";
    public const string WeightRegular = "400";
    public const string WeightMedium = "500";
    public const string WeightSemibold = "600";
    public const string WeightBold = "700";

    // Heading sizes (mapped to MudText Typo levels)
    // TODO: Update sizes/weights/line-heights from Figma
    public const string H1_Size = "";
    public const string H1_Weight = "";
    public const string H1_LineHeight = "";

    public const string H2_Size = "";
    public const string H2_Weight = "";
    public const string H2_LineHeight = "";

    public const string H3_Size = "";
    public const string H3_Weight = "";
    public const string H3_LineHeight = "";

    public const string H4_Size = "";
    public const string H4_Weight = "";
    public const string H4_LineHeight = "";

    public const string H5_Size = "";
    public const string H5_Weight = "";
    public const string H5_LineHeight = "";

    public const string H6_Size = "";
    public const string H6_Weight = "";
    public const string H6_LineHeight = "";

    // Body sizes
    public const string Body1_Size = "";
    public const string Body1_Weight = "";
    public const string Body1_LineHeight = "";

    public const string Body2_Size = "";
    public const string Body2_Weight = "";
    public const string Body2_LineHeight = "";

    // Other
    public const string Subtitle1_Size = "";
    public const string Subtitle2_Size = "";
    public const string Caption_Size = "";
    public const string Overline_Size = "";
    public const string Button_Size = "";
}
```

### Usage rules — always `MudText` with `Typo`

All text rendering uses `<MudText>` with the `Typo` parameter. The typography tokens defined above are wired into MudBlazor via `ShopTheme`, so `MudText Typo="Typo.h1"` automatically applies the correct font family, size, weight, and line height.

```razor
@* ✅ Good — MudText with Typo parameter *@
<MudText Typo="Typo.h1">Premium Disposables</MudText>
<MudText Typo="Typo.h4">Product Name</MudText>
<MudText Typo="Typo.body1">Description text</MudText>
<MudText Typo="Typo.body2">Smaller body text</MudText>
<MudText Typo="Typo.caption">Footnote text</MudText>
<MudText Typo="Typo.overline">CATEGORY</MudText>
<MudText Typo="Typo.subtitle1">Section subtitle</MudText>

@* ❌ Bad — native HTML elements *@
<h1>Premium Disposables</h1>
<p>Description text</p>
<span>Footnote</span>

@* ❌ Bad — inline font styles *@
<MudText Style="font-size: 36px; font-weight: 500;">Title</MudText>
```

Available `Typo` enum values: `h1`, `h2`, `h3`, `h4`, `h5`, `h6`, `subtitle1`, `subtitle2`, `body1`, `body2`, `button`, `caption`, `overline`, `inherit`.

### MudText with color

`MudText` also accepts the `Color` parameter — combine both for full styling control without writing any CSS:

```razor
<MudText Typo="Typo.h2" Color="Color.Primary">Featured</MudText>
<MudText Typo="Typo.body2" Color="Color.Secondary">Subtitle text</MudText>
<MudText Typo="Typo.caption" Color="Color.Error">Error message</MudText>
```

### Adding a new typography variant

If you need a typography style that doesn't fit any of the existing `Typo` values, **stop and ask the user first** (per Rule 3 — MudBlazor components only). Do not introduce custom CSS classes or inline styles.

---

## MudBlazor Theme — `ShopTheme`

### Purpose

Wires `ShopColors` and `ShopTypography` into MudBlazor's theme system so MudBlazor components automatically use your design tokens via `Color="Color.Primary"` and `Typo="Typo.h1"`.

### Definition

```csharp
namespace TheShop.Web.Theme;

using MudBlazor;

public class ShopTheme
{
    public MudTheme BuildTheme()
    {
        return new MudTheme
        {
            PaletteLight = BuildLightPalette(),
            PaletteDark = BuildDarkPalette(),
            Typography = BuildTypography(),
            LayoutProperties = BuildLayout(),
        };
    }

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
            FontFamily = new[] { ShopTypography.FontFamilyPrimary },
            FontSize = ShopTypography.Body1_Size,
            LineHeight = ShopTypography.Body1_LineHeight,
            FontWeight = ShopTypography.WeightRegular,
        },
        H1 = new H1Typography
        {
            FontSize = ShopTypography.H1_Size,
            FontWeight = ShopTypography.H1_Weight,
            LineHeight = ShopTypography.H1_LineHeight,
        },
        H2 = new H2Typography
        {
            FontSize = ShopTypography.H2_Size,
            FontWeight = ShopTypography.H2_Weight,
            LineHeight = ShopTypography.H2_LineHeight,
        },
        H3 = new H3Typography
        {
            FontSize = ShopTypography.H3_Size,
            FontWeight = ShopTypography.H3_Weight,
            LineHeight = ShopTypography.H3_LineHeight,
        },
        H4 = new H4Typography
        {
            FontSize = ShopTypography.H4_Size,
            FontWeight = ShopTypography.H4_Weight,
            LineHeight = ShopTypography.H4_LineHeight,
        },
        H5 = new H5Typography
        {
            FontSize = ShopTypography.H5_Size,
            FontWeight = ShopTypography.H5_Weight,
            LineHeight = ShopTypography.H5_LineHeight,
        },
        H6 = new H6Typography
        {
            FontSize = ShopTypography.H6_Size,
            FontWeight = ShopTypography.H6_Weight,
            LineHeight = ShopTypography.H6_LineHeight,
        },
        Body1 = new Body1Typography
        {
            FontSize = ShopTypography.Body1_Size,
            FontWeight = ShopTypography.Body1_Weight,
            LineHeight = ShopTypography.Body1_LineHeight,
        },
        Body2 = new Body2Typography
        {
            FontSize = ShopTypography.Body2_Size,
            FontWeight = ShopTypography.Body2_Weight,
            LineHeight = ShopTypography.Body2_LineHeight,
        },
        Button = new ButtonTypography
        {
            FontSize = ShopTypography.Button_Size,
            FontWeight = ShopTypography.WeightMedium,
            TextTransform = "none",
        },
    };

    private LayoutProperties BuildLayout() => new()
    {
        DefaultBorderRadius = "8px",
        AppbarHeight = "64px",
    };
}
```

### Registration in Program.cs

```csharp
// Web/DependencyInjection.cs
public static IServiceCollection AddPresentation(this IServiceCollection services)
{
    services.AddSingleton<ShopTheme>();
    return services;
}
```

### Usage in `MainLayout.razor`

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

## Imagery & Static Assets

### File locations

| Asset type | Location | Format |
|---|---|---|
| Product images | Supabase Storage (`products/` bucket) | WebP preferred, JPG fallback |
| Category banners | Supabase Storage (`categories/` bucket) | WebP, 1920x600 |
| Brand logos | `wwwroot/images/brands/` | SVG |
| Site logo | `wwwroot/images/logo/` | SVG |
| Favicon | `wwwroot/favicon.svg` | SVG |
| Hero images | `wwwroot/images/heroes/` | WebP |
| Static UI graphics | `wwwroot/images/ui/` | SVG/WebP |
| Open Graph image | `wwwroot/images/og/` | PNG, 1200x630 |

### Image requirements

- **Always use WebP** for raster images (smaller files, same quality). Provide JPG fallback only if needed.
- **Always set `width` and `height` attributes** on images to prevent layout shift.
- **Always provide `alt` text from resources** — never hardcoded.
- **Always use `loading="lazy"`** for images below the fold.

```razor
@* For images, use MudImage when possible *@
<MudImage Src="@_product.ImageUrl" 
          Alt="@string.Format(Localizer["Product_ImageAlt"], _product.Name)"
          Width="400" 
          Height="400" 
          ObjectFit="ObjectFit.Cover" />
```

### Product image guidelines

- Aspect ratio: square (1:1) for catalog grid
- Minimum resolution: 800x800
- White or transparent background for catalog
- Lifestyle images can be separate gallery items
- File naming: `{product-slug}-{variant}-{angle}.webp` (e.g. `northdrift-disposable-blue-front.webp`)

---

## Component Design Rules

### 1. MudBlazor only
- Use only MudBlazor components — never invent custom buttons, inputs, dialogs, etc.
- If MudBlazor doesn't have a component or cannot meet a requirement, **ask the user first** (per Rule 3) before introducing alternatives.

### 2. Include all states
Every interactive component must visually handle:
- Default
- Hover
- Active/pressed
- Focus (keyboard accessibility)
- Disabled
- Loading (where applicable)

### 3. Component naming convention
`Component / Type / Variant / State`

Examples:
- `Button / Primary / Large / Default`
- `Input / Text / Default / Focused`
- `Card / Product / Default`

Avoid: `Button1`, `NewButton`, `FinalCard`.

### 4. Follow MudBlazor variants
Every component must support its standard variants:
- Buttons: `Variant.Filled`, `Variant.Outlined`, `Variant.Text`
- Sizes: `Size.Small`, `Size.Medium`, `Size.Large`
- Colors: `Color.Primary`, `Color.Secondary`, `Color.Tertiary`

### 5. Build base components first
Before building feature-specific UI, ensure these MudBlazor wrappers exist (only if customization is needed beyond MudBlazor defaults):
- Buttons (with all variants)
- Inputs (text, number, email, password)
- Checkbox / Radio
- Select / Autocomplete
- Card (Product, Order, Generic)
- Modal / Dialog
- Toast / Snackbar (MudBlazor provides)
- Loading spinner / skeleton

If standard MudBlazor components meet your needs, use them directly — don't create unnecessary wrappers.

---

## Anti-Patterns to Reject

### ❌ Hardcoded user-facing strings
```razor
<MudText>Add to Cart</MudText>
<MudButton>Save Changes</MudButton>
<PageTitle>Product Details</PageTitle>
```
**Fix:** Use `IStringLocalizer<Strings>`.

### ❌ Hardcoded hex colors
```razor
<div style="background: #101010">...</div>
<MudButton Style="color: #d4a55c">...</MudButton>
```
**Fix:** Use `Color="Color.Primary"` first; if not possible, `Class="mud-theme-primary"`; last resort, Ask User with a detailed explaination why and proceed with user choice.

### ❌ Native HTML text elements
```razor
<h1>Welcome</h1>
<p>Description</p>
<span>Footnote</span>
```
**Fix:** Use `<MudText Typo="Typo.h1">`, `<MudText Typo="Typo.body1">`, `<MudText Typo="Typo.caption">`.

### ❌ Inline typography styles
```razor
<MudText Style="font-size: 36px; font-weight: 500;">Title</MudText>
```
**Fix:** Use the appropriate `Typo` parameter — those values are defined in `ShopTypography` and applied automatically.

### ❌ Custom UI components when MudBlazor exists
```razor
<button class="my-custom-button">Click me</button>
```
**Fix:** Use `<MudButton>`. If MudBlazor cannot meet the requirement, **stop and ask the user**.

### ❌ Direct Material Icons references
```razor
<MudIcon Icon="@Icons.Material.Filled.ShoppingCart" />
```
**Fix:** Use `ShopIcons.Cart` (project uses custom SVG icons only).

### ❌ Custom CSS classes for one-off styling
```css
/* product-page.css */
.special-button { background: #d4a55c; }
```
**Fix:** Use MudBlazor + `Color`/`Typo` parameters. If truly impossible, ask the user.

### ❌ Missing alt text or hardcoded alt text
```razor
<img src="@product.ImageUrl" alt="Product image" />
```
**Fix:** `Alt="@string.Format(Localizer["Product_ImageAlt"], product.Name)"`.

### ❌ Theme classes without `Shop` prefix
```csharp
public static class AppColors { }
public static class IconLibrary { }
```
**Fix:** Rename to `ShopColors`, `ShopIcons`.

---

## Design Checklist

Before generating or accepting any UI code, verify:

### Strings
- [ ] All user-facing text comes from `IStringLocalizer<Strings>`?
- [ ] Resource keys follow the `{Context}_{Purpose}` naming convention?
- [ ] French resource file (`Strings.fr.resx`) updated alongside English?
- [ ] Application layer returns resource KEYS (not English text)?

### Components
- [ ] Only MudBlazor components used (no custom buttons, inputs, custom HTML)?
- [ ] If MudBlazor cannot meet the requirement, has the user been asked before implementing an alternative?
- [ ] All interactive states handled (hover, focus, disabled)?
- [ ] Component variants follow MudBlazor patterns?

### Colors
- [ ] First preference: `Color="Color.Primary"` (or other `Color` enum) used?
- [ ] Second preference: `Class="mud-theme-primary"` used when `Color` enum not possible?
- [ ] Last resort: Ask User with a comment explaining why?
- [ ] No hardcoded hex values anywhere in `.razor` files?

### Icons
- [ ] All icons come from `ShopIcons`?
- [ ] No direct `Icons.Material.*` references?
- [ ] Icon names are semantic (`Cart`) not visual (`ShoppingCart`)?
- [ ] When adding a new icon, semantic naming convention followed?

### Typography
- [ ] All text uses `<MudText>` with the `Typo` parameter?
- [ ] No `<span>`, `<p>`, `<h1>`-`<h6>`, or other native text elements?
- [ ] No inline `font-size`, `font-weight`, or `line-height` styles?
- [ ] Need a new typography variant? — User asked first?

### Brand
- [ ] All theme classes use the `Shop` prefix?

### Images
- [ ] WebP format used for raster images?
- [ ] `width`, `height`, and `loading="lazy"` attributes set?
- [ ] Alt text from resources?
- [ ] `MudImage` used where appropriate?

If any answer is "no", stop and refactor before proceeding.

---

## Final Reminders for AI Agents

1. **Hardcoded strings = automatic rejection.** When you see English text in a `.razor` file, refactor it to use `IStringLocalizer<Strings>` before doing anything else.

2. **`Shop` prefix is mandatory.** Any new color, icon, or typography concept goes into a `Shop*` class. No exceptions.

3. **MudBlazor is the only UI library.** If MudBlazor cannot meet a requirement, **stop and ask the user first** — propose alternatives, wait for confirmation, then proceed.

4. **Color preference order is strict:** `Color` enum → `mud-theme-*` class → Ask user (last resort with comment).

5. **Always `MudText` with `Typo`.** Never use `<span>`, `<p>`, `<h1>`, etc. for displaying content.

6. **Generate the resource keys alongside the page.** When you create `ProductDetail.razor`, also add the necessary keys to `Strings.resx` (and `Strings.fr.resx` with the same keys, French translations or `[TODO]` placeholders).

7. **Reference both files.** When making decisions, cite the specific principle from `ARCHITECTURE.md` (architecture) or this `DESIGN.md` (visual/strings).

---

**End of Design System Instructions**

*Last updated: May 2026 · Version 1.0*
*See `ARCHITECTURE.md` for architecture, layer rules, and code structure.*

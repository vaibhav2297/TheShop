# E-commerce — Design System & Resource Conventions

> **Audience:** AI coding agent (Claude, Copilot, Cursor, etc.)
> **Project:** The Shop
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
10. [CSS Classes & Inline Styles](#css-classes--inline-styles)
11. [SCSS Stylesheets](#scss-stylesheets)
12. [Anti-Patterns to Reject](#anti-patterns-to-reject)
13. [Loading & Busy Indicators](#loading--busy-indicators)
14. [Code-Behind Separation](#code-behind-separation)
15. [Final Reminders for AI Agents](#final-reminders-for-ai-agents)
16. [Design Checklist](#design-checklist)

---

## The Non-Negotiable Rules

### Rule 1 — No hardcoded user-facing strings, and no magic-string resource keys

**Every** string a user reads must come from `Strings.resx`. Access keys via the strongly-typed `Strings.*` accessor — never as magic-string indexer arguments. This includes:

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

**The strongly-typed pattern is non-negotiable.** When the resource key is known at compile time, access it as `Strings.AddToCart` — never `Localizer["AddToCart"]`. The magic-string form silently breaks on typos and renames; the typed form fails at compile time. Full rules and examples in §String Resources below.

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

2. **Second — if the `Color` enum doesn't have what you need, use any of MudBlazor's auto-generated color CSS classes.** MudBlazor's `_colors.scss` (see [`MudBlazor/Styles/abstracts/_colors.scss`](https://github.com/MudBlazor/MudBlazor/blob/dev/src/MudBlazor/Styles/abstracts/_colors.scss)) emits a wide family of color utility classes — not just `mud-theme-*`. Pick the one that semantically matches your need. The full set includes (non-exhaustive):
   - `mud-theme-{name}` — themed background + contrasting text (`primary`, `secondary`, `tertiary`, `info`, `success`, `warning`, `error`, `dark`, `surface`)
   - `mud-{name}-text` — text color only (`mud-primary-text`, `mud-secondary-text`, `mud-error-text`, …)
   - `mud-{name}-bg` / `mud-bg-{name}` — background color only
   - `mud-{name}-hover` — hover state color
   - `mud-border-{name}` — border color
   - `mud-icon-default`, `mud-icon-{name}` — icon colors
   - `mud-text-primary`, `mud-text-secondary`, `mud-text-disabled` — palette text shortcuts
   - Lighten/darken variants where MudBlazor emits them
   ```razor
   <MudPaper Class="mud-theme-secondary">Filled secondary surface</MudPaper>
   <MudText Class="mud-error-text">Error message</MudText>
   <div class="mud-border-primary">Bordered block</div>
   ```
   Always prefer a more specific MudBlazor class (e.g. `mud-error-text`) over `mud-theme-*` when you only need one color facet.

3. **Last resort — Ask User.** Only when neither the `Color` enum nor any of MudBlazor's auto-generated color classes can produce the required result. Ask the user with a detailed explanation of why, and proceed only with their chosen approach.

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

**Typography escape hatch — SCSS utility classes.** If a design needs a font size or weight that no `Typo` value cleanly produces, do NOT inline-style and do NOT invent a one-off CSS class. Compose `MudText` with the project's SCSS-generated utility classes from `src/TheShop.Web/Styles/abstracts/_typography.scss`:

- `fs-{n}` — font-size utility (e.g. `fs-12`, `fs-14`, `fs-18`, `fs-24`, `fs-48`). Add new sizes to the `$font-sizes` SCSS list — don't write a bespoke class.
- `fw-{n}` — font-weight utility (e.g. `fw-400`, `fw-500`, `fw-600`, `fw-700`). Add new weights to the `$font-weights` SCSS list.

```razor
@* ✅ Good — Typo + utility classes when Typo alone doesn't fit *@
<MudText Typo="Typo.h4" Class="fs-22 fw-600">Off-spec heading</MudText>

@* ❌ Bad — inline style *@
<MudText Style="font-size: 22px; font-weight: 600;">Off-spec heading</MudText>

@* ❌ Bad — one-off page-scoped class *@
<MudText Class="product-title-22">Off-spec heading</MudText>
```

Still always pick the closest `Typo` first — utility classes are a fine-tune, not a replacement.

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

### `.resx` configuration — required for typed access

The `Strings.resx` file MUST be configured to auto-generate a strongly-typed C# accessor class. In Visual Studio, set these properties on `Strings.resx`:

| Property | Value |
|---|---|
| Build Action | `Embedded resource` |
| Custom Tool | `PublicResXFileCodeGenerator` |
| Custom Tool Namespace | `TheShop.Web.Resources` |

This generates `Strings.Designer.cs` automatically — a static class with one property per resource key. The file regenerates every time you save the `.resx`. Never edit `Strings.Designer.cs` by hand.

```csharp
// Auto-generated by Visual Studio — DO NOT EDIT
namespace TheShop.Web.Resources;

public class Strings
{
    public static string AddToCart =>
        ResourceManager.GetString("AddToCart", Culture);

    public static string ProductDetail_PageTitle =>
        ResourceManager.GetString("ProductDetail_PageTitle", Culture);

    // ...one property per key in the .resx
}
```

Each property returns the localized value for the current culture automatically.

### Why French resources from day one

This is a Canadian e-commerce business. Quebec's Bill 96 strengthens French-language requirements for businesses operating in Quebec. Even if French translations are added later, **scaffold the file structure now** so adding `Strings.fr.resx` later is a content task, not an architectural one.

### Naming conventions for resource keys

Use a `{Context}_{Purpose}` pattern to keep keys scoped and discoverable. Keys must also be valid C# identifiers (no hyphens, no spaces, no leading digits) since they become property names on the auto-generated `Strings` class.

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

### Three access patterns — when to use which

There are exactly three allowed ways to access localized strings. **Magic-string indexer access is never allowed.**

#### Pattern 1 — `Strings.{KeyName}` (preferred for known keys)

The default for all static, compile-time-known keys. Direct property access on the auto-generated class — fully type-safe, no `IStringLocalizer` injection needed, simplest to read.

```razor
@using TheShop.Web.Resources

<PageTitle>@Strings.ProductDetail_PageTitle</PageTitle>

<MudButton OnClick="@AddToCart">
    @Strings.AddToCart
</MudButton>

<MudTextField Label="@Strings.Email_Label"
              Placeholder="@Strings.Email_Placeholder"
              HelperText="@Strings.Email_Hint" />
```

In code-behind:

```csharp
@code {
    private async Task AddToCart()
    {
        var cmd = new AddToCartCommand(_product.Id, _quantity);
        var result = await Mediator.Send(cmd);
        if (result.IsSuccess)
            Snackbar.Add(Strings.AddedToCart, Severity.Success);
    }
}
```

For format strings with placeholders, the typed accessor still works:

```razor
@* Strings.resx — Product_StockWarning = "Only {0} left in stock" *@

<MudAlert>@string.Format(Strings.Product_StockWarning, _product.Stock)</MudAlert>
```

#### Pattern 2 — `Localizer[runtimeKey]` (only for keys determined at runtime)

When the key is unknown at compile time — for example, when the Application layer returns a resource key as a string in `Result.Fail()` — inject `IStringLocalizer<Strings>` and use the indexer with the runtime value. This is the only case where indexer access is allowed.

```razor
@inject IStringLocalizer<Strings> Localizer

@code {
    private async Task AddToCart()
    {
        var result = await Mediator.Send(cmd);
        if (!result.IsSuccess)
        {
            // result.Error is a string like "ProductNotFound" returned from Application
            // We don't know which key it will be at compile time
            Snackbar.Add(Localizer[result.Error], Severity.Error);
        }
    }
}
```

```razor
@if (!result.IsSuccess)
{
    <MudAlert Severity="Severity.Error">@Localizer[result.Error]</MudAlert>
}
```

This is the **only** legitimate use of the indexer. The key is dynamic — passed in from somewhere else — so compile-time checking is impossible. The page still benefits from runtime localization for whatever key the Application layer happens to return.

#### Pattern 3 — `Localizer[nameof(Strings.KeyName)]` (rare — testing or scoped localization)

For tests that want to inject a mocked `IStringLocalizer<Strings>`, or for components that need explicit `IStringLocalizer` for scoped culture switching, use `nameof()` to keep the key compile-time safe while still going through the localizer interface.

```razor
@inject IStringLocalizer<Strings> Localizer

<MudButton>@Localizer[nameof(Strings.AddToCart)]</MudButton>
```

**Use this only when there's a concrete reason to involve `IStringLocalizer`.** For 95% of UI code, Pattern 1 is the right choice. Don't reach for `IStringLocalizer` "just in case" — the typed accessor in Pattern 1 already handles culture switching automatically.

### Application layer error keys

The Application layer returns resource KEYS (not English text) in `Result.Fail()`:

```csharp
// Application layer
return Result.Fail<CartDto>("ProductNotFound");  // resource KEY
```

For Application layer code, use `nameof(Strings.{Key})` to keep the key compile-time safe even on the producer side:

```csharp
// Even better — compile-time-checked key
return Result.Fail<CartDto>(nameof(Strings.ProductNotFound));
```

The Web layer then translates the runtime key via Pattern 2:

```razor
@if (!result.IsSuccess)
{
    <MudAlert Severity="Severity.Error">@Localizer[result.Error]</MudAlert>
}
```

This keeps the Application layer language-agnostic AND compile-time safe.

### Decision summary

| You have... | Use |
|---|---|
| A static, known-at-compile-time key | `@Strings.AddToCart` |
| A runtime key (from `Result.Fail`, dynamic input) | `@Localizer[runtimeKey]` |
| Need to mock localization in tests | `@Localizer[nameof(Strings.AddToCart)]` |
| Want to use a string literal as the key | **Stop. This is forbidden.** |

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

#### 2. Second choice — MudBlazor's auto-generated color CSS classes

When the `Color` enum doesn't fit (e.g. styling a `MudPaper`, `MudStack`, custom div, or applying color to a property that doesn't accept a `Color` enum), reach for one of MudBlazor's auto-generated color CSS classes — and **pick the most specific one** that does what you need.

The full set is emitted by [`MudBlazor/Styles/abstracts/_colors.scss`](https://github.com/MudBlazor/MudBlazor/blob/dev/src/MudBlazor/Styles/abstracts/_colors.scss). Browse that file (or your built CSS) before reaching for `style="..."`. The available families include:

| Family | What it does | Examples |
|---|---|---|
| `mud-theme-{name}` | Background + contrasting text together | `mud-theme-primary`, `mud-theme-secondary`, `mud-theme-tertiary`, `mud-theme-info`, `mud-theme-success`, `mud-theme-warning`, `mud-theme-error`, `mud-theme-dark`, `mud-theme-surface` |
| `mud-{name}-text` | Text color only | `mud-primary-text`, `mud-secondary-text`, `mud-error-text`, `mud-success-text`, … |
| `mud-{name}-bg` / `mud-bg-{name}` | Background color only | `mud-primary-bg`, `mud-error-bg`, … |
| `mud-{name}-hover` | Hover-state color | `mud-primary-hover`, … |
| `mud-border-{name}` | Border color | `mud-border-primary`, `mud-border-lines-default` |
| `mud-icon-{name}` | Icon-specific palette colors | `mud-icon-default`, `mud-icon-primary`, … |
| `mud-text-{slot}` | Palette text shortcuts | `mud-text-primary`, `mud-text-secondary`, `mud-text-disabled` |
| Lighten / darken variants | Where MudBlazor emits them | per the SCSS source |

```razor
@* ✅ Good — full themed surface (bg + text together) *@
<MudPaper Class="mud-theme-secondary">Themed paper</MudPaper>

@* ✅ Better — only need text color, so pick the specific class *@
<MudText Class="mud-error-text">Validation failed</MudText>

@* ✅ Good — only need a border tint *@
<div class="mud-border-primary">Bordered block</div>

@* ❌ Avoid — `mud-theme-*` when you only wanted one color facet *@
<MudText Class="mud-theme-error">Validation failed</MudText>
```

Rule of thumb: if you only need text, use a `*-text` class. If you only need a background, use a `*-bg` class. Reach for `mud-theme-*` only when you actually want the matched background + foreground pair.

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

### Off-spec sizes & weights — SCSS utility classes

If the design needs a size or weight that no `Typo` value cleanly produces, **do not inline-style** and **do not invent a one-off CSS class**. Compose `MudText` with the project's SCSS-generated utility classes from `src/TheShop.Web/Styles/abstracts/_typography.scss`:

- `fs-{n}` — font-size in pixels. Already-generated sizes: see the `$font-sizes` SCSS list (`10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 34, 48, 60, 96`).
- `fw-{n}` — font-weight. Already-generated weights: see the `$font-weights` SCSS list (`400, 500, 600, 700`).

```razor
@* ✅ Good — Typo gives type-family/line-height, utilities fine-tune size/weight *@
<MudText Typo="Typo.h4" Class="fs-22 fw-600">Off-spec heading</MudText>
<MudText Typo="Typo.body1" Class="fw-500">Emphasized body</MudText>

@* ❌ Bad — inline font styles *@
<MudText Style="font-size: 22px; font-weight: 600;">Off-spec heading</MudText>

@* ❌ Bad — one-off page-scoped CSS class *@
<MudText Class="product-title-22">Off-spec heading</MudText>
```

If the size or weight you need is not already in the SCSS list, add it to the `$font-sizes` or `$font-weights` collection in `_typography.scss` (one entry — the loop generates the class). Never hand-write a `.fs-{n}` rule individually.

### Adding a new typography variant

If you need a structural typography style that doesn't fit any of the existing `Typo` values (different font family, line-height, letter-spacing, etc.), **stop and ask the user first** (per Rule 3 — MudBlazor components only). Do not introduce custom CSS classes or inline styles for structural typography changes.

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
          Alt="@string.Format(Strings.Product_ImageAlt, _product.Name)"
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

### Deciding when to extract a reusable component

Before writing a new component, decide whether a component is actually the right unit. The rules in this section (`MudComponentBase`, `Class`/`Style` forwarding, builders) only matter once you've decided extraction is justified. Default to **inline markup** until one of the *extract* triggers fires — and even then, stop if any *avoid* signal is also true.

**Extract a reusable component when:**

- The same UI **and** behavior repeats in two or more places today, and the duplication is non-trivial.
- Design consistency across pages depends on it (e.g. product card, OTP input, page header).
- Logic and markup belong together — extracting just one would split a tight coupling.
- The component has a **single, clearly named** responsibility (`ShopProductCard`, `ShopOtpInput` — not `ShopProductOrCartWidget`).
- The pattern is stable and unlikely to need restructuring in the next handful of changes.
- Reuse demonstrably improves maintenance or readability at the call sites.
- You can give it a clear, specific name without resorting to `Generic*`, `Common*`, `Shared*`, or numeric suffixes.

**Avoid extracting a reusable component when:**

- It's used **only once** today, and you cannot point at a concrete (not hypothetical) second use.
- The only motivation is to reduce lines of code in the page that would have inlined it.
- The abstraction feels forced — you struggle to name it or explain its responsibility in one sentence.
- It would need many parameters / configuration knobs / boolean flags to satisfy its callers (a sign the extraction is hiding two components, not one).
- It handles multiple unrelated responsibilities (mixing presentation, business logic, and side effects).
- Only the **logic** repeats — not the markup. In that case, extract a service, helper method, or `*State` store, not a component.
- The markup is tiny (one or two MudBlazor components in a `MudStack`) and the abstraction adds no meaningful semantics.
- You're future-proofing for a hypothetical need ("we might reuse this on the checkout page someday"). Wait for the second real call site to appear, then extract.

**Rule of thumb:** when in doubt, **inline first, extract on the second real call site.** Per `CLAUDE.md`: *three similar lines is better than a premature abstraction.* This applies double for Razor markup, where the cost of an awkward component (parameter explosion, slot juggling, render-tree churn) is high.

When you do extract a reusable component, the rules below (`MudComponentBase`, `Class`/`Style` forwarding, naming, variants, states) all apply.

### General — applies to every component

#### 1. MudBlazor only
- Use only MudBlazor components — never invent custom buttons, inputs, dialogs, etc.
- If MudBlazor doesn't have a component or cannot meet a requirement, **ask the user first** (per Rule 3) before introducing alternatives.

#### 2. Reusable components must inherit from `MudComponentBase`

Every reusable Blazor component in this project must have `MudBlazor.MudComponentBase` somewhere in its inheritance chain (direct parent or a transitive ancestor — both are acceptable). This is what makes `Class`, `Style`, `UserAttributes`, and other consumer-side overrides available without the component having to re-declare them.

```csharp
// ✅ Good — direct child of MudComponentBase
public partial class ShopProductCard : MudComponentBase { }

// ✅ Good — MudComponentBase is somewhere in the chain
public partial class ShopOrderCard : ShopProductCard { }   // ShopProductCard : MudComponentBase

// ✅ Good — extending an existing Mud component (which itself derives from MudComponentBase)
public partial class ShopBrandedButton : MudButton { }

// ❌ Bad — no MudComponentBase in the hierarchy
public partial class ShopProductCard : ComponentBase { }   // missing Class/Style/UserAttributes plumbing
```

A consumer must always be able to write `<ShopProductCard Class="my-spacing" Style="@(...)" />` and have it work without the component re-declaring pass-through attributes. Inheriting from `MudComponentBase` is what gives you that for free.

#### 3. Reusable components must forward `Class` and `Style` to their root element

`MudComponentBase` exposes `Class` and `Style` as parameters — but it does NOT automatically apply them to your render tree. Every reusable component must explicitly forward those values to its root element. There are two acceptable patterns:

**Pattern A — root element has no internal classes/styles.** Forward `Class` and `Style` directly:

```razor
@* ✅ ShopSection.razor — no internal styling on the root, just pass through *@
<MudGrid Class="@Class"
         Style="@Style">
    <MudPaper>
        @ChildContent
    </MudPaper>
</MudGrid>
```

**Pattern B — root element has its own internal classes/styles.** Compose using `CssBuilder` / `StyleBuilder` and always end the chain with `.AddClass(Class)` / `.AddStyle(Style)` so consumer-supplied values land last and can override:

```razor
@* ✅ ShopAlert.razor *@
<MudGrid Class="@Classname"
         Style="@Stylename">
    <MudPaper>
        @ChildContent
    </MudPaper>
</MudGrid>
```

```csharp
// ✅ ShopAlert.razor.cs
protected string Classname => new CssBuilder("mud-alert")
    .AddClass("mud-dense", Dense)
    .AddClass("mud-square", Square)
    .AddClass(Class)                  // consumer's Class added last
    .Build();

private string Stylename => new StyleBuilder()
    .AddStyle("margin-top", "4px")
    .AddStyle(Style)                  // consumer's Style added last
    .Build();
```

```razor
@* ❌ Bad — consumer's Class/Style is silently dropped *@
<MudGrid Class="mud-alert">           @* hard-coded, ignores @Class *@
    <MudPaper>@ChildContent</MudPaper>
</MudGrid>
```

This rule is what makes spacing and layout adjustments composable at the call site instead of forcing every minor tweak into a new component prop.

#### 4. Include all states
Every interactive component must visually handle:
- Default
- Hover
- Active/pressed
- Focus (keyboard accessibility)
- Disabled
- Loading (where applicable)

#### 5. Component naming convention
`Component / Type / Variant / State`

Examples:
- `Button / Primary / Large / Default`
- `Input / Text / Default / Focused`
- `Card / Product / Default`

Avoid: `Button1`, `NewButton`, `FinalCard`.

#### 6. Follow MudBlazor variants
Every component must support its standard variants:
- Buttons: `Variant.Filled`, `Variant.Outlined`, `Variant.Text`
- Sizes: `Size.Small`, `Size.Medium`, `Size.Large`
- Colors: `Color.Primary`, `Color.Secondary`, `Color.Tertiary`

#### 7. Build base components first
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

### Per-component rules

This section captures rules that apply to specific MudBlazor components. More rules will be added over time.

#### `MudTextField`

**Always use `Placeholder`, never `Label`.** The project's input style relies on placeholder-only fields — `Label` produces an outline/floating-label layout we explicitly don't want.

```razor
@* ✅ Good *@
<MudTextField @bind-Value="_email"
              Placeholder="@Strings.Email_Placeholder"
              HelperText="@Strings.Email_Hint" />

@* ❌ Bad — uses Label *@
<MudTextField @bind-Value="_email"
              Label="@Strings.Email_Label" />
```

If a design genuinely needs a label-above-input pattern, render the label as a separate `<MudText>` above the field — don't fall back to `MudTextField.Label`:

```razor
@* ✅ When a visible field name above the input is required *@
<MudText Typo="Typo.caption">@Strings.Email_Label</MudText>
<MudTextField @bind-Value="_email"
              Placeholder="@Strings.Email_Placeholder" />
```

---

## CSS Classes & Inline Styles

Most UI in this project should be achievable using MudBlazor components and their built-in parameters. Reach for classes only when parameters can't express the design, and reach for inline `style` only when classes can't either.

### Priority order (strict)

When you need to alter the look or layout of a MudBlazor component, work down this list in order. Move to the next step only when the current one cannot produce the required result.

1. **MudBlazor component parameters.** Try to achieve the design solely using MudBlazor components and their available parameters (`Variant`, `Color`, `Size`, `Dense`, `Outlined`, `Elevation`, `Spacing`, `Justify`, `Align`, etc.). This is the cleanest path and the most resilient to MudBlazor upgrades.

2. **MudBlazor's auto-generated CSS classes.** If parameters can't express it, inspect MudBlazor's emitted CSS for an existing utility class that fits — the color families covered in §Colors, plus spacing/alignment utilities (`pa-*`, `ma-*`, `gap-*`, `d-flex`, `align-center`, …). Use those before writing anything new.

3. **Project SCSS-generated classes.** If MudBlazor doesn't have a class that fits, use one of the project's own SCSS-generated utility classes from `src/TheShop.Web/Styles/`. If a suitable class doesn't exist yet but the styling would be reusable, **generate a new SCSS class** (see §SCSS Stylesheets for rules — folder, naming, when to generate). **Never write component-scoped CSS in a `<style>` block inside the `.razor` file.**

4. **Inline `Style` (last resort).** Only when the styling is genuinely one-off, won't be reused elsewhere, has no foreseeable future reuse, and a class would be more overhead than benefit. Even here, build the style via `StyleBuilder` rather than concatenating strings.

```razor
@* ✅ Step 1 — parameters alone *@
<MudPaper Elevation="2" Outlined="true" Class="pa-4">…</MudPaper>

@* ✅ Step 2 — Mud utility class fills a small gap *@
<MudStack Class="gap-2 align-center">…</MudStack>

@* ✅ Step 3 — project SCSS utility class for a reusable pattern *@
<MudText Typo="Typo.h4" Class="fs-22 fw-600">Off-spec heading</MudText>

@* ✅ Step 4 — truly one-off, last-resort inline style via StyleBuilder *@
<MudPaper Style="@CardStyle">…</MudPaper>
```

### Composing classes — always use `CssBuilder`

When a component (or page) needs to conditionally compose multiple CSS classes, use MudBlazor's `CssBuilder`. Never concatenate class strings with `+`, `string.Format`, interpolation, or ternary expressions.

```csharp
// ✅ In *.razor.cs
protected string Classname => new CssBuilder("mud-alert")
    .AddClass("mud-dense", Dense)
    .AddClass("mud-square", Square)
    .AddClass(Class)             // forward consumer's Class (per Component Rule 3)
    .Build();

protected string SomeClassname => new CssBuilder("mud-toolbar-appbar")
    .AddClass(SomeClass)
    .Build();
```

```razor
@* ✅ In *.razor *@
<MudGrid Class="@Classname">
    <SomeComponent Class="@SomeClassname" />
</MudGrid>

@* ❌ Bad — string concatenation *@
<MudGrid Class="@($"mud-alert {(Dense ? "mud-dense" : "")} {Class}")">…</MudGrid>
```

`CssBuilder.AddClass(condition: bool)` only emits the class when the predicate is true — that's the entire point of using it.

### Composing inline styles — always use `StyleBuilder`

When inline styles are truly necessary (step 4 above), build them with `StyleBuilder` and expose the result as a property. Never concatenate style strings by hand.

```csharp
// ✅ In *.razor.cs
private string Stylename => new StyleBuilder()
    .AddStyle("margin-top", "4px")
    .AddStyle("max-width", $"{_maxWidth}px", when: _maxWidth > 0)
    .AddStyle(Style)             // forward consumer's Style (per Component Rule 3)
    .Build();
```

```razor
@* ✅ In *.razor *@
<MudGrid Style="@Stylename">…</MudGrid>

@* ❌ Bad — string concatenation *@
<MudGrid Style="@($"margin-top: 4px; max-width: {_maxWidth}px;")">…</MudGrid>
```

### What never to do

- ❌ `<style>` block inside a `.razor` file for ad-hoc page styling. If reusable, it belongs in SCSS. If truly one-off, use inline `style` via `StyleBuilder`. Never both.
- ❌ A new global CSS file (`*.css`) just to hold one page's overrides. Use SCSS in `src/TheShop.Web/Styles/` with the right folder and naming.
- ❌ Inline `style` strings concatenated with `+` or string interpolation. Use `StyleBuilder`.
- ❌ Forgetting `.AddClass(Class)` / `.AddStyle(Style)` at the end of a reusable component's builder chain — that silently drops consumer customization.

---

## SCSS Stylesheets

When MudBlazor can't express the design via parameters or its own classes, SCSS — not inline `<style>` blocks, not page-scoped CSS files — is the next step. The rules below govern where SCSS lives, what it should contain, and when to write it at all.

### Location

All SCSS lives under `src/TheShop.Web/Styles/`. There is no other allowed location for project stylesheets.

### Folder structure

```
src/TheShop.Web/Styles/
├── TheShop.scss               // root entry point — imports the partials below
├── abstracts/                 // tokens, theme variables, type/color utilities
│   ├── _colors.scss
│   ├── _typography.scss
│   └── _variables.scss
├── components/                // component-level styles
│   ├── _button.scss
│   ├── _field.scss
│   └── _picker.scss
├── layouts/                   // layout-level styles (MainLayout, AuthLayout, …)
│   └── _main.scss
└── utilities/                 // utility-class collections
    ├── borders/
    ├── flexbox/
    └── spacing/
```

- `abstracts/` — design tokens and theme variables that aren't components themselves (colors, typography utilities, shared SCSS variables, mixins). The typography `fs-*` / `fw-*` utilities live here.
- `components/` — styles scoped to a specific component family (`_button.scss`, `_field.scss`). One file per component family.
- `layouts/` — page-shell styles (e.g. `_main.scss` for `MainLayout`).
- `utilities/` — broad utility-class collections (borders, flexbox helpers, spacing — split into subfolders as the collection grows).

### Naming conventions

- All SCSS partials start with an underscore and use lowercase, e.g. `_typography.scss`, `_button.scss`. **Never** `Typography.scss` or `typography.scss`.
- Generated utility classes follow short, descriptive prefixes by concern: `fs-*` (font-size), `fw-*` (font-weight), `pa-*` / `ma-*` (already in MudBlazor for padding/margin if applicable), etc.
- Prefer generating classes from a SCSS list + `@each` loop rather than hand-writing every variant — that's how `_typography.scss` produces `fs-10`, `fs-12`, … from a single `$font-sizes` collection. To add a new size or weight, add it to the list — don't hand-write a new selector.

### When to write SCSS

Generate SCSS only if **all** of the following are true:

1. **MudBlazor doesn't already provide it.** Check MudBlazor's emitted classes (color, spacing, flexbox, typography utilities) before writing anything. If MudBlazor has it, use that.
2. **It will be reused.** Style that is genuinely shared across multiple places, future-proof, and belongs to a reusable concept. If you can only think of one call site and no plausible future one, don't generate a class — use an inline `style` instead (step 4 of the priority order).
3. **It fits an existing partial or warrants a new one.** Don't sprinkle one-off rules into `_button.scss` if they belong in `_field.scss`. If a new family of styles emerges (e.g. shadow utilities), create a new partial under the right folder rather than expanding an unrelated one.

If a class already exists for what you need — **use it directly**. Don't duplicate.

### When to fall back to inline `style`

Inline `style` (always via `StyleBuilder`) is the right choice when:

- The styling does not repeat anywhere else in the codebase.
- There is no realistic future reuse — it's a one-off layout tweak on a single page.
- Producing a class would be more code than the inline value (e.g. `Style="max-width: 480px"` for one container).

Even in this case, the styling lives on the call site as an inline `Style` — not in a `<style>` block, not in a new CSS file.

### Anti-patterns

```razor
@* ❌ Bad — <style> block inside a .razor file *@
<style>
    .product-page-header { font-size: 22px; }
</style>

@* ❌ Bad — a new CSS file just for one page *@
@* wwwroot/css/product-page.css *@

@* ❌ Bad — SCSS partial named without leading underscore or with capital letters *@
@* Styles/components/Button.scss *@

@* ❌ Bad — hand-writing every .fs-* class instead of using @each *@
.fs-22 { font-size: 22px !important; }
.fs-23 { font-size: 23px !important; }
.fs-24 { font-size: 24px !important; }
```

```scss
// ✅ Good — generate the family from a list
$font-sizes: (10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 34, 48, 60, 96);

@each $size in $font-sizes {
    .fs-#{$size} {
        font-size: #{$size}px !important;
    }
}
```

---

## Anti-Patterns to Reject

### ❌ Hardcoded user-facing strings
```razor
<MudText>Add to Cart</MudText>
<MudButton>Save Changes</MudButton>
<PageTitle>Product Details</PageTitle>
```
**Fix:** Use the strongly-typed `Strings` accessor — `<MudText>@Strings.AddToCart</MudText>`.

### ❌ Magic-string resource keys
```razor
<MudButton>@Localizer["AddToCart"]</MudButton>
<PageTitle>@Localizer["ProductDetail_PageTitle"]</PageTitle>
<MudText>@Localizer["WelcomeMessage"]</MudText>
```
**Why this is wrong:** No compile-time check. A typo like `Localizer["AddtoCart"]` (lowercase `t`) compiles fine and silently shows the literal key in the UI at runtime. Renaming a key in `Strings.resx` doesn't update the magic-string usages — they break silently.

**Fix:** Use the strongly-typed accessor. The compiler validates the key exists.
```razor
<MudButton>@Strings.AddToCart</MudButton>
<PageTitle>@Strings.ProductDetail_PageTitle</PageTitle>
<MudText>@Strings.WelcomeMessage</MudText>
```

The only legitimate use of `Localizer[...]` is when the key is determined at runtime (e.g., `@Localizer[result.Error]` where `result.Error` is a string returned by the Application layer).

### ❌ Hardcoded hex colors
```razor
<div style="background: #101010">...</div>
<MudButton Style="color: #d4a55c">...</MudButton>
```
**Fix:** Use `Color="Color.Primary"` first; if not possible, one of MudBlazor's auto-generated color classes (`mud-theme-*`, `mud-{name}-text`, `mud-{name}-bg`, `mud-border-{name}`, …); last resort, Ask User with a detailed explanation why and proceed with user choice.

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
**Fix:** Use the closest `Typo` parameter. For off-spec sizes/weights, compose with the project's SCSS utility classes (`fs-*`, `fw-*`) — e.g. `<MudText Typo="Typo.h4" Class="fs-22 fw-600">`. Never inline-style font properties.

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

### ❌ Page-scoped CSS / `<style>` blocks in `.razor`
```razor
@* product-detail.razor *@
<style>
    .product-page-header { font-size: 22px; font-weight: 600; }
</style>
```
**Fix:** If reusable, define a class in the appropriate SCSS partial under `src/TheShop.Web/Styles/` (per §SCSS Stylesheets). If truly one-off, use inline `Style` composed via `StyleBuilder`. Never both.

### ❌ One-off page CSS files
```css
/* wwwroot/css/product-page.css */
.special-button { background: var(--mud-palette-primary); }
```
**Fix:** Use MudBlazor parameters and auto-generated classes first; project SCSS only for genuinely reusable styles; inline `Style` via `StyleBuilder` for one-offs.

### ❌ Concatenated class / style strings
```razor
<MudGrid Class="@($"mud-alert {(Dense ? "mud-dense" : "")} {Class}")"
         Style="@($"margin-top: 4px; max-width: {_maxWidth}px;")">…</MudGrid>
```
**Fix:** Use `CssBuilder` for classes and `StyleBuilder` for styles — see §CSS Classes & Inline Styles.

### ❌ Reusable component drops consumer `Class` / `Style`
```razor
@* ShopCard.razor — root is hard-coded, consumer's Class/Style is ignored *@
<MudPaper Class="mud-card">
    @ChildContent
</MudPaper>
```
**Fix:** Forward `Class` and `Style` to the root element. Use Pattern A (direct passthrough) or Pattern B (`CssBuilder`/`StyleBuilder` ending in `.AddClass(Class)` / `.AddStyle(Style)`) — see Component Design Rules §3.

### ❌ Reusable component not derived from `MudComponentBase`
```csharp
public partial class ShopProductCard : ComponentBase { }
```
**Fix:** Inherit from `MudComponentBase` (directly or transitively) so `Class`, `Style`, and `UserAttributes` are available without re-declaring — see Component Design Rules §2.

### ❌ Premature or single-use component extraction
```razor
@* ShopSignInButton.razor — used in exactly one place (SignIn.razor) and just wraps a MudButton *@
<MudButton Color="Color.Primary" Variant="Variant.Filled" OnClick="OnClick">
    @ChildContent
</MudButton>
```
**Fix:** Inline the markup at the single call site. Extract on the **second** real call site, not the first. See Component Design Rules §Deciding when to extract a reusable component. Also avoid: components with many boolean configuration flags (a sign you're hiding two components in one), components named `Generic*`/`Common*`/`Shared*`, and components extracted only to reduce line count in the parent page.

### ❌ `MudTextField` with `Label`
```razor
<MudTextField Label="@Strings.Email_Label" @bind-Value="_email" />
```
**Fix:** Use `Placeholder` instead. If a visible label-above-input is genuinely required, render a separate `<MudText Typo="Typo.caption">` above the field.

### ❌ Missing alt text or hardcoded alt text
```razor
<img src="@product.ImageUrl" alt="Product image" />
```
**Fix:** `Alt="@string.Format(Strings.Product_ImageAlt, product.Name)"`.

### ❌ Theme classes without `Shop` prefix
```csharp
public static class AppColors { }
public static class IconLibrary { }
```
**Fix:** Rename to `ShopColors`, `ShopIcons`.

---

## Loading & Busy Indicators

The project owns spinner placement and styling centrally. Pages must not hand-roll loaders.

### Inline button busy state

Wrap the affected control in `<BusyFor Key="@BusyKeys.X" Context="busy">` and bind `MudButton.Disabled="@busy"` plus an inline `MudProgressCircular` when `busy` is true. The `BusyFor` component subscribes to `BusyState.Changed` and re-renders only its child fragment on transitions.

```razor
<BusyFor Key="@BusyKeys.Auth.SignIn" Context="busy">
    <MudButton Variant="Variant.Filled"
               Color="Color.Primary"
               Disabled="@(!_isFormValid || busy)"
               OnClick="OnSubmitAsync">
        @if (busy)
        {
            <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
        }
        @Strings.Auth_SendCode
    </MudButton>
</BusyFor>
```

The page's code-behind drives the busy state explicitly:

```csharp
private async Task OnSubmitAsync()
{
    await BusyState.RunAsync(BusyKeys.Auth.SignIn, async () =>
    {
        var result = await Mediator.Send(new RequestSignInOtpCommand(_email));
        // ...
    });
}
```

### App-blocking busy state

`<ShopLoadingOverlay />` is mounted once in `MainLayout.razor` and observes `BusyKeys.Global`. Trigger it from page code for app-blocking work (session restore on startup, sign-out, etc.):

```csharp
await BusyState.RunAsync(BusyKeys.Global, () => RestoreSessionAsync());
```

### Rules

- Never hand-roll `MudProgressCircular` outside the `BusyFor` `ChildContent` fragment — placement and styling live in that component.
- No `_isBusy` boolean fields in pages. `BusyState` is the only source of truth.
- `BusyKeys` constants only — no magic strings at call sites.

---

## Code-Behind Separation

Every `.razor` file with logic has a sibling `.razor.cs` partial class. Markup-only files (simple display components) may stay single-file.

### File split

`.razor` holds:
- Markup (component tree, render fragments)
- Directives: `@inherits`, `@implements`, `@typeparam`, `@attribute`
- Local `@using` directives for namespaces the markup references
- **No** `@page` directive — route declarations live in code-behind

`.razor.cs` holds:
- `public partial class X : ComponentBase` (or `LayoutComponentBase`)
- `[Route(Routes.X)]` attribute for pages
- All `[Inject]`, `[Parameter]`, fields, methods, lifecycle hooks, `IDisposable` implementations

### Example

```razor
@* Pages/Auth/SignIn.razor *@
@attribute [AllowAnonymous]
@using TheShop.Web.Common
@using TheShop.Web.Components.Common

<PageTitle>@Strings.SignIn_PageTitle</PageTitle>
<MudContainer>...</MudContainer>
```

```csharp
// Pages/Auth/SignIn.razor.cs
using Microsoft.AspNetCore.Components;
using TheShop.Web.Common;

namespace TheShop.Web.Pages.Auth;

[Route(Routes.Auth.SignIn)]
public partial class SignIn : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    // ...
}
```

### Rules

- No inline `@code` blocks larger than ~5 lines.
- Page namespaces follow folder structure (`Pages/Auth/SignIn.razor.cs` → `TheShop.Web.Pages.Auth`).
- Add `@using` directives directly in the `.razor` file that needs them — don't pollute `_Imports.razor` with feature-specific namespaces.

---

## Final Reminders for AI Agents

1. **Hardcoded strings = automatic rejection.** When you see English text in a `.razor` file, refactor it to use the strongly-typed `Strings.{KeyName}` accessor before doing anything else.

2. **Magic-string keys are forbidden.** `Localizer["AddToCart"]` is wrong. `Strings.AddToCart` is right. The only allowed `Localizer[...]` usage is when the key is determined at runtime (e.g. `Localizer[result.Error]`).

3. **`Shop` prefix is mandatory.** Any new color, icon, or typography concept goes into a `Shop*` class. No exceptions.

4. **MudBlazor is the only UI library.** If MudBlazor cannot meet a requirement, **stop and ask the user first** — propose alternatives, wait for confirmation, then proceed.

5. **Color preference order is strict:** `Color` enum → any of MudBlazor's auto-generated color CSS classes (pick the **most specific** one — `mud-{name}-text`, `mud-theme-{name}`, `mud-border-{name}`, …) → Ask user (last resort).

6. **Always `MudText` with `Typo`.** Never use `<span>`, `<p>`, `<h1>`, etc. for displaying content. For off-spec sizes/weights, compose with `fs-*` / `fw-*` utility classes from `_typography.scss` — never inline `font-size` / `font-weight`.

7. **Styling priority is strict:** Mud parameters → Mud auto-generated classes → project SCSS class (only if reusable) → inline `Style` via `StyleBuilder` (last resort). Compose classes with `CssBuilder`, styles with `StyleBuilder` — never string concatenation. No `<style>` blocks in `.razor`, no page-scoped CSS files.

8. **Inline first, extract on the second real call site.** Don't create a reusable component for a single use, to shorten a parent page, or to "future-proof" for a hypothetical caller. Extract when the same UI + behavior actually repeats today, has a clearly nameable single responsibility, and the markup is non-trivial. Per `CLAUDE.md`: *three similar lines is better than a premature abstraction.*

9. **Reusable components must inherit from `MudComponentBase` and forward `Class`/`Style` to their root element.** Otherwise consumer-side customization is silently lost.

10. **`MudTextField`: `Placeholder` only — never `Label`.**

11. **Generate the resource keys alongside the page.** When you create `ProductDetail.razor`, also add the necessary keys to `Strings.resx` (and `Strings.fr.resx` with the same keys, French translations or `[TODO]` placeholders). Use `Strings.{KeyName}` everywhere in the Razor file — the auto-generated `Strings.Designer.cs` provides the typed properties.

12. **Reference both files.** When making decisions, cite the specific principle from `ARCHITECTURE.md` (architecture) or this `DESIGN.md` (visual/strings).

---

## Design Checklist

Before writing or accepting any UI code, verify. If any item fails, **stop and fix it** before proceeding.

### Strings
- [ ] All user-facing text comes from `Strings.resx` (no hardcoded English in `.razor` files)?
- [ ] Static keys accessed via `Strings.{KeyName}` directly — NOT via `Localizer["{KeyName}"]`?
- [ ] `Localizer[...]` only used for runtime keys (e.g. `Localizer[result.Error]`)?
- [ ] Resource keys follow the `{Context}_{Purpose}` naming convention?
- [ ] Resource keys are valid C# identifiers (no hyphens, spaces, leading digits)?
- [ ] `Strings.resx` configured with `PublicResXFileCodeGenerator` custom tool?
- [ ] French resource file (`Strings.fr.resx`) updated alongside English?
- [ ] Application layer returns resource KEYS via `nameof(Strings.{Key})` (not magic strings)?

### Colors
- [ ] First preference: `Color="Color.Primary"` (or other `Color` enum) used?
- [ ] Second preference: the **most specific** MudBlazor auto-generated color class used (e.g. `mud-error-text` for text-only, `mud-theme-secondary` for bg + text together, `mud-border-primary` for border-only)?
- [ ] Last resort: user was asked with an explanation before reaching for anything else?
- [ ] No hardcoded hex values anywhere in `.razor` files?

### Icons
- [ ] All icons come from `ShopIcons`?
- [ ] No direct `Icons.Material.*` references in pages/components?
- [ ] Icon names are semantic (`Cart`) not visual (`ShoppingCart`)?
- [ ] When adding a new icon, the semantic naming convention is followed?

### Typography
- [ ] All text uses `<MudText>` with the `Typo` parameter?
- [ ] No `<span>`, `<p>`, `<h1>`–`<h6>`, or other native text elements for content?
- [ ] No inline `font-size`, `font-weight`, or `line-height` styles?
- [ ] Off-spec sizes/weights composed with `fs-*` / `fw-*` utility classes from `_typography.scss` (not inline styles, not bespoke classes)?
- [ ] New size/weight added to `$font-sizes` / `$font-weights` lists rather than hand-writing a `.fs-{n}` selector?
- [ ] Need a structural new typography variant (font family, line-height)? — User asked first?

### Components
- [ ] Only MudBlazor components used (no custom buttons, inputs, or raw HTML primitives)?
- [ ] If MudBlazor cannot meet the requirement, was the user asked before introducing an alternative?
- [ ] **Did you decide to extract vs inline correctly?** Each new reusable component satisfies at least one *extract* trigger (repeats today, consistency-critical pattern, clearly nameable single responsibility) AND no *avoid* signal (single use, future-proofing, many flag parameters, only logic repeats, tiny markup) — see §Deciding when to extract a reusable component.
- [ ] Every reusable component inherits from `MudComponentBase` (directly or transitively)?
- [ ] Every reusable component forwards `Class` and `Style` to its root element (Pattern A — direct passthrough, or Pattern B — builder chain ending in `.AddClass(Class)` / `.AddStyle(Style)`)?
- [ ] All interactive states handled (hover, focus, disabled)?
- [ ] Component variants follow MudBlazor patterns?
- [ ] `MudTextField` uses `Placeholder` — never `Label`?

### CSS Classes & Styles
- [ ] Did the design land on the lowest step of the priority order it could? (1: Mud parameters → 2: Mud classes → 3: project SCSS class → 4: inline `Style`)
- [ ] Conditional classes composed with `CssBuilder` — not string concatenation/interpolation?
- [ ] Inline styles composed with `StyleBuilder` — not string concatenation/interpolation?
- [ ] No `<style>` blocks inside `.razor` files?
- [ ] No new page-scoped `*.css` files in `wwwroot/`?

### SCSS
- [ ] SCSS lives under `src/TheShop.Web/Styles/` in the right folder (`abstracts/`, `components/`, `layouts/`, `utilities/`)?
- [ ] Partial filenames start with `_` and are lowercase (`_field.scss`, not `Field.scss`)?
- [ ] New utility families generated via `$list` + `@each` loop rather than hand-writing every selector?
- [ ] An existing SCSS class is reused before generating a new one?
- [ ] New class is genuinely reusable (multiple call sites / plausible future reuse) — not a one-off that should have been inline `Style`?

### Brand
- [ ] All theme classes use the `Shop` prefix (`ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`)?

### Loaders & routes
- [ ] No `_isBusy` field in the page — busy state goes through `await BusyState.RunAsync(BusyKeys.X, ...)`?
- [ ] Spinner is inside `<BusyFor Key="@BusyKeys.X" Context="busy">`, never hand-rolled?
- [ ] No hardcoded route strings — `Href`, `NavigateTo`, redirect targets all use `Routes.X`?
- [ ] Page declares its route via `[Route(Routes.X)]` on the code-behind, not `@page "/..."` in markup?

### Code-behind
- [ ] Page has a sibling `.razor.cs` partial class for any `@code` logic beyond ~5 lines?
- [ ] Markup file has no `@code` block (only directives + render tree)?
- [ ] Feature-specific `@using` directives live in the `.razor` file, not in `_Imports.razor`?

### Images
- [ ] WebP format used for raster images?
- [ ] `width`, `height`, and `loading="lazy"` attributes set?
- [ ] Alt text comes from resources?
- [ ] `MudImage` used where appropriate?

If any answer above is "no", stop and refactor before declaring the task complete.

---

**End of Design System Instructions**

*Last updated: May 2026 · Version 1.2*
*See `ARCHITECTURE.md` for architecture, layer rules, and code structure.*

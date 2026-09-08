# Design — CSS, Inline Styles, SCSS

> Implementation guide for Rules 26, 27, 28 from `SKILL.md`. Covers the priority order for styling, `CssBuilder` / `StyleBuilder` patterns, SCSS folder layout and naming, and when to fall back to inline `Style`. The rules themselves live in `SKILL.md`; this file does not restate them.

---

## The four steps in the priority order (Rule 26)

When you need to alter the look or layout of a MudBlazor component, work down this list. Move to the next step **only when the current one cannot produce the result.**

### 1. MudBlazor parameters

Most styling lives on MudBlazor itself — `Variant`, `Color`, `Size`, `Dense`, `Outlined`, `Elevation`, `Spacing`, `Justify`, `Align`. This is the cleanest path and the most resilient to MudBlazor upgrades.

```razor
<MudPaper Elevation="2" Outlined="true" Class="pa-4">…</MudPaper>
```

### 2. MudBlazor auto-generated CSS classes

If parameters can't express it, reach for MudBlazor's emitted utility classes:

- Colour families: `mud-theme-*`, `mud-{name}-text`, `mud-{name}-bg`, `mud-border-{name}`, `mud-icon-{name}` — see `design-theme.md` for the full family table.
- Spacing / layout: `pa-*` (padding), `ma-*` (margin), `gap-*`, `d-flex`, `align-center`, `justify-space-between`, …

```razor
<MudStack Class="gap-2 align-center">…</MudStack>
```

Use these before writing anything new. Don't reach for project SCSS just because a class might exist later.

### 3. Project SCSS-generated class

If MudBlazor doesn't have a class and the styling is **genuinely reusable**, use one of the project's SCSS-generated utilities from `src/TheShop.Web/Styles/`. If a suitable class doesn't exist yet but the styling will be reused, **generate a new SCSS class** (rules below). Never write component-scoped CSS in a `<style>` block inside a `.razor` file.

```razor
<MudText Typo="Typo.h4" Class="fs-22 fw-600">Off-spec heading</MudText>
```

### 4. Inline `Style` — last resort

Only when:
- The styling is genuinely one-off and won't be reused.
- There is no foreseeable future reuse — single page, single block.
- A class would be more overhead than benefit (e.g. `Style="max-width: 480px"` for a one-off container).

Even here, build the style with `StyleBuilder` (below) — never string concatenation.

```razor
<MudPaper Style="@CardStyle">…</MudPaper>
```

---

## `CssBuilder` (Rule 27)

When a component (or page) needs to conditionally compose multiple CSS classes, use MudBlazor's `CssBuilder`. Never concatenate class strings with `+`, `string.Format`, interpolation, or ternary expressions.

```csharp
// ✅ In *.razor.cs
protected string Classname => new CssBuilder("mud-alert")
    .AddClass("mud-dense", Dense)
    .AddClass("mud-square", Square)
    .AddClass(Class)             // forward consumer's Class — Rule 24
    .Build();

protected string SomeClassname => new CssBuilder("mud-toolbar-appbar")
    .AddClass(SomeClass)
    .Build();
```

```razor
@* In *.razor *@
<MudGrid Class="@Classname">
    <SomeComponent Class="@SomeClassname" />
</MudGrid>

@* ❌ string concatenation *@
<MudGrid Class="@($"mud-alert {(Dense ? "mud-dense" : "")} {Class}")">…</MudGrid>
```

`CssBuilder.AddClass(name, condition: bool)` only emits the class when the predicate is true — that's the entire point.

---

## `StyleBuilder` (Rule 27)

When inline styles are necessary (step 4), build them with `StyleBuilder` and expose the result as a property. Never concatenate style strings by hand.

```csharp
// ✅ In *.razor.cs
private string Stylename => new StyleBuilder()
    .AddStyle("margin-top", "4px")
    .AddStyle("max-width", $"{_maxWidth}px", when: _maxWidth > 0)
    .AddStyle(Style)             // forward consumer's Style — Rule 24
    .Build();
```

```razor
@* In *.razor *@
<MudGrid Style="@Stylename">…</MudGrid>

@* ❌ string concatenation *@
<MudGrid Style="@($"margin-top: 4px; max-width: {_maxWidth}px;")">…</MudGrid>
```

---

## SCSS — location and structure (Rule 28)

All SCSS lives under `src/TheShop.Web/Styles/`. There is **no other allowed location** for project stylesheets.

```
src/TheShop.Web/Styles/
├── TheShop.scss               ← root entry — imports the partials below
├── abstracts/                 ← tokens, theme variables, type/colour utilities
│   ├── _colors.scss
│   ├── _typography.scss
│   └── _variables.scss
├── components/                ← per-component-family styles
│   ├── _button.scss
│   ├── _field.scss
│   └── _picker.scss
├── layouts/                   ← page-shell styles
│   └── _main.scss
└── utilities/                 ← utility-class collections
    ├── borders/
    ├── flexbox/
    └── spacing/
```

- `abstracts/` — design tokens, theme variables, shared SCSS variables, mixins. The `fs-*` / `fw-*` typography utilities live in `_typography.scss`.
- `components/` — styles scoped to a component family (`_button.scss`, `_field.scss`). One file per family.
- `layouts/` — page-shell styles (`_main.scss` for `MainLayout`).
- `utilities/` — broad utility-class collections. Split into subfolders as a collection grows.

### Naming

- Partials start with an underscore and are lowercase: `_typography.scss`, `_button.scss`. **Never** `Typography.scss` or `typography.scss`.
- Generated utility classes use short prefixes by concern: `fs-*` (font-size), `fw-*` (font-weight). Spacing/margin utilities come from MudBlazor (`pa-*`, `ma-*`) — don't duplicate them.

### Generation discipline

**Always** generate utility families from a `$list` + `@each` loop. **Never** hand-write each selector.

```scss
// ✅ Good — one source of truth, generate the family
$font-sizes: (10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 34, 48, 60, 96);

@each $size in $font-sizes {
    .fs-#{$size} {
        font-size: #{$size}px !important;
    }
}

// ❌ Bad — hand-writing every variant
.fs-22 { font-size: 22px !important; }
.fs-23 { font-size: 23px !important; }
.fs-24 { font-size: 24px !important; }
```

To add a new size or weight: **add the value to the list**, not a new selector.

### When to write SCSS at all

Generate SCSS only if **all** of the following are true:

1. **MudBlazor doesn't already provide it.** Check emitted Mud classes (colour, spacing, flexbox, typography) before writing anything.
2. **It will be reused.** Genuinely shared across multiple places. If you can only point to one call site and no plausible future one, use inline `Style` (step 4) instead.
3. **It fits an existing partial or warrants a new one.** Don't sprinkle one-off rules into `_button.scss` if they belong in `_field.scss`. New family of styles → new partial under the right folder.

If a class already exists, **use it directly.** Don't duplicate.

### When inline `Style` is right

- Styling does not repeat anywhere else.
- No realistic future reuse — a one-off layout tweak on a single page.
- Producing a class would be more code than the inline value (e.g. `Style="max-width: 480px"` for one container).

Even then, the styling lives on the call site as inline `Style` (via `StyleBuilder`) — not in a `<style>` block, not in a new CSS file.

---

## Forbidden — what you must never do (Rule 28)

```razor
@* ❌ <style> block inside a .razor *@
<style>
    .product-page-header { font-size: 22px; }
</style>

@* ❌ a new CSS file just for one page *@
@* wwwroot/css/product-page.css *@

@* ❌ SCSS partial without leading underscore or with capital letters *@
@* Styles/components/Button.scss *@
```

---

## Common mistakes

| Mistake | Fix |
|---|---|
| Skipped step 1, went straight to a custom class | Verify no MudBlazor parameter, no MudBlazor utility class fits before generating SCSS |
| Class composed via `string.Format` / interpolation / ternary | Use `CssBuilder.AddClass(name, condition)` |
| Style composed via `string.Format` / interpolation | Use `StyleBuilder.AddStyle(prop, val, when)` |
| Reusable component's builder chain omits `.AddClass(Class)` at the end | Add it — silently dropping consumer `Class` is a Rule 24 violation |
| Hand-written `.fs-{n}` selector for a new size | Add the number to `$font-sizes`, let `@each` generate it |
| New `_Button.scss` (PascalCase) | Rename to `_button.scss` |
| `<style>` block in `Product.razor` to add a one-off rule | Inline `Style` (via `StyleBuilder`) for one-off; new SCSS class for reusable |
| New `wwwroot/css/cart.css` for cart-page overrides | Either MudBlazor utility, project SCSS, or inline `Style` — never a new page-scoped CSS file |
| Duplicated class in a new partial when one already exists | Reuse the existing class — search before generating |

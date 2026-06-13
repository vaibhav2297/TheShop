# Checklist — Design

> Verification gate for Strings, Theme, Web, Components, and Styles rules from `SKILL.md`. Loaded by reviewer agents or when explicitly verifying UI code. Yes/no only — no rationale. If any answer is "no", **stop and fix it** before declaring the task complete.

---

## Strings (Rules 11, 12)

- [ ] All user-facing text comes from `Strings.resx`? No hardcoded English in any `.razor` file?
- [ ] Static keys accessed via `@Strings.{Key}` directly — never `@Localizer["{Key}"]`?
- [ ] `@Localizer[...]` is used **only** for runtime keys (e.g. `@Localizer[result.Error]`)?
- [ ] Resource keys follow the `{Context}_{Purpose}` naming convention?
- [ ] Resource keys are valid C# identifiers (no hyphens, no spaces, no leading digits)?
- [ ] Only `.resx` files edited for new strings — `Strings.Designer.cs` left untouched (it auto-generates on build)?
- [ ] French resource file (`Strings.fr.resx`) updated alongside English (translation or `[TODO]` placeholder)?
- [ ] Application layer returns resource keys via `nameof(Strings.{Key})` — never magic-string literals?

## Theme — Shop prefix (Rule 13)

- [ ] Every theme class uses the `Shop` prefix (`ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`)?
- [ ] Theme classes live under `src/TheShop.Web/Theme/`?
- [ ] Token registries are `static` classes? `ShopTheme` is an instance class?

## Theme — Colours (Rule 15)

- [ ] Step 1 first: `Color="Color.{Enum}"` parameter used where available?
- [ ] Step 2 when needed: the **most specific** MudBlazor auto-generated class used (e.g. `mud-error-text` for text only, not `mud-theme-error`)?
- [ ] No hardcoded hex values anywhere in `.razor` files?
- [ ] If neither enum nor Mud class could express the colour, was the user asked before any alternative was used?

## Theme — Icons (Rule 19)

- [ ] All icons come from `ShopIcons`?
- [ ] No `Icons.Material.*` references anywhere?
- [ ] Icon constants use **semantic** names (`Cart`) — not visual names (`ShoppingBag`)?

## Theme — Typography (Rules 16, 18)

- [ ] All text uses `<MudText Typo="...">`? No `<span>`, `<p>`, `<h1>`–`<h6>` for content?
- [ ] No inline `font-size`, `font-weight`, or `line-height` styles?
- [ ] Off-spec sizes/weights composed with `fs-*` / `fw-*` utility classes from `_typography.scss`?
- [ ] New sizes/weights added to `$font-sizes` / `$font-weights` lists — not hand-written `.fs-{n}` selectors?
- [ ] Need a structural typography variant (font family, line-height)? User was asked first?

## Components — extraction and forwarding (Rules 14, 17, 23, 24, 25)

- [ ] Did you decide to extract vs inline correctly? The new reusable component satisfies an *extract* trigger (repeats today, design-consistency-critical, clearly nameable single responsibility) AND no *avoid* signal (single use, future-proofing, many flag parameters, only logic repeats, tiny markup)?
- [ ] Only MudBlazor components used? No custom buttons, inputs, or raw HTML primitives?
- [ ] If MudBlazor cannot meet the requirement, was the user asked before introducing an alternative (Rule 14)?
- [ ] Every reusable component inherits from `MudBlazor.MudComponentBase` (directly or transitively)?
- [ ] Every reusable component forwards `Class` and `Style` to its root — Pattern A (direct passthrough) or Pattern B (builder chain ending with `.AddClass(Class)` / `.AddStyle(Style)`)?
- [ ] All interactive states handled (hover, focus, disabled, loading where applicable)?
- [ ] Component variants follow MudBlazor patterns (`Variant.Filled` / `Variant.Outlined` / `Variant.Text`; sizes; colours)?
- [ ] `MudTextField` uses `Placeholder` — never `Label`? If a visible label was needed, was a sibling `<MudText Typo="Typo.caption">` used instead?

## Styles — CSS / inline / SCSS (Rules 26, 27, 28)

- [ ] Styling landed on the lowest step of the priority order it could? (1: Mud parameters → 2: Mud auto-generated classes → 3: project SCSS class → 4: inline `Style`)
- [ ] Conditional classes composed with `CssBuilder` — never string concatenation / interpolation / ternary?
- [ ] Inline styles composed with `StyleBuilder` — never string concatenation / interpolation?
- [ ] No `<style>` blocks inside any `.razor` file?
- [ ] No new page-scoped `*.css` files in `wwwroot/`?
- [ ] SCSS lives under `src/TheShop.Web/Styles/` in the right folder (`abstracts/`, `components/`, `layouts/`, `utilities/`)?
- [ ] Partial filenames start with `_` and are lowercase (`_field.scss`, not `Field.scss`)?
- [ ] New utility families generated via `$list` + `@each` loop — not hand-written selectors?
- [ ] Existing SCSS class reused before generating a new one?
- [ ] New class is genuinely reusable (multiple call sites / plausible future reuse) — not a one-off that should have been inline `Style`?

## Web — pages, routes, busy state (Rules 20, 21, 22)

- [ ] Page has a sibling `.razor.cs` partial class for any `@code` logic beyond ~5 lines?
- [ ] Markup file has no `@code` block (only directives + render tree)?
- [ ] `[Route(Routes.X)]` lives on the code-behind partial — not `@page "/..."` in markup?
- [ ] No hardcoded route strings — every `Href`, `NavigateTo`, redirect uses `Routes.X`?
- [ ] No `_isBusy` field anywhere — busy state goes through `await BusyState.RunAsync(BusyKeys.X, ...)`?
- [ ] Spinner is inside `<BusyFor Key="@BusyKeys.X" Context="busy">` or `<ShopLoadingOverlay />` — never hand-rolled `MudProgressCircular`?
- [ ] `BusyKeys` constants used at every call site — no magic strings like `"sign-in"`?
- [ ] Feature-specific `@using` directives live in the `.razor` file — not in `_Imports.razor`?

## Imagery

- [ ] WebP format used for raster images?
- [ ] `width`, `height`, and `loading="lazy"` attributes set?
- [ ] Alt text comes from resources (`@string.Format(Strings.X, ...)`) — never hardcoded?
- [ ] `MudImage` used where appropriate?

For architecture / tests / documentation verification, run `checklists/code-generation.md`.

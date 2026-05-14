# Checklists — Code Generation & Design

Two pre-commit checklists for The Shop. Run both before declaring any task complete. If any item fails, **stop and fix it** before proceeding.

- Architecture detail → `references/ARCHITECTURE.md`
- Design / strings / theme detail → `references/DESIGN.md`

---

## Code Generation Checklist

Before writing or accepting any code:

### Architecture
- [ ] Is this in the right layer (Domain / Application / Infrastructure / Web)?
- [ ] Does it follow the dependency rule (dependencies point inward)?
- [ ] Are external SDKs (Supabase / Stripe / Resend) used **only** in Infrastructure?
- [ ] Are entities staying in Domain (not leaking to UI)?
- [ ] Is business logic in Domain entities or Application handlers (not in `@code` blocks or services)?
- [ ] Are use cases dispatched through MediatR (`IMediator.Send`)?
- [ ] Is `Result<T>` used for expected failures (not exceptions)?
- [ ] Are DTOs (records) used to cross layer boundaries — entities never crossing into UI?
- [ ] Is dependency injection via constructor with `readonly` fields?
- [ ] Do async methods accept `CancellationToken`?
- [ ] Are Commands/Queries declared as `record` (immutable)?
- [ ] Is the file in the correct folder per the structure?
- [ ] Does the class name follow the naming conventions?
- [ ] Is there a corresponding test in the matching `tests/` project?

---

## Design Checklist

Before writing or accepting any UI code:

### Strings
- [ ] All user-facing text comes from `Strings.resx` (no hardcoded English in `.razor` files)?
- [ ] Static keys accessed via `Strings.{KeyName}` directly — NOT via `Localizer["{KeyName}"]`?
- [ ] `Localizer[...]` only used for runtime keys (e.g. `Localizer[result.Error]`)?
- [ ] Resource keys follow the `{Context}_{Purpose}` naming convention?
- [ ] Resource keys are valid C# identifiers (no hyphens, spaces, leading digits)?
- [ ] `Strings.resx` configured with `PublicResXFileCodeGenerator` custom tool?
- [ ] French resource file (`Strings.fr.resx`) updated alongside English?
- [ ] Application layer returns resource KEYS via `nameof(Strings.{Key})` (not magic strings)?

### Components
- [ ] Only MudBlazor components used (no custom buttons, inputs, or raw HTML primitives)?
- [ ] If MudBlazor cannot meet the requirement, was the user asked before introducing an alternative?
- [ ] All interactive states handled (hover, focus, disabled)?
- [ ] Component variants follow MudBlazor patterns?

### Colors
- [ ] First preference: `Color="Color.Primary"` (or other `Color` enum) used?
- [ ] Second preference: `Class="mud-theme-primary"` used when the `Color` enum is not viable?
- [ ] Last resort: `ShopColors.*` only with a comment explaining why?
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
- [ ] Need a new typography variant? — User asked first?

### Brand
- [ ] All theme classes use the `Shop` prefix (`ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`)?

### Images
- [ ] WebP format used for raster images?
- [ ] `width`, `height`, and `loading="lazy"` attributes set?
- [ ] Alt text comes from resources?
- [ ] `MudImage` used where appropriate?

---

If any answer above is "no", stop and refactor before declaring the task complete.

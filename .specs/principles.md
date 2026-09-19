# The Shop — Project Constitution

Canonical location: `.specs/principles.md`
Status: Proposed for adoption
Source: Supplied constitution guidelines; repo implementation not inspected.

## Purpose and authority

This constitution sets mandatory architecture, design, component, styling, testing, and documentation rules for The Shop, premium Canadian e-commerce platform.

Declared stack: .NET 10, Blazor WebAssembly, MudBlazor, Supabase, Stripe, Resend, Azure Static Web Apps. Project context from owner, not dependency-version verification.

All rules mandatory unless project owner explicitly approves scoped exception or amendment. Rule numbers keep original 1–30 numbering with category prefixes, so existing numbered references map directly.

- Apply relevant rules to changed code. Existing violations don't authorize new ones or require unrelated refactoring.
- If feature request, Figma design, or existing implementation conflicts with rule, flag rule and conflict before implementing conflicting part. Continue unaffected work when practical.
- Record approved exceptions in feature spec: rule ID, reason, scope, approver, follow-up needed. Don't silently relax rule or rewrite it to make code compliant.
- Change this constitution only through explicit owner-approved amendment. Doesn't override system instructions, access controls, or owner's authority to revise project policy.

Routine dev steps, tool adapters, reference-loading tables, test commands belong in `.spec/README.md` or existing project docs, not here. Don't invent referenced implementation details not supplied with source.

## Architecture

### ARCH-01 — Four layers with inward dependencies

Use `TheShop.Domain`, `TheShop.Application`, `TheShop.Infrastructure`, `TheShop.Web`.

| Project | Permitted project references | Boundary |
| --- | --- | --- |
| Domain | None | Pure domain model. |
| Application | Domain | Use cases and application contracts. |
| Infrastructure | Application, Domain | Implementations of application contracts and external adapters. |
| Web | Application, Domain, Infrastructure | Infrastructure usage limited to composition wiring; UI doesn't consume domain entities. |

`Program.cs` is composition root joining layers. Keep layer registration helpers within own boundaries. Don't use Web's compiler-level references to bypass application contracts.

**Verify:** Inspect project references, cross-layer imports, constructor dependencies, composition wiring.

### ARCH-02 — Pure Domain

Domain uses pure C#, no project references. No Supabase, Stripe, MudBlazor, HTTP, JSON, persistence deps, or serialization attributes like `[JsonProperty]`.

**Verify:** Inspect Domain references, imports, attributes, public contracts.

### ARCH-03 — External SDK isolation

Supabase, Stripe, Resend SDK use only in `TheShop.Infrastructure`. SDK types must not leak through application interfaces or DTOs. E.g. `using Supabase;` outside Infrastructure violates rule.

**Verify:** Inspect package usage, imports, boundary signatures.

### ARCH-04 — MediatR use cases

Pages dispatch use cases through `IMediator.Send(...)`. Pages must not inject or directly call repositories.

**Verify:** Inspect page dependencies and data-access paths.

### ARCH-05 — Explicit business failures

Return `Result<T>` for expected business failures, e.g. missing products or insufficient stock. Reserve exceptions for unexpected technical failures.

**Verify:** Inspect handler failure paths and behavioral tests.

### ARCH-06 — Domain-owned behavior

Domain entities own business behavior and invariants. E.g. cart mutation belongs in `cart.AddItem(...)`; pages, handlers, services orchestrate rather than duplicate behavior.

**Verify:** Inspect where rules and mutations live; test observable outcomes.

### ARCH-07 — Immutable boundary DTOs

Use immutable `record` DTOs at layer boundaries. Don't expose domain entities to UI. Doesn't prohibit Application and Infrastructure using Domain types internally under ARCH-01.

**Verify:** Inspect boundary contracts and UI models for mutable DTOs or entity leakage.

### ARCH-08 — Cancellation across async boundaries

Every async method crossing layer boundary accepts `CancellationToken`. Forward it to downstream operations supporting cancellation rather than discarding it.

**Verify:** Inspect async contracts and token propagation.

### ARCH-09 — Layer-specific organization

Group Application code by business capability, e.g. `Features/Cart/` and `Features/Checkout/`.

Group Infrastructure by technical concern: `Persistence/`, `Auth/`, `Payments/`, `Email/`, `Storage/`. Don't create `Infrastructure/<BusinessFeature>/` folders.

- `Persistence/Records/`, `Persistence/Mappers/`, `Persistence/Repositories/` hold corresponding database-specific implementation artifacts.
- Persistence root holds only cross-cutting database plumbing, e.g. client factories or shared query extensions.
- Feature-specific database code belongs in relevant Persistence subfolder.
- Auth, Payments, Email, Storage are flat adapter folders; don't copy Records/Mappers/Repositories structure into them.
- Storage-bucket adapters belong in Storage, not Persistence.

**Verify:** Inspect changed file placement against business or technical responsibility.

### ARCH-10 — Thin pages

Pages hold presentation and interaction orchestration, not business decisions. Put use-case orchestration in Application, domain invariants in Domain under ARCH-06.

Razor `@code` block over 30 lines is mandatory extraction/review trigger, not permission to keep smaller business rules in page. Apply WEB-20 to move presentation logic into code-behind; don't move UI-only state or lifecycle behavior into Application just to cut line count.

**Verify:** Inspect markup and code-behind for business logic. Code-behind separation alone doesn't make page architecturally compliant.

## Design: text and resources

### TEXT-11 — Typed localized UI text

Don't hardcode user-facing strings or magic-string resource keys. Use `@Strings.Key` for compile-time-known keys. Use `@Localizer[key]` for runtime keys like `result.Error`. Literal lookup like `Localizer["AddToCart"]` forbidden.

**Verify:** Inspect UI text and resource lookups, incl. validation and error display.

### TEXT-12 — Typed Application error keys

Application returns resource keys through `nameof(Strings.Key)`, never string literals or localized display text.

Must not introduce Application-to-Web dependency. Before adopting implementation, verify where `Strings` accessor declared and how Application can legally reference it. If Web-only, resolve ownership conflict with owner; don't add outward reference or substitute unapproved resource strategy.

**Verify:** Inspect error-key expressions and accessor's defining assembly and dependencies.

## Design: theme and controls

### DESIGN-13 — Shop theme naming

Theme classes use `Shop` prefix: `ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`. Don't introduce alternate naming for these responsibilities.

**Verify:** Inspect theme class declarations and usage.

### DESIGN-14 — MudBlazor primitives

Use MudBlazor UI components. Reusable Shop components may compose or extend them under COMP-23 and COMP-24. If MudBlazor can't meet requirement, present alternatives and get explicit approval before introducing custom UI primitive.

**Verify:** Inspect control implementations and any recorded exception.

### DESIGN-15 — Semantic color priority

Use color in this order:

1. Supported `Color="Color.Enum"` component parameter.
2. Most specific available MudBlazor generated class for needed facet: text, background, border, icon, or hover. Use `mud-theme-{name}` only when both background and text intended.
3. Ask for decision if neither satisfies requirement.

Don't place hex values in `.razor` files. Confirm generated class names against installed theme/library rather than inventing them.

**Verify:** Inspect color parameters and classes; check rendered result.

### DESIGN-16 — MudText content

Render text content with `<MudText Typo="...">`. Don't use raw `<span>`, `<p>`, or `<h1>`–`<h6>` elements for content.

**Verify:** Inspect text markup and rendered typography.

### DESIGN-17 — Text-field labels

Use `Placeholder`, never `Label`, on `MudTextField`. When above-input label required, render sibling `<MudText Typo="Typo.caption">`. Preserve accessible input naming and association using supported component attributes; visual sibling alone doesn't establish that association.

**Verify:** Inspect component parameters, visual placement, rendered accessible name.

### DESIGN-18 — Generated typography utilities

For sizes or weights outside standard typography scale, compose `MudText` with `fs-*` and `fw-*` utilities from `Styles/abstracts/_typography.scss`. Extend existing `$font-sizes` or `$font-weights` lists when needed.

Don't hand-write `.fs-{n}` selectors, use inline `font-size` or `font-weight`, or create one-off page-scoped typography classes.

**Verify:** Inspect markup and SCSS utility source and generated output.

### DESIGN-19 — Semantic custom icons

Use custom SVG icons through `ShopIcons`; Material Icons not used. Name icons by meaning, e.g. `Cart`, not appearance, e.g. `ShoppingBag`.

**Verify:** Inspect icon definitions, naming, usage.

## Web: pages, navigation, and state

### WEB-20 — Code-behind and route attributes

Razor logic beyond ~5 lines belongs in sibling `.razor.cs` partial class. Keep `[Route(Routes.X)]` on partial class; don't use literal `@page "/..."` directives in markup.

Threshold is separation guideline; business logic stays prohibited in pages regardless of length under ARCH-10.

**Verify:** Inspect changed Razor/code-behind pairs and route declarations.

### WEB-21 — Centralized routes

Use `Routes.X` from `TheShop.Web/Common/Routes.cs` for navigation calls, `Href` values, redirect targets. Domain and Application don't own or know navigation URLs. Resolve unsupported dynamic or external destination explicitly rather than silently bypassing routing convention.

**Verify:** Inspect navigation expressions, route constants, layer contracts.

### WEB-22 — Centralized busy state

Pages run busy work through `await BusyState.RunAsync(BusyKeys.X, ...)`. Render spinners through `<BusyFor Key="@BusyKeys.X" Context="busy">` or `<ShopLoadingOverlay />`.

Mount global overlay once in `MainLayout`, keyed to `BusyKeys.Global`. Don't create `_isBusy` fields or magic-string busy keys. Keep `BusyState`, `BusyKeys`, `BusyFor`, `ShopLoadingOverlay` entirely in Web.

**Verify:** Inspect busy-state ownership, keys, execution wrappers, spinner placement.

## Web: reusable components

### COMP-23 — MudComponentBase inheritance

Every reusable component inherits from `MudBlazor.MudComponentBase`, directly or transitively. Use its `Class`, `Style`, `UserAttributes` properties rather than redeclaring them.

**Verify:** Inspect inheritance and parameter declarations.

### COMP-24 — Consumer class and style forwarding

Forward consumer `Class` and `Style` to component root. Use direct passthrough or `CssBuilder`/`StyleBuilder` chains ending in `.AddClass(Class)` and `.AddStyle(Style)` so consumer values appended last. Don't silently drop them. Appending CSS classes doesn't itself guarantee CSS cascade precedence.

**Verify:** Inspect root bindings and check consumer customization reaches rendered root.

### COMP-25 — Extract on demonstrated reuse

Keep UI inline until second real call site justifies extraction. Don't extract just to shorten page, future-proof, or create component with many behavior flags. Use code-behind separation for presentation logic where appropriate.

**Verify:** Identify actual call sites and assess proposed shared responsibility.

## Web: styling

### STYLE-26 — Styling priority

Prefer, in order: MudBlazor parameters; generated MudBlazor color/spacing/flex utilities; genuinely reusable project SCSS class; inline `Style` for one-off last resort.

This fallback doesn't override specific restrictions like DESIGN-15 or DESIGN-18. One-off inline style can't be used to evade prohibited color or typography choices.

**Verify:** Inspect styling choices and any lower-priority fallback justification.

### STYLE-27 — Builder-based composition

Compose classes with `CssBuilder` and inline styles with `StyleBuilder`. Don't string-concatenate, interpolate, or ternary-build class/style strings. Static literal class lists and direct passthrough remain valid; use builders when composing values.

**Verify:** Inspect dynamic class/style expressions and builder usage.

### STYLE-28 — Centralized SCSS

No `<style>` blocks in Razor, no page-scoped CSS files in `wwwroot/`. Author SCSS under `src/TheShop.Web/Styles/` in `abstracts/`, `components/`, `layouts/`, or `utilities/`.

Name partials lowercase with leading underscore. Generate utility families through lists and `@each`; don't hand-write every selector. Existing build outputs aren't alternative location for authored styles.

**Verify:** Inspect style file placement, names, selectors, generated-family source.

## Tests and documentation

### TEST-29 — Meaningful coverage for new behavior

Every new handler, repository, value object, or domain method gets at least one meaningful test in matching `tests/TheShop.{Layer}.Tests/` project. Verify behavior rather than mere construction or implementation structure; add coverage where important branches need it.

Project minimum mandatory even when test-first development selective. Unit tests, functional E2E, visual checks don't automatically substitute for one another; SDD guide defines applicable verification approach.

**Verify:** Map each new artifact to behavioral test and record actual execution results or blockers.

### DOC-30 — Public contract documentation

Public types and members get XML `<summary>` comments describing contracts, not implementation details. Private, internal, test code don't get documentation comments.

Original source references detailed documentation guide not supplied. Follow that guide if it exists in repo and is consistent with this rule; don't invent its additional requirements.

**Verify:** Inspect new or changed public contracts for accurate summaries and non-public/test code for prohibited doc comments.

## Minimal SDD integration

Keep existing four-section feature template:

| Feature section | Constitution usage |
| --- | --- |
| Goal & Scope | List applicable rule IDs and any proposed or approved exceptions. |
| Acceptance Criteria | Include feature-specific observable constraints where needed; don't copy every rule. |
| Implementation Plan | Explain consequential choices needed to satisfy applicable rules. |
| Verification | Link each applicable rule or related group to check and actual result. |

SDD README should require reading this constitution before specifying or implementing feature. Use existing lint, compiler, architecture checks where available, plus focused code/design review for rules needing judgment. Don't claim compliance with unavailable checks.

## Adoption clarifications

This document keeps original 30 rule areas, removing skill front matter, generated-adapter provenance, communication/runtime preloads, reference-routing tables, routine workflow instructions.

These interpretations explicit for owner review before adoption:

- ARCH-10 and WEB-20 distinguish presentation code-behind from Application orchestration and Domain business behavior; line counts don't change ownership.
- TEXT-12 keeps typed-key requirement but flags unresolved `Strings` assembly ownership. No repo references available to settle it.
- DESIGN-17 adds accessible naming to existing visual-label rule.
- STYLE-27 distinguishes static class literals/direct passthrough from dynamic string composition.
- Rule exceptions need explicit owner decisions; original claim to override every conversational instruction removed.

These notes don't assert current codebase complies. Adoption and repo verification stay separate from drafting constitution.
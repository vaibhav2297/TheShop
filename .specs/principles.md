# The Shop — Project Constitution

Canonical location: `.spec/principles.md`  
Status: Proposed for adoption  
Source: The supplied constitution guidelines; repository implementation has not been inspected.

## Purpose and authority

This constitution defines mandatory architecture, design, component, styling, testing, and documentation constraints for The Shop, a premium Canadian e-commerce platform.

Declared stack: .NET 10, Blazor WebAssembly, MudBlazor, Supabase, Stripe, Resend, and Azure Static Web Apps. This is project context supplied by the owner, not a dependency-version verification.

All rules below are mandatory unless the project owner explicitly approves a scoped exception or amendment. Rule numbers preserve the original 1–30 numbering with category prefixes, allowing existing numbered references to be mapped directly.

- Apply relevant rules to changed code. Existing violations do not authorize new ones or require unrelated refactoring.
- If a feature request, Figma design, or existing implementation conflicts with a rule, identify the rule and conflict before implementing the conflicting portion. Continue unaffected work when practical.
- Record approved exceptions in the feature spec with rule ID, reason, scope, approver, and any required follow-up. Do not silently relax a rule or rewrite it to make code compliant.
- Change this constitution only through an explicit owner-approved amendment. It does not override system instructions, access controls, or the owner's authority to revise project policy.

Routine development steps, tool adapters, reference-loading tables, and test commands belong in `.spec/README.md` or existing project documentation, not here. Referenced implementation details that were not supplied with the source must not be invented.

## Architecture

### ARCH-01 — Four layers with inward dependencies

Use `TheShop.Domain`, `TheShop.Application`, `TheShop.Infrastructure`, and `TheShop.Web`.

| Project | Permitted project references | Boundary |
| --- | --- | --- |
| Domain | None | Pure domain model. |
| Application | Domain | Use cases and application contracts. |
| Infrastructure | Application, Domain | Implementations of application contracts and external adapters. |
| Web | Application, Domain, Infrastructure | Infrastructure usage is limited to composition wiring; UI does not consume domain entities. |

`Program.cs` is the composition root that brings the layers together. Keep any layer registration helpers within their own boundaries. Do not use Web's compiler-level references to bypass application contracts.

**Verify:** Inspect project references, cross-layer imports, constructor dependencies, and composition wiring.

### ARCH-02 — Pure Domain

Domain uses pure C# and has no project references. It contains no Supabase, Stripe, MudBlazor, HTTP, JSON, persistence dependencies, or serialization attributes such as `[JsonProperty]`.

**Verify:** Inspect Domain references, imports, attributes, and public contracts.

### ARCH-03 — External SDK isolation

Supabase, Stripe, and Resend SDK use belongs only in `TheShop.Infrastructure`. Their SDK types must not leak through application interfaces or DTOs. For example, `using Supabase;` outside Infrastructure violates this rule.

**Verify:** Inspect package usage, imports, and boundary signatures.

### ARCH-04 — MediatR use cases

Pages dispatch use cases through `IMediator.Send(...)`. Pages must not inject or directly call repositories.

**Verify:** Inspect page dependencies and data-access paths.

### ARCH-05 — Explicit business failures

Return `Result<T>` for expected business failures, such as missing products or insufficient stock. Reserve exceptions for unexpected technical failures.

**Verify:** Inspect handler failure paths and their behavioral tests.

### ARCH-06 — Domain-owned behavior

Domain entities own their business behavior and invariants. For example, cart mutation belongs in `cart.AddItem(...)`; pages, handlers, and services orchestrate rather than duplicate that behavior.

**Verify:** Inspect where rules and mutations live and test their observable outcomes.

### ARCH-07 — Immutable boundary DTOs

Use immutable `record` DTOs at layer boundaries. Do not expose domain entities to the UI. This does not prohibit Application and Infrastructure from using Domain types internally under ARCH-01.

**Verify:** Inspect boundary contracts and UI models for mutable DTOs or entity leakage.

### ARCH-08 — Cancellation across async boundaries

Every async method crossing a layer boundary accepts a `CancellationToken`. Forward it to downstream operations that support cancellation rather than discarding it.

**Verify:** Inspect async contracts and token propagation.

### ARCH-09 — Layer-specific organization

Group Application code by business capability, such as `Features/Cart/` and `Features/Checkout/`.

Group Infrastructure by technical concern: `Persistence/`, `Auth/`, `Payments/`, `Email/`, and `Storage/`. Do not create `Infrastructure/<BusinessFeature>/` folders.

- `Persistence/Records/`, `Persistence/Mappers/`, and `Persistence/Repositories/` hold the corresponding database-specific implementation artifacts.
- The Persistence root holds only cross-cutting database plumbing, such as client factories or shared query extensions.
- Feature-specific database code belongs in the relevant Persistence subfolder.
- Auth, Payments, Email, and Storage are flat adapter folders; do not copy the Records/Mappers/Repositories structure into them.
- Storage-bucket adapters belong in Storage, not Persistence.

**Verify:** Inspect changed file placement against its business or technical responsibility.

### ARCH-10 — Thin pages

Pages contain presentation and interaction orchestration, not business decisions. Put use-case orchestration in Application and domain invariants in Domain under ARCH-06.

A Razor `@code` block over 30 lines is a mandatory extraction/review trigger, not permission to keep smaller business rules in a page. Apply WEB-20 to move presentation logic into code-behind; do not move UI-only state or lifecycle behavior into Application merely to reduce line count.

**Verify:** Inspect both markup and code-behind for business logic. Code-behind separation alone does not make a page architecturally compliant.

## Design: text and resources

### TEXT-11 — Typed localized UI text

Do not hardcode user-facing strings or magic-string resource keys. Use `@Strings.Key` for compile-time-known keys. Use `@Localizer[key]` for runtime keys such as `result.Error`. Literal lookup such as `Localizer["AddToCart"]` is forbidden.

**Verify:** Inspect UI text and resource lookups, including validation and error display.

### TEXT-12 — Typed Application error keys

Application returns resource keys through `nameof(Strings.Key)`, never string literals or localized display text.

This must not introduce an Application-to-Web dependency. Before adopting an implementation, verify where the `Strings` accessor is declared and how Application can reference it legally. If it is Web-only, resolve that ownership conflict with the owner; do not add an outward reference or substitute an unapproved resource strategy.

**Verify:** Inspect error-key expressions and the accessor's defining assembly and dependencies.

## Design: theme and controls

### DESIGN-13 — Shop theme naming

Theme classes use the `Shop` prefix: `ShopColors`, `ShopIcons`, `ShopTypography`, and `ShopTheme`. Do not introduce alternative naming for these responsibilities.

**Verify:** Inspect theme class declarations and usage.

### DESIGN-14 — MudBlazor primitives

Use MudBlazor UI components. Reusable Shop components may compose or extend them under COMP-23 and COMP-24. If MudBlazor cannot meet a requirement, present alternatives and obtain explicit approval before introducing a custom UI primitive.

**Verify:** Inspect control implementations and any recorded exception.

### DESIGN-15 — Semantic color priority

Use color in this order:

1. A supported `Color="Color.Enum"` component parameter.
2. The most specific available MudBlazor generated class for the required facet: text, background, border, icon, or hover. Use `mud-theme-{name}` only when both background and text are intended.
3. Ask for a decision if neither can satisfy the requirement.

Do not place hex values in `.razor` files. Confirm generated class names against the installed theme/library rather than inventing them.

**Verify:** Inspect color parameters and classes; check the rendered result.

### DESIGN-16 — MudText content

Render text content with `<MudText Typo="...">`. Do not use raw `<span>`, `<p>`, or `<h1>`–`<h6>` elements for content.

**Verify:** Inspect text markup and rendered typography.

### DESIGN-17 — Text-field labels

Use `Placeholder`, never `Label`, on `MudTextField`. When an above-input label is required, render sibling `<MudText Typo="Typo.caption">`. Preserve accessible input naming and association using supported component attributes; a visual sibling alone does not establish that association.

**Verify:** Inspect component parameters, visual placement, and the rendered accessible name.

### DESIGN-18 — Generated typography utilities

For sizes or weights outside the standard typography scale, compose `MudText` with `fs-*` and `fw-*` utilities from `Styles/abstracts/_typography.scss`. Extend the existing `$font-sizes` or `$font-weights` lists when needed.

Do not hand-write `.fs-{n}` selectors, use inline `font-size` or `font-weight`, or create one-off page-scoped typography classes.

**Verify:** Inspect markup and the SCSS utility source and generated output.

### DESIGN-19 — Semantic custom icons

Use custom SVG icons through `ShopIcons`; Material Icons are not used. Name icons by meaning, such as `Cart`, rather than appearance, such as `ShoppingBag`.

**Verify:** Inspect icon definitions, naming, and usage.

## Web: pages, navigation, and state

### WEB-20 — Code-behind and route attributes

Razor logic beyond approximately five lines belongs in a sibling `.razor.cs` partial class. Keep `[Route(Routes.X)]` on the partial class; do not use literal `@page "/..."` directives in markup.

This threshold is a separation guideline; business logic remains prohibited in pages regardless of length under ARCH-10.

**Verify:** Inspect changed Razor/code-behind pairs and route declarations.

### WEB-21 — Centralized routes

Use `Routes.X` from `TheShop.Web/Common/Routes.cs` for navigation calls, `Href` values, and redirect targets. Domain and Application do not own or know navigation URLs. Resolve an unsupported dynamic or external destination explicitly rather than silently bypassing the routing convention.

**Verify:** Inspect navigation expressions, route constants, and layer contracts.

### WEB-22 — Centralized busy state

Pages run busy work through `await BusyState.RunAsync(BusyKeys.X, ...)`. Render spinners through `<BusyFor Key="@BusyKeys.X" Context="busy">` or `<ShopLoadingOverlay />`.

Mount the global overlay once in `MainLayout`, keyed to `BusyKeys.Global`. Do not create `_isBusy` fields or magic-string busy keys. Keep `BusyState`, `BusyKeys`, `BusyFor`, and `ShopLoadingOverlay` entirely in Web.

**Verify:** Inspect busy-state ownership, keys, execution wrappers, and spinner placement.

## Web: reusable components

### COMP-23 — MudComponentBase inheritance

Every reusable component inherits from `MudBlazor.MudComponentBase`, directly or transitively. Use its `Class`, `Style`, and `UserAttributes` properties rather than redeclaring them.

**Verify:** Inspect inheritance and parameter declarations.

### COMP-24 — Consumer class and style forwarding

Forward consumer `Class` and `Style` to the component root. Use direct passthrough or `CssBuilder`/`StyleBuilder` chains ending in `.AddClass(Class)` and `.AddStyle(Style)` so consumer values are appended last. Do not silently drop them. Appending CSS classes does not itself guarantee CSS cascade precedence.

**Verify:** Inspect root bindings and check that consumer customization reaches the rendered root.

### COMP-25 — Extract on demonstrated reuse

Keep UI inline until a second real call site justifies extraction. Do not extract only to shorten a page, future-proof, or create a component with many behavior flags. Use code-behind separation for presentation logic where appropriate.

**Verify:** Identify actual call sites and assess the proposed shared responsibility.

## Web: styling

### STYLE-26 — Styling priority

Prefer, in order: MudBlazor parameters; generated MudBlazor color/spacing/flex utilities; a genuinely reusable project SCSS class; inline `Style` for a one-off last resort.

This fallback does not override specific restrictions such as DESIGN-15 or DESIGN-18. A one-off inline style cannot be used to evade prohibited color or typography choices.

**Verify:** Inspect styling choices and any lower-priority fallback justification.

### STYLE-27 — Builder-based composition

Compose classes with `CssBuilder` and inline styles with `StyleBuilder`. Do not string-concatenate, interpolate, or ternary-build class/style strings. Static literal class lists and direct passthrough remain valid; use builders when composing values.

**Verify:** Inspect dynamic class/style expressions and builder usage.

### STYLE-28 — Centralized SCSS

No `<style>` blocks in Razor and no page-scoped CSS files in `wwwroot/`. Author SCSS under `src/TheShop.Web/Styles/` in `abstracts/`, `components/`, `layouts/`, or `utilities/`.

Name partials in lowercase with a leading underscore. Generate utility families through lists and `@each`; do not hand-write every selector. Existing build outputs are not an alternative location for authored styles.

**Verify:** Inspect style file placement, names, selectors, and generated-family source.

## Tests and documentation

### TEST-29 — Meaningful coverage for new behavior

Every new handler, repository, value object, or domain method receives at least one meaningful test in the matching `tests/TheShop.{Layer}.Tests/` project. Verify behavior rather than merely construction or implementation structure; add coverage where important branches require it.

The project minimum is mandatory even when test-first development is selective. Unit tests, functional E2E, and visual checks do not automatically substitute for one another; the SDD guide defines the applicable verification approach.

**Verify:** Map each newly introduced artifact to a behavioral test and record actual execution results or blockers.

### DOC-30 — Public contract documentation

Public types and members receive XML `<summary>` comments describing their contracts, not implementation details. Private, internal, and test code do not receive documentation comments.

The original source references a detailed documentation guide that was not supplied. Follow that guide if it exists in the repository and is consistent with this rule; do not invent its additional requirements.

**Verify:** Inspect new or changed public contracts for accurate summaries and non-public/test code for prohibited doc comments.

## Minimal SDD integration

Keep the existing four-section feature template:

| Feature section | Constitution usage |
| --- | --- |
| Goal & Scope | List applicable rule IDs and any proposed or approved exceptions. |
| Acceptance Criteria | Include feature-specific observable constraints where needed; do not copy every rule. |
| Implementation Plan | Explain consequential choices needed to satisfy applicable rules. |
| Verification | Link each applicable rule or related group to a check and its actual result. |

The SDD README should require reading this constitution before specifying or implementing a feature. Use existing lint, compiler, and architecture checks where available, plus focused code/design review for rules requiring judgment. Do not claim compliance with unavailable checks.

## Adoption clarifications

This document preserves the original 30 rule areas while removing skill front matter, generated-adapter provenance, communication/runtime preloads, reference-routing tables, and routine workflow instructions.

The following interpretations are explicit for owner review before adoption:

- ARCH-10 and WEB-20 distinguish presentation code-behind from Application orchestration and Domain business behavior; line counts do not change ownership.
- TEXT-12 retains the typed-key requirement but flags the unresolved `Strings` assembly ownership. No repository references were available to settle it.
- DESIGN-17 adds accessible naming to the existing visual-label rule.
- STYLE-27 distinguishes static class literals/direct passthrough from dynamic string composition.
- Rule exceptions require explicit owner decisions; the original claim to override every conversational instruction has been removed.

These notes do not assert that the current codebase complies. Adoption and repository verification remain separate from drafting the constitution.

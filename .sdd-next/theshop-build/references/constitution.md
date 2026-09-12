# Constitution

Stable engineering requirements. Workflow lives in `SKILL.md`; this file assigns no agents or pipeline stages. Host instructions and explicit user decisions retain authority. Numbering preserves existing design-check diagnostics.

## Architecture

1. Preserve four layers and inward dependencies. Domain references no projects; Application references Domain; Infrastructure references Application/Domain; Web references Infrastructure only for composition.
2. Domain stays pure C#: business entities, value objects, invariants, domain exceptions. No SDKs, HTTP, JSON, persistence attributes, or external-service interfaces.
3. Supabase, Stripe, Resend, and persistence models stay in Infrastructure behind Application interfaces. No SDK types on public contracts.
4. Pages dispatch use cases through `IMediator.Send`; never call repositories directly.
5. Expected use-case failures return `Result<T>`. Preserve existing domain-invariant exception conventions and translate at Application boundary. Unexpected technical failures remain exceptions.
6. Entities own business behavior. Handlers coordinate; repositories persist; pages render and dispatch.
7. Immutable record DTOs cross presentation boundaries; entities never leak into UI.
8. Async operations crossing layers accept and propagate `CancellationToken`. No blocking `.Result` or `.Wait()`.
9. Application groups commands, queries, handlers, validators, DTOs by business capability. Infrastructure groups by technical concern.
10. Keep business logic out of pages. Move domain rules to Domain and use-case coordination to Application; UI-only interaction stays Web.

## UI and resources

11. All user-facing text comes from resources, including titles, validation, toasts, accessibility text. Static keys use `Strings.Key`; `Localizer[...]` only for runtime keys.
12. Application returns existing compile-safe resource keys, typically `nameof(Strings.Key)`. Preserve current resource wiring; never add dependency on Web.
13. Theme classes use `Shop` prefix: `ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`.
14. Use MudBlazor components. If requirement needs a custom primitive, propose alternative and obtain user decision before implementation.
15. Colors: component `Color` parameter first, specific `mud-*` class second, existing `ShopColors` token last with justification comment. No hardcoded hex in Razor. New design tokens require resolved design choice.
16. Render content through `MudText` with explicit `Typo`. No native text elements such as `span`, `p`, or headings for content.
17. `MudTextField` uses `Placeholder`, never `Label`; visible label uses sibling `MudText Typo="Typo.caption"`.
18. Typography uses existing `fs-*`/`fw-*` utilities. Extend SCSS lists for required sizes/weights; no inline typography or one-off selector families.
19. Icons use semantic `ShopIcons` entries, never `Icons.Material`.
20. Pages place route attributes on `.razor.cs` partials: `[Route(Routes.X)]`, never literal `@page`. Move substantial UI logic (more than about five lines) to code-behind.
21. Navigation uses `Routes` constants/helpers. Routes remain Web-only.
22. Busy UI uses `BusyState.RunAsync(BusyKeys.X, ...)`, `BusyFor`, or existing global `ShopLoadingOverlay`. No per-page `_isBusy`; these primitives remain Web-only.
23. Reusable components inherit `MudComponentBase` directly or transitively.
24. Reusable components forward `Class` and `Style` to root; consumer values apply last.
25. Inline first; extract on second real call site. No speculative components or extraction merely to shorten a file.
26. Styling priority: Mud parameters, Mud utility classes, reusable project SCSS, justified one-off inline style.
27. Compose conditional classes/styles using `CssBuilder`/`StyleBuilder`, never string interpolation, concatenation, or ternary-built strings.
28. SCSS stays under `Web/Styles/`; lowercase underscore partials. No Razor `<style>` blocks or page CSS under `wwwroot`. Generate utility families from lists.

## Tests and documentation

29. Every new handler, repository, value object, or domain method has meaningful tests in matching test project. Verification policy assigns unit, component, integration, and E2E proof by behavior.
30. Public types and non-obvious public contracts receive useful XML documentation during implementation. Describe contract, not member name. No private/internal/test/generated documentation ceremony.

## Security and delivery

Blazor WebAssembly is public client code. Keep secrets and privileged operations server-side. Enforce data access through backend authorization/RLS; hiding controls is insufficient. Load security reference for affected work.

Changed resource keys need complete English and French text; placeholders block delivery. Preserve architecture and UI rules during repairs. Explicit exceptions stay recorded; never silently suppress diagnostics.

Commit format: `{Type} | {Description}`. No AI attribution in commit, PR, or review text. Message generation grants no Git mutation permission.

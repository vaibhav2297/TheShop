# Checklist — Design

Verification checklist for the canonical constitution. Apply native requirements to new and converted surfaces. Record remaining legacy consumers during migration; do not remove their providers before replacements are verified.

## Strings and boundaries

- [ ] User-facing text, accessible labels, and errors use typed Strings accessors or runtime resource keys.
- [ ] English-only resources remain intact; generated accessors are not edited.
- [ ] Routes stay centralized and authorization/business contracts remain unchanged.
- [ ] Pages dispatch through Application; common controls do not fetch feature data.

## Theme and styling

- [ ] Shared design values belong to Styles/tokens; component-only values stay in their owning partial and custom properties are scoped to the component, not :root.
- [ ] Temporary C# theme ownership is limited to existing Mud consumers and tracked for removal.
- [ ] Project classes use component-prefixed kebab-case, with no BEM double underscores or double hyphens.
- [ ] Static styles stay in the correct centralized SCSS partial; no Razor style blocks or new CSS isolation.
- [ ] Sass modules use @use, local namespaces, and module-qualified built-ins.
- [ ] Selectors remain shallow; no routine !important or global resets that break unconverted controls.
- [ ] Typography uses semantic HTML; visual size does not dictate heading level.
- [ ] Icons use trusted ShopIcons fragments, not user HTML or external SVG strings.
- [ ] Conditional classes use the small project helper; class order is not claimed to override CSS specificity.
- [ ] Shared utility families are generated from maps/lists only when actually needed.

## Components and forms

- [ ] Extractions have repeated behavior or established design-consistency responsibility.
- [ ] ComponentBase or ShopComponentBase owns presentation; validated inputs use built-in controls or InputBase.
- [ ] Class, Style, and unmatched attributes reach the documented element.
- [ ] Enforced disabled/type/accessibility attributes cannot be negated by attribute splatting.
- [ ] Buttons default to type=button; submit buttons are explicit; navigation uses anchors.
- [ ] Native controls retain keyboard activation, visible focus, disabled, hover, and pressed states.
- [ ] Focus remains visible in forced colors; reduced-motion preferences are respected.
- [ ] Inputs have associated labels; placeholders are supplementary.
- [ ] Hints/errors are associated; aria-invalid and validation messages agree.
- [ ] EditContext notifications, parsing, validation, and binding timing match the required behavior.
- [ ] Submit paths await validation and guard duplicate work; failed forms do not dispatch commands.
- [ ] Dialog focus, dismissal, cancellation, and completion are verified where affected.

## Lifecycle and loading

- [ ] Code-behind and centralized routes remain intact.
- [ ] BusyState/BusyKeys/BusyFor remain the work-state source; no page-local _isBusy.
- [ ] Loading indicators are scoped through BusyFor or ShopLoadingOverlay.
- [ ] Events, JS references, cancellation registrations, and subscriptions are disposed.
- [ ] Image geometry is reserved before loading; failures keep meaningful alt/placeholder semantics.
- [ ] Image presets and mobile artwork rules survive source changes and failure recovery.

## Verification

- [ ] Behavioral tests target native controls and preserved outcomes, not removed Mud internals.
- [ ] Converted controls render correctly without vendor CSS.
- [ ] Desktop/mobile, zoom, long content, keyboard focus, and validation states are checked.
- [ ] Supplied Figma nodes and screenshots were inspected; browser results are compared with those references.
- [ ] Inferred states and accessibility deviations are recorded, not claimed as exact Figma parity.
- [ ] Existing and converted consumers work during coexistence.
- [ ] Unavailable checks are reported honestly, not treated as passed.

Use code-generation.md for architecture, tests, and public API documentation checks.

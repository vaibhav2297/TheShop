# Web guidance

Read for UI, resources, styles, theme, or navigation. Constitution owns component and token constraints.

Use supplied design references where present. Otherwise confirm reuse of existing visual patterns during Understand. Resolve new layout/token choices before dependent implementation; do not require Figma for every UI fix.

Accept Figma links at invocation or later during Understand. Prefer exact frame/node links; map each to screen, state, and viewport in feature record. For a broad file link, inspect relevant frames or ask which ones define acceptance. Fetch only scoped nodes and necessary styles/variables using available Figma capability; do not invent connector calls or infer design from URL alone.

Check access before promising parity. Inaccessible link: report missing access, request accessible reference or user-approved screenshot/reuse alternative, and continue independent work. Record agreed alternative; never silently substitute it. If design changes after confirmation, reconcile affected acceptance before implementing those changes. Retain original URL and node identifiers for final visual comparison.

Render loading, empty, success, validation, and failure states relevant to acceptance. Preserve keyboard navigation, focus, accessible names, disabled/loading behavior, and supported viewport layout. Use existing components before extracting new ones.

Use `Routes` helpers for parameterized URLs and `BusyKeys` constants for actions. Keep feature-specific imports local to Razor file. Code-behind owns UI interactions, not business rules.

Resource keys follow `{Context}_{Purpose}` and remain valid C# identifiers. Reuse existing keys with same meaning. Update `Strings.resx` and `Strings.fr.resx` together, preserving format placeholders. Never edit/create `Strings.Designer.cs`. Include email, metadata, image alt text, and ARIA text when user-facing. Runtime failures localize returned keys at presentation boundary.

Theme registries live under `Web/Theme/`; token registries are static, `ShopTheme` is an instance. Prefer most specific color class: `mud-error-text` for text, not whole-surface theme class. New typography structure such as font family or line height requires resolved design choice.

SCSS folders: `abstracts/`, `components/`, `layouts/`, `utilities/`. Reuse existing classes; generate required utility families through lists and `@each`. One-off styling needs justified `StyleBuilder`; no inline font-size/font-weight/line-height.

Use WebP for raster assets where appropriate, explicit dimensions, localized alt text, and lazy loading where appropriate to placement. Use `MudImage` where it fits. Preserve existing SVG icon system.

For visual acceptance, compare running UI with agreed reference at relevant viewport. Automated browser assertions prove interactions; screenshots plus inspection prove layout aspects. Unit tests cannot substitute for browser journey proof.

# Design — Components

Implementation guidance for native component contracts. Use approved Figma components for appearance, semantic HTML for structure, and Blazor for behavior. This reference adds no workflow stage or gate.

## Choose the smallest useful component

Use headings, paragraphs, lists, anchors, sections, Grid, and Flexbox directly. Do not recreate vendor layout/text wrappers such as `ShopStack`, `ShopGrid`, or `ShopText`.

Extract repeated UI and behavior with a stable responsibility. Keep single-use layout inline. Share repeated logic through a helper or state object when a visual component adds nothing. Avoid speculative flags, generic frameworks, and extraction solely to shorten a file. Search existing controls such as `ShopButton` and `ShopImage` before adding another.

## Base and attribute forwarding

| Need | Base |
|---|---|
| Render-only content or local behavior | `ComponentBase` |
| Shared visual-root class, style, and attribute contract | `ShopComponentBase` |
| Custom editable value participating in an `EditContext` | `InputBase<TValue>` |
| Layout | `LayoutComponentBase` |

`ShopComponentBase` supplies only `Class`, `Style`, and `AdditionalAttributes`. Do not turn it into a validation, loading, service-location, or rendering framework. Prefer Blazor built-in inputs before a custom `InputBase<TValue>` control.

Compose conditional classes in code-behind:

```csharp
private string ClassName => ShopCssClass.Join(
    "shop-product-card",
    Compact ? "shop-product-card-compact" : null,
    Class);
```

For enum-based modifiers, use `ShopCssClass.Modifier("shop-button", Size)` within `Join` instead of repeating switches. It emits component-prefixed kebab-case, rejects undeclared enum values, and leaves SCSS ownership with the component. It is a class helper, not a style-value or rendering framework.

Forward to the documented root:

```razor
<article @attributes="AdditionalAttributes"
         class="@ClassName"
         style="@Style">
    @ChildContent
</article>
```

For native elements, Blazor resolves duplicate attributes from right to left. Place the attribute splat before explicit component-owned attributes so a consumer cannot replace enforced `disabled`, `type`, event handlers, or identity markers. When merging dictionaries, use case-insensitive keys and apply enforced values last. Test precedence; class-name order does not determine CSS precedence.

`Class` and `Style` target the documented root. Inputs forward `id`, `name`, `aria-*`, and native input attributes to the actual input, not its wrapper. Expose wrapper styling separately only when a caller needs it.

## Actions and state

Use `ShopButton` for actions and anchors with centralized `Routes` values for navigation. Do not nest interactive elements or make generic `div` elements imitate buttons. `ShopButton` defaults to `type="button"`; opt into submission explicitly.

```razor
<ShopButton Type="submit"
            Variant="ShopVariant.Filled"
            Disabled="@busy">
    @Strings.Save
</ShopButton>
```

The `busy` value above comes from `BusyFor`, not a page-owned flag. Icon-only actions need resource-backed accessible names. Use trusted `ShopIcons` fragments; decorative icons remain hidden from assistive technology.

Use `ShopIconButton` for icon-only actions. Supply nonblank `Icon` and resource-backed `Label`; it composes `ShopButton` and `ShopIcon` without adding a DOM wrapper. It supports all shared colors, variants, and Small/Medium/Large sizes, defaulting to Primary/Filled/Medium. `Label` owns the accessible name and overrides conflicting ARIA naming attributes. Icon-button geometry belongs in `_icon-button.scss`, independent of text-button sizes; color/state behavior remains shared. The inspected variant-specific size table and one inferred Figma variant name are recorded in `UI_MIGRATION_GUIDE.md` section 6.2.

Shared visual choices live in `Common/UI/`: `ShopColor` selects a color role, `ShopVariant` selects Filled/Outlined/Text treatment, and `ShopSize` selects a component-relative size. `ShopButton` defaults to Primary/Filled/Medium. Components opt into only applicable choices; do not add these parameters to `ShopComponentBase` or force a shared enum onto a different concept such as dialog width. Enums contain no color values. Owned SCSS selects contrast-token labels for filled treatments and base-role labels for outlined/text; check actual contrast separately and retain the documented temporary palette limitations. Select colors through `Color`, not shortcut classes that override the variant; destructive intent maps to `ShopColor.Error`, while image-overlay buttons use `ShopColor.Surface`.

Handle applicable default, hover, pressed, focus, disabled, loading, and error states. Native `disabled` prevents browser activation; the handler also guards disabled dispatch. Disabled appearance or `aria-disabled` alone does not prevent activation. Figma guides variants; missing keyboard/loading/error states still require accessible treatment.

## Dialog composition

`ShopDialog` accepts `TitleContent`, `DialogContent`, and `DialogActions`. Title markup supplies the accessible name through the component's unique wrapper ID; callers provide a semantic heading and localized text. Only the middle content region scrolls; header/actions remain visible. Keep safe initial focus on an action carrying `data-dialog-initial-focus` and preserve native modal dismissal/focus restoration.

Use the separate shared `ShopMaxWidth` enum for `MaxWidth`, not control `ShopSize`. Omitted/null preserves the 500px theme default; explicit choices consume the SCSS sizing scale, and `None` retains viewport gutters without a named cap. Do not put pixel widths or per-value switches in Razor/C#.

## Notifications

Inject `IShopNotificationService` for operation feedback and call `Show` with localized plain text plus `ShopNotificationKind`. The scoped service owns bounded messages/timers; `ShopUiHost` owns the single `ShopNotificationHost`. Do not reintroduce `ISnackbar` or snackbar providers. Preserve existing inline validation and operation failure channels.

All current operation results use a polite live region. Notifications never take focus; hover or focus pauses expiry. Text is encoded, with a resource-named dismiss action. Keep notification kinds in the API/model, but render one identical bar for every kind: Figma Snackbar `2948:17575`, primary background, primary-contrast text/icon, subtitle-2 typography, 24px padding/gap, and an 18px close icon. No visible kind labels or severity accents. Place bottom-center at notification layer 1500; native modal top-layer ordering still wins. Numeric layout tokens belong in `_theme.scss`, appearance in `_notification.scss`. See `UI_MIGRATION_GUIDE.md` section 8.3 for timing, overflow, navigation, and accessible dismiss sizing.

## Labels and validation

Associate visible labels with input IDs. Placeholders supplement labels; they never replace them. Associate helper/error text using `aria-describedby`, and expose invalid state when applicable. Group related choices with `fieldset` and `legend`.

```razor
<label class="shop-field-label" for="email">@Strings.Email_Label</label>
<InputText id="email"
           class="shop-field-input"
           @bind-Value="Model.Email"
           aria-describedby="email-hint" />
<span id="email-hint" class="shop-field-hint">@Strings.Email_Hint</span>
<ValidationMessage For="@(() => Model.Email)" />
```

This example belongs in an `EditForm`. Add a stable error ID to the described-by relationship when the chosen error renderer supports it. Repeated instances need unique IDs, not copies of the example ID.

Custom `InputBase<TValue>` controls preserve `Value`, `ValueChanged`, `ValueExpression`, parsing errors, field notification, and the surrounding `EditContext`. Do not replace validated input behavior with an unrelated component wrapper. Preserve existing required, culture, numeric, date, and selection semantics.

Await asynchronous validation from the submission path; avoid `async void` validation handlers. Do not dispatch before validation completes. Application failures remain resource keys translated in Web; do not redesign the result contract for a control replacement.

## Busy state

Pages start operations through `BusyState.RunAsync` with `BusyKeys` constants. `BusyFor` renders keyed disabled/loading state. Do not introduce parallel page-owned `_isBusy` flags. `ShopButton.Loading` receives this value and owns spinner-only presentation, stable dimensions, `aria-busy`, the disabled activation guard, and a visually hidden status. `ShopIconButton` forwards `Loading` to it; neither control starts or tracks operations.

```razor
<BusyFor Key="@BusyKeys.Auth.SignIn" Context="busy">
    <ShopButton Loading="@busy" OnClick="OnSubmitAsync">
        @Strings.Auth_SendCode
    </ShopButton>
</BusyFor>
```

Keep label and icons mounted in the button's internal content span, visually transparent while loading, so dimensions and the accessible name remain stable. A decorative spinner is centered over it. Effective disabled state is `Disabled || Loading`; preserve independent disabled conditions after completion. A primed, visually hidden status sibling announces `Strings.Loading` outside the busy button without changing its name. Do not add duplicate page-level spinners/status markup. Other inline indicators still belong inside their `BusyFor` fragment. The app-blocking overlay observes `BusyKeys.Global` and is mounted once in the active layout. Authentication and main layouts must not create duplicate active hosts.

## Code-behind and lifecycle

Keep markup in `.razor` and substantial state, parameters, handlers, lifecycle, and disposal in `.razor.cs`. Pages declare `[Route(Routes.X)]` in code-behind rather than literal `@page` paths. Components do not bypass the Application boundary to call persistence.

Preserve route cancellation, event unsubscription, JS-module disposal, and existing navigation behavior when changing markup. Local transient interaction state does not transfer business behavior or shared operation ownership into the component.

## Migration coexistence

Existing Mud consumers, providers, packages, assets, and registrations may remain until their final consumers are migrated and verified. Do not extend the dependency or require vendor CSS for native controls. Scope native and legacy styles so adding a primitive does not silently restyle unmigrated forms.

Migrate behavior-focused tests with each control: native semantics, events, binding, accessible names, and resources replace vendor-component assertions. Retain source-specific image failures, upload cleanup, dialog completion, and keyboard contracts. Appearance alone is not preservation proof.

Historical Mud-only inheritance, placeholder-only labels, and vendor-builder examples are legacy references, not instructions for new native components. Follow the current constitution and this reference.

# Example — Native reusable Web component

Use the existing `src/TheShop.Web/Components/Common/ShopButton.razor` and `.razor.cs` as the visual-control example. For a composed feature view, read `Components/Products/ProductCard.razor` and `.razor.cs`. These are working implementations, not instructions to create another wrapper.

## Visual component root

`ShopButton` derives from `ShopComponentBase`, whose only shared parameters are `Class`, `Style`, and `AdditionalAttributes`. The actual button root follows this pattern:

```razor
<button @attributes="AdditionalAttributes"
        type="@ButtonType"
        class="@ClassName"
        style="@Style"
        disabled="@Disabled"
        @onclick="OnClickAsync">
    @ChildContent
</button>
```

The full implementation also renders optional `ShopIcon` children using trusted `ShopIcons` fragments. Its internal content span preserves label/icon geometry and accessible naming while `Loading` shows only a centered spinner. Effective disabling is `Disabled || Loading`; the component owns `aria-busy` and a visually hidden status sibling. `ShopIconButton` forwards `Loading` to this shared implementation. The attribute splat precedes component-owned attributes so callers cannot override the effective type, disabled state, or handler. The handler also guards disabled dispatch. Default type is `button`, not `submit`.

Use `ShopCssClass.Join` for component-prefixed class composition in code-behind. ProductCard's actual composition is:

```csharp
private string ClassName => ShopCssClass.Join("shop-native", "shop-product-card", Class);
```

Forward styles and attributes to the documented root. Class-name order does not determine CSS specificity or override priority. Static component appearance belongs in SCSS, not a generated inline-style builder.

## Existing consumer example

ProductCard renders an independently named wishlist action:

```razor
<ShopIconButton Color="ShopColor.Surface"
                Class="shop-product-card-action shop-product-card-wishlist"
                Label="@Strings.Wishlist_Add"
                Icon="@ShopIcons.Outlined.Heart_01"
                OnClick="OnToggleWishlistAsync" />
```

The icon is decorative; the button owns its accessible name. `ShopIconButton` composes the shared native button without a DOM wrapper and defaults to Medium; select `Size="ShopSize.Small"` or `Size="ShopSize.Large"` when appropriate. Each variant has independently inspected icon-button geometry. Keep cart, wishlist, and selection actions separate rather than nesting buttons or links. Navigation uses a native anchor with a centralized route; a button remains appropriate for an existing callback-only action without an implemented destination.

Use `ShopColor` for the color role, `ShopVariant.Filled`, `.Outlined`, or `.Text` for treatment, and `ShopSize` for relative size. These choices are independent; component-owned SCSS defines their appearance. Components expose only meaningful choices rather than inheriting unused parameters from the base. Do not invent properties based on historical vendor components. Component classes use single hyphens (`shop-button-icon`, `shop-button-small`); theme values use CSS variables (`--shop-color-primary`).

## Validated input is a different contract

Read `Components/Common/ShopTextField.razor` and `.razor.cs` for the input example. It derives from `InputBase<string?>`, not `ShopComponentBase`, and binds `CurrentValueAsString` on `oninput`, guarding disabled updates. This preserves binding, parsing, field notifications, validation classes, and `EditContext` behavior while supporting immediate updates required by existing forms.

Input attributes target the actual input. The component owns the required floating Label, optional StartIcon/HelperText, generated or caller-supplied input ID, helper/error associations, and EditContext-driven ValidationMessage. It has no Variant/Placeholder option and renders no visible placeholder. The owning form supplies binding, validation rules, and a disabled value from `BusyFor`. Prefer built-in Blazor inputs when their update behavior already meets the requirement; do not wrap every native element.

## What stays outside the component

Business rules remain in Domain/Application; shared work state stays in `BusyState`. A visual base must not absorb services, form validation, loading, or arbitrary rendering policy. Extract repeated behavior or stable composition, not a single-use layout merely to shorten a parent.

During migration, unconverted controls may retain Mud dependencies. New native controls use owned SCSS and must work without vendor CSS; remove transitional bridges with their last consumers.

## Verification references

`ShopButtonTests.cs`, `ShopIconButtonTests.cs`, `ShopIconTests.cs`, and `ProductCardTests.cs` under `tests/TheShop.Web.Tests/Components/` cover native semantics, event callbacks, attribute precedence, resource-backed names, sizes, and content behavior. `Common/UI/ShopCssClassTests.cs` covers the reusable enum modifier helper. Verify focus, forced colors, Figma sizing, and responsive presentation in a browser; markup assertions alone do not prove appearance.

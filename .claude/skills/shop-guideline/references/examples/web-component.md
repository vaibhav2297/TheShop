# Example — Reusable Web component

Canonical reusable component using `MudComponentBase` + Pattern B (`CssBuilder` / `StyleBuilder`). Shows Rules 23, 24, 27 in one file.

### `.razor`

```razor
@* Components/Common/ShopAlert.razor *@
@inherits MudComponentBase

<MudPaper Class="@Classname"
          Style="@Stylename"
          Elevation="0">
    @if (Icon is not null)
    {
        <MudIcon Icon="@Icon" Class="me-2" />
    }
    <MudText Typo="Typo.body2">
        @ChildContent
    </MudText>
</MudPaper>
```

### `.razor.cs`

```csharp
// Components/Common/ShopAlert.razor.cs
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace TheShop.Web.Components.Common;

public partial class ShopAlert : MudComponentBase
{
    [Parameter] public string? Icon { get; set; }
    [Parameter] public bool Dense { get; set; }
    [Parameter] public bool Square { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected string Classname =>
        new CssBuilder("mud-alert")
            .AddClass("mud-dense", Dense)
            .AddClass("mud-square", Square)
            .AddClass(Class)               // consumer's Class last — Rule 24
            .Build();

    protected string Stylename =>
        new StyleBuilder()
            .AddStyle("margin-top", "4px")
            .AddStyle(Style)               // consumer's Style last — Rule 24
            .Build();
}
```

### Consumer call site

```razor
<ShopAlert Icon="@ShopIcons.Info"
           Dense="true"
           Class="my-2"                       @* consumer wins on the same axis if overlap *@
           Style="max-width: 480px;">
    @Strings.Cart_EmptyHint
</ShopAlert>
```

Highlights:
- `MudComponentBase` inheritance gives `Class`, `Style`, `UserAttributes` to the component for free (Rule 23). No need to redeclare those parameters.
- Both `Classname` and `Stylename` builders end with `.AddClass(Class)` / `.AddStyle(Style)` so the consumer's value wins last (Rule 24).
- Conditional classes use `CssBuilder.AddClass(name, condition)` — no string interpolation (Rule 27).
- Text content uses `<MudText Typo="...">` (Rule 16). No raw `<p>` or `<span>`.
- Icon comes from `ShopIcons` (Rule 19); the component renders `<MudIcon Icon="@Icon" />` so consumers pass `ShopIcons.Info`, `ShopIcons.Warning`, etc.

### Pattern A alternative — root has no internal classes/styles

If a wrapper component genuinely has no internal styling, the simpler pattern works — pass `Class` and `Style` straight through:

```razor
@* Components/Common/ShopSection.razor *@
@inherits MudComponentBase

<MudGrid Class="@Class" Style="@Style">
    @ChildContent
</MudGrid>
```

Use Pattern A only when the root element has no class or style of its own.

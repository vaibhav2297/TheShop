# Example — Web page

Canonical Blazor page split into `.razor` (markup) + `.razor.cs` (logic). Shows the **dispatch via IMediator → handle Result → update state + toast** pattern enforced by Rules 4, 10, 11, 16, 19, 20, 21, 22.

### `.razor`

```razor
@* Pages/Products/ProductDetail.razor *@
@using TheShop.Web.Resources
@using TheShop.Web.Components.Common

<PageTitle>@Strings.ProductDetail_PageTitle</PageTitle>

@if (Product is null)
{
    <ShopLoadingOverlay />
}
else
{
    <MudCard>
        <MudText Typo="Typo.h4">@Product.Name</MudText>
        <MudText Typo="Typo.body1">@Product.Price.Format()</MudText>

        <BusyFor Key="@BusyKeys.Cart.AddItem" Context="busy">
            <MudButton OnClick="@AddToCartAsync"
                       Color="Color.Primary"
                       Variant="Variant.Filled"
                       StartIcon="@ShopIcons.Cart"
                       Disabled="@busy">
                @if (busy)
                {
                    <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
                }
                @Strings.AddToCart
            </MudButton>
        </BusyFor>
    </MudCard>
}
```

### `.razor.cs`

```csharp
// Pages/Products/ProductDetail.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MediatR;
using MudBlazor;
using TheShop.Application.Features.Cart.Commands;
using TheShop.Application.Features.Products.Queries;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;
using TheShop.Web.Theme;

namespace TheShop.Web.Pages.Products;

[Route(Routes.Products.Detail)]
public partial class ProductDetail : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private CartState Cart { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    [Parameter] public string Slug { get; set; } = "";

    protected ProductDto? Product { get; private set; }
    private int _quantity = 1;

    protected override async Task OnInitializedAsync()
    {
        var result = await Mediator.Send(new GetProductBySlugQuery(Slug));
        if (result.IsSuccess) Product = result.Value;
    }

    private async Task AddToCartAsync()
    {
        if (Product is null) return;

        await BusyState.RunAsync(BusyKeys.Cart.AddItem, async () =>
        {
            var result = await Mediator.Send(new AddToCartCommand(Product.Id, _quantity));

            if (result.IsSuccess)
            {
                Cart.UpdateFromDto(result.Value);
                Snackbar.Add(Strings.AddedToCart, Severity.Success);
            }
            else
            {
                Snackbar.Add(Localizer[result.Error], Severity.Error);
            }
        });
    }
}
```

Highlights:
- Route lives on the code-behind via `[Route(Routes.Products.Detail)]` — no `@page "/..."` in markup (Rule 20).
- All text comes from `Strings.{Key}` (Rule 11). The runtime error key from `result.Error` uses `Localizer[...]` — the only legitimate indexer use.
- Icon comes from `ShopIcons.Cart` (Rule 19).
- Page dispatches via `Mediator.Send` only — never injects a repository (Rule 4).
- Markup uses `<MudText Typo="...">` for content (Rule 16). No `<p>`, `<h1>`, `<span>`.
- Busy state goes through `BusyState.RunAsync(BusyKeys.Cart.AddItem, ...)` and the UI surfaces it via `<BusyFor>` (Rule 22). No `_isBusy` field.
- `@code` block in the `.razor` is absent — all logic lives in `.razor.cs` (Rule 20).
- Constructor injection via `[Inject]` properties (Blazor's convention for pages); the primary-constructor pattern from `architecture-core.md` applies to plain classes — pages use `[Inject]` because Blazor's lifecycle needs property injection.
- Colour via `Color.Primary` enum, not a class or hex (Rule 15).

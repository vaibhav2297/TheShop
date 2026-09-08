using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// The manage-products admin list: a simple paged table — newest first, published and
/// unpublished alike, no search/filter/sort/bulk selection (AC-1, AC-2, AC-35). Requires a
/// signed-in user via <c>Pages/Admin/_Imports.razor</c>, and is gated on <c>products.view</c>
/// within the page itself.
/// </summary>
[Route(Routes.Admin.ManageProducts)]
[AuthorizePermission("products.view")]
public partial class ManageProducts : ComponentBase
{
    private const int PageSize = 10;

    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    private Paginator<ProductListItemDto> _products = default!;

    /// <inheritdoc/>
    protected override Task OnInitializedAsync()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Current(Strings.ManageProducts_Heading));
        _products = new Paginator<ProductListItemDto>(FetchProductsAsync, PageSize);

        return BusyState.RunAsync(BusyKeys.Products.ManageList, () => _products.LoadAsync());
    }

    private async Task<PagedResult<ProductListItemDto>> FetchProductsAsync(
        PaginationRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetAdminProductsPageQuery(request), ct);

        if (result.IsSuccess)
            return result.Value;

        Snackbar.Add(Localizer[result.Error!], Severity.Error);
        return PagedResult<ProductListItemDto>.Empty(request);
    }

    private Task OnPageChangedAsync(int page) =>
        BusyState.RunAsync(BusyKeys.Products.ManageList, () => _products.GoToAsync(page));

    /// <summary>
    /// Renders a row's price in the shop's currency: a "from" price for a product that prices
    /// through its variants (RULE-19), its effective price otherwise, and a placeholder when the
    /// product carries no price at all.
    /// </summary>
    private static string FormatPrice(ProductListItemDto product)
    {
        if (product.HasVariants)
            return product.MinVariantPrice is { } min
                ? string.Format(Strings.ManageProducts_PriceFrom, CurrencyFormatter.Format(min, product.Currency))
                : Strings.NotAvailable;

        return product.EffectivePrice is { } price
            ? CurrencyFormatter.Format(price, product.Currency)
            : Strings.NotAvailable;
    }
}

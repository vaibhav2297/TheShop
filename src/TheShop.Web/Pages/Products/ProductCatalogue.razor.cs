using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetCatalogueFilters;
using TheShop.Application.Features.Products.Queries.GetProductCataloguePage;
using TheShop.Domain.Enums;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Products;

/// <summary>
/// The public product catalogue — a paginated, filterable, sortable grid of published
/// products. Filters and sort options are fetched once; every filter, sort, or page change
/// resets to page 1 and re-sends <see cref="GetProductCataloguePageQuery"/> with the current
/// criteria. Add-to-Cart, Wishlist, and card-body navigation are display-only for this
/// feature — their callbacks are wired by the Cart, Wishlist, and product-detail features.
/// </summary>
[Route(Routes.Products)]
public partial class ProductCatalogue : ComponentBase
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    private CatalogueFiltersDto? _filters;
    private Paginator<ProductSummaryDto> _products = default!;

    private IReadOnlyList<AppliedFilterDto> _selectedFilters = [];
    private decimal? _priceMin;
    private decimal? _priceMax;
    private ProductSortOption _sort = ProductSortOption.NewestFirst;

    private IReadOnlyList<FilterGroupDto> FilterGroups => _filters?.Groups ?? [];

    private int ResultRangeStart => ((_products.Page - 1) * _products.PageSize) + 1;

    private int ResultRangeEnd => ResultRangeStart + _products.Items.Count - 1;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Storefront().Current(Strings.Nav_Products));

        _products = new Paginator<ProductSummaryDto>(FetchProductsAsync);

        await BusyState.RunAsync(BusyKeys.Products.Catalogue, async () =>
        {
            // The filter sidebar and the first product page are independent reads — issue both
            // at once so the page waits on a single round-trip instead of two back-to-back ones.
            var filtersTask = Mediator.Send(new GetCatalogueFiltersQuery());
            var productsTask = _products.LoadAsync();

            await Task.WhenAll(filtersTask, productsTask);

            var filtersResult = await filtersTask;
            if (filtersResult.IsSuccess)
                _filters = filtersResult.Value;
            else
                Snackbar.Add(Localizer[filtersResult.Error!], Severity.Error);
        });
    }

    private async Task<PagedResult<ProductSummaryDto>> FetchProductsAsync(
        PaginationRequest request, CancellationToken ct)
    {
        var query = new GetProductCataloguePageQuery(
            _selectedFilters, _priceMin, _priceMax, _sort, request);

        var result = await Mediator.Send(query, ct);

        if (result.IsSuccess)
            return result.Value;

        Snackbar.Add(Localizer[result.Error!], Severity.Error);
        return PagedResult<ProductSummaryDto>.Empty(request);
    }

    private Task OnFiltersChangedAsync(IReadOnlyList<AppliedFilterDto> filters)
    {
        _selectedFilters = filters;
        return ResetPageAsync();
    }

    private Task OnPriceMinChangedAsync(decimal? value)
    {
        _priceMin = value;
        return ResetPageAsync();
    }

    private Task OnPriceMaxChangedAsync(decimal? value)
    {
        _priceMax = value;
        return ResetPageAsync();
    }

    private Task OnSortChangedAsync(ProductSortOption sort)
    {
        _sort = sort;
        return ResetPageAsync();
    }

    private Task OnPageChangedAsync(int page) =>
        BusyState.RunAsync(BusyKeys.Products.Catalogue, () => _products.GoToAsync(page));

    private Task OnClearFiltersAsync()
    {
        _selectedFilters = [];
        _priceMin = null;
        _priceMax = null;
        return ResetPageAsync();
    }

    private Task ResetPageAsync() =>
        BusyState.RunAsync(BusyKeys.Products.Catalogue, () => _products.ResetAsync());
}

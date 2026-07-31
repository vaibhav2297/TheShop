using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetCatalogueFilters;
using TheShop.Application.Features.Products.Queries.GetProductCataloguePage;
using TheShop.Domain.Enums;
using TheShop.Web.Common;
using TheShop.Web.Common.Sorting;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Products;

/// <summary>
/// The public product catalogue — a paginated, filterable, sortable grid of published
/// products. Filter/sort/page state is deep-linked through the URL query string
/// (<see cref="CatalogueQueryState"/>): every change writes the URL, and a single
/// <see cref="ApplyStateAsync"/> path re-sends <see cref="GetProductCataloguePageQuery"/> with
/// the current criteria — whether the change came from the user, a shared link, or browser
/// Back/Forward. Filters and sort reset to page 1. Add-to-Cart, Wishlist, and card-body
/// navigation are display-only for this feature — their callbacks are wired by the Cart,
/// Wishlist, and product-detail features.
/// </summary>
[Route(Routes.Products)]
public partial class ProductCatalogue : QueryStatePageBase<CatalogueQueryState>
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
    private ProductSortOption _sort = ProductSortCatalogue.Instance.Default;

    private IReadOnlyList<FilterGroupDto> FilterGroups => _filters?.Groups ?? [];

    /// <summary>
    /// The price bounds in the panel's group-keyed range shape. The catalogue's own state stays
    /// price-specific — <see cref="CatalogueQueryState"/> and
    /// <see cref="GetProductCataloguePageQuery"/> both name price explicitly — so the translation
    /// to and from the feature-agnostic panel happens here, at the component boundary.
    /// </summary>
    private IReadOnlyList<RangeSelection> SelectedRanges =>
        [new RangeSelection(ProductFilterKeys.Price, _priceMin, _priceMax)];

    /// <summary>
    /// The sort orders offered in the grid's picker, in display order. Declared once in
    /// <see cref="ProductSortCatalogue"/>, which also owns the matching URL slugs and the default —
    /// so an added order reaches the picker, the link, and the fallback together.
    /// </summary>
    private static readonly IReadOnlyList<(ProductSortOption Value, string LabelKey)> SortOptions =
        ProductSortCatalogue.Instance.Picker;

    private int ResultRangeStart => ((_products.Page - 1) * _products.PageSize) + 1;

    private int ResultRangeEnd => ResultRangeStart + _products.Items.Count - 1;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Storefront().Current(Strings.Nav_Products));

        _products = new Paginator<ProductSummaryDto>(FetchProductsAsync);

        var filtersTask = BusyState.RunAsync(
            BusyKeys.Products.Filters, () => Mediator.Send(new GetCatalogueFiltersQuery()));
        var stateTask = base.OnInitializedAsync();

        await Task.WhenAll(filtersTask, stateTask);

        var filtersResult = await filtersTask;
        if (filtersResult.IsSuccess)
            _filters = filtersResult.Value;
        else
            Snackbar.Add(Localizer[filtersResult.Error!], Severity.Error);
    }

    /// <summary>
    /// Applies a catalogue state — snapshot the criteria into the fields the query and the filter
    /// components bind to, then load the requested page. Runs on first render and on every URL
    /// change (in-app, shared link, or Back/Forward).
    /// </summary>
    protected override Task ApplyStateAsync(CatalogueQueryState state, CancellationToken ct)
    {
        _selectedFilters = state.Filters;
        _priceMin = state.PriceMin;
        _priceMax = state.PriceMax;
        _sort = state.Sort;

        return BusyState.RunAsync(BusyKeys.Products.Catalogue, () => _products.GoToAsync(state.Page, ct));
    }

    private CatalogueQueryState BuildState() =>
        new(_selectedFilters, _priceMin, _priceMax, _sort, _products.Page);

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

    private Task OnFilterToggledAsync(FilterToggle toggle)
    {
        var next = BuildState().ToggleFilter(toggle.GroupKey, toggle.Value, toggle.IsSelected);
        _selectedFilters = next.Filters;
        return PushStateAsync(next);
    }

    /// <summary>
    /// Applies a settled range from the filter panel. Both bounds arrive together, so a drag that
    /// moved each thumb costs one state push — and one page query — instead of two.
    /// </summary>
    private Task OnRangeChangedAsync(RangeSelection range)
    {
        if (range.GroupKey != ProductFilterKeys.Price)
            return Task.CompletedTask;

        _priceMin = range.Min;
        _priceMax = range.Max;
        return PushStateAsync(BuildState() with { Page = 1 }, replace: true);
    }

    /// <summary>
    /// Formats a range-filter bound for the panel. Price is the catalogue's only range group and
    /// is money, so it renders in the shop's currency.
    /// </summary>
    private static string FormatRange(string groupKey, decimal value) =>
        groupKey == ProductFilterKeys.Price
            ? CurrencyFormatter.Format(value)
            : value.ToString(CultureInfo.InvariantCulture);

    private Task OnSortChangedAsync(ProductSortOption sort)
    {
        _sort = sort;
        return PushStateAsync(BuildState() with { Page = 1 });
    }

    private Task OnPageChangedAsync(int page) =>
        PushStateAsync(BuildState() with { Page = page });

    private Task OnClearFiltersAsync()
    {
        var next = CatalogueQueryState.Default;
        _selectedFilters = next.Filters;
        _priceMin = next.PriceMin;
        _priceMax = next.PriceMax;
        _sort = next.Sort;
        return PushStateAsync(next);
    }
}

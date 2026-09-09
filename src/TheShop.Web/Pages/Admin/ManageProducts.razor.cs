using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.Commands.DeleteProducts;
using TheShop.Application.Features.Products.Commands.SetProductStatus;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Queries.GetAdminProductFilters;
using TheShop.Application.Features.Products.Queries.GetAdminProductsPage;
using TheShop.Domain.Enums;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Common.Sorting;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// The manage-products admin list: a paged, searchable, filterable (status/brand/category/price),
/// sortable table with row selection and bulk Activate/Deactivate/Delete, plus links into the
/// id-addressed add/edit forms. Filter/search/sort/page state is deep-linked through the URL
/// query string (<see cref="ProductQueryState"/>). Requires a signed-in user via
/// <c>Pages/Admin/_Imports.razor</c>, and is gated on <c>products.view</c> within the page itself.
/// </summary>
[Route(Routes.Admin.ManageProducts)]
[AuthorizePermission("products.view")]
public partial class ManageProducts : QueryStatePageBase<ProductQueryState>
{
    private const int PageSize = 10;
    private const string StatusFilterKey = "status";
    private const string StatusActiveValue = "active";
    private const string StatusInactiveValue = "inactive";

    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    private static readonly FilterGroupDto StatusFilterGroup = new(
        StatusFilterKey,
        nameof(Strings.Filter_Status),
        FilterKind.SingleSelect,
        [
            new FilterOptionDto(StatusActiveValue, Strings.Filter_StatusActive, null),
            new FilterOptionDto(StatusInactiveValue, Strings.Filter_StatusInactive, null),
        ],
        null);

    /// <summary>
    /// The sort orders offered in the list's picker, in display order. Declared once in
    /// <see cref="AdminProductSortCatalogue"/>, which also owns the matching URL slugs and the
    /// default — so an added order reaches the picker, the link, and the fallback together.
    /// </summary>
    private static readonly IReadOnlyList<(AdminProductSortOption Value, string LabelKey)> SortOptions =
        AdminProductSortCatalogue.Instance.Picker;

    /// <summary>
    /// The busy-state keys for mutations that act on rows already on screen. These mutations keep
    /// the list visible, disable controls that could start an overlapping mutation, and report
    /// progress on the affected rows rather than through the table's progress bar.
    /// </summary>
    private static readonly IReadOnlyList<string> MutationBusyKeys =
    [
        BusyKeys.Products.ProductStatus,
        BusyKeys.Products.DeleteProducts,
    ];

    /// <summary>Which row-scoped mutation is currently in flight, if any.</summary>
    private enum RowMutation
    {
        None,
        Status,
        Delete,
    }

    private Paginator<ProductListItemDto> _products = default!;
    private AdminProductFiltersDto? _filters;

    private string? _search;
    private ProductStatusFilter? _status;
    private IReadOnlyList<AppliedFilterDto> _selectedFilters = [];
    private decimal? _priceMin;
    private decimal? _priceMax;
    private AdminProductSortOption _sort = AdminProductSortCatalogue.Instance.Default;

    /// <summary>
    /// The currently selected rows. Selection is page-owned UI state rather than part of the
    /// deep-linked query and is cleared whenever query state is applied.
    /// </summary>
    private HashSet<ProductListItemDto> _selectedItems = [];

    /// <summary>
    /// The rows whose deletion was blocked because the product is still referenced, keyed by id
    /// with the reference count the in-use caption reports. This is page-owned UI state and is
    /// cleared whenever query state is applied.
    /// </summary>
    private readonly Dictionary<Guid, int> _referencedIds = [];

    /// <summary>The identifiers of rows on which the current mutation is operating.</summary>
    private readonly HashSet<Guid> _pendingIds = [];

    /// <summary>
    /// The current row mutation, used with <see cref="_pendingIds"/> to display progress on the
    /// control that initiated the operation.
    /// </summary>
    private RowMutation _pendingMutation = RowMutation.None;

    private IReadOnlyList<FilterGroupDto> FilterGroups =>
    [
        StatusFilterGroup,
        new FilterGroupDto(ProductFilterKeys.Price, nameof(Strings.Filter_Price), FilterKind.Range, [], PriceRange),
        new FilterGroupDto(ProductFilterKeys.Brand, nameof(Strings.Filter_Brand), FilterKind.MultiSelect, _filters?.Brands ?? [], null),
        new FilterGroupDto(ProductFilterKeys.Category, nameof(Strings.Filter_Category), FilterKind.MultiSelect, _filters?.Categories ?? [], null),
    ];

    private RangeFilterDto? PriceRange => _filters?.PriceRange;

    private IReadOnlyList<AppliedFilterDto> SelectedFilters =>
        _status is { } status
            ? [new AppliedFilterDto(StatusFilterKey, [StatusToValue(status)]), .. _selectedFilters]
            : _selectedFilters;

    private IReadOnlyList<RangeSelection> SelectedRanges =>
        [new RangeSelection(ProductFilterKeys.Price, _priceMin, _priceMax)];

    private bool HasSelection => _selectedItems.Count > 0;

    private bool HasActiveCriteria =>
        _search is not null || _status is not null || _selectedFilters.Count > 0
        || _priceMin is not null || _priceMax is not null;

    /// <summary>
    /// <c>true</c> once the unfiltered, unsearched list has loaded with no rows at all — the
    /// catalogue itself is empty (Figma node 2629:4487), which hides the filter panel, table,
    /// pagination, and bulk bar entirely rather than showing them empty. A criteria-narrowed zero
    /// result instead renders the in-grid no-match message so the filter panel stays available.
    /// </summary>
    private bool ShowEmptyState => _products.HasLoaded && _products.Items.Count == 0 && !HasActiveCriteria;

    /// <summary>
    /// Gets the body spacing, adding bottom padding while the viewport-fixed bulk-action bar is
    /// visible so that it does not obscure the last table rows or pagination.
    /// </summary>
    private string BodyClass => HasSelection ? "px-8 pt-8 pb-14" : "pa-8";

    private bool IsRowPending(Guid id, RowMutation mutation) =>
        _pendingMutation == mutation && _pendingIds.Contains(id);

    /// <summary>
    /// Marks the specified rows as pending before the busy scope begins so their progress
    /// indicators are present when the busy-state render occurs.
    /// </summary>
    private void BeginRowMutation(IReadOnlyList<Guid> ids, RowMutation mutation)
    {
        _pendingIds.UnionWith(ids);
        _pendingMutation = mutation;
    }

    private void EndRowMutation()
    {
        _pendingIds.Clear();
        _pendingMutation = RowMutation.None;
    }

    /// <inheritdoc/>
    protected override void OnPageInitialized()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Current(Strings.ManageProducts_Heading));
        _products = new Paginator<ProductListItemDto>(FetchProductsAsync, PageSize);
    }

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        var filtersTask = BusyState.RunAsync(
            BusyKeys.Products.Filters, () => Mediator.Send(new GetAdminProductFiltersQuery()));
        var stateTask = base.OnInitializedAsync();

        await Task.WhenAll(filtersTask, stateTask);

        var filtersResult = await filtersTask;
        if (filtersResult.IsSuccess)
            _filters = filtersResult.Value;
        else
            Snackbar.Add(Localizer[filtersResult.Error!], Severity.Error);
    }

    /// <summary>
    /// Applies a manage-products state — snapshot the criteria, clear the transient selection
    /// (RULE-6), then load the requested page. Runs on first render and on every URL change.
    /// </summary>
    protected override Task ApplyStateAsync(ProductQueryState state, CancellationToken ct)
    {
        _selectedItems.Clear();
        _referencedIds.Clear();
        _search = state.Search;
        _status = state.Status;
        _selectedFilters = state.Filters;
        _priceMin = state.PriceMin;
        _priceMax = state.PriceMax;
        _sort = state.Sort;

        return BusyState.RunAsync(BusyKeys.Products.ManageList, () => _products.GoToAsync(state.Page, ct));
    }

    private ProductQueryState BuildState() =>
        new(_search, _status, _selectedFilters, _priceMin, _priceMax, _sort, _products.Page);

    private async Task<PagedResult<ProductListItemDto>> FetchProductsAsync(
        PaginationRequest request, CancellationToken ct)
    {
        var query = new GetAdminProductsPageQuery(
            _search, _status, ResolveIds(ProductFilterKeys.Brand), ResolveIds(ProductFilterKeys.Category),
            _priceMin, _priceMax, _sort, request);

        var result = await Mediator.Send(query, ct);

        if (result.IsSuccess)
            return result.Value;

        Snackbar.Add(Localizer[result.Error!], Severity.Error);
        return PagedResult<ProductListItemDto>.Empty(request);
    }

    private IReadOnlyList<Guid>? ResolveIds(string groupKey)
    {
        var values = _selectedFilters.FirstOrDefault(f => f.Key == groupKey)?.Values;
        if (values is not { Count: > 0 })
            return null;

        return [.. values.Where(v => Guid.TryParse(v, out _)).Select(Guid.Parse)];
    }

    private Task OnSearchChangedAsync(string? value)
    {
        _search = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return PushStateAsync(BuildState() with { Page = 1 }, replace: true);
    }

    private Task OnSingleSelectChangedAsync((string GroupKey, string? Value) change)
    {
        if (change.GroupKey != StatusFilterKey)
            return Task.CompletedTask;

        _status = ParseStatusValue(change.Value);
        return PushStateAsync(BuildState() with { Page = 1 });
    }

    private Task OnFilterToggledAsync(FilterToggle toggle)
    {
        var next = BuildState().ToggleFilter(toggle.GroupKey, toggle.Value, toggle.IsSelected);
        _selectedFilters = next.Filters;
        return PushStateAsync(next);
    }

    private Task OnRangeChangedAsync(RangeSelection range)
    {
        if (range.GroupKey != ProductFilterKeys.Price)
            return Task.CompletedTask;

        _priceMin = range.Min;
        _priceMax = range.Max;
        return PushStateAsync(BuildState() with { Page = 1 }, replace: true);
    }

    private Task OnSortChangedAsync(AdminProductSortOption sort)
    {
        _sort = sort;
        return PushStateAsync(BuildState() with { Page = 1 });
    }

    private Task OnPageChangedAsync(int page) =>
        PushStateAsync(BuildState() with { Page = page });

    private Task OnClearFiltersAsync()
    {
        _search = null;
        _status = null;
        _selectedFilters = [];
        _priceMin = null;
        _priceMax = null;
        return PushStateAsync(BuildState() with { Page = 1 });
    }

    private void OnSelectedItemsChanged(HashSet<ProductListItemDto> items) => _selectedItems = items;

    /// <summary>
    /// Dismisses the bulk-action bar. The bar's visibility is derived from the selection, so
    /// clearing the selection is what takes it down — along with the in-use indicators, which
    /// only ever annotate rows the user just tried to delete.
    /// </summary>
    private void OnCloseBulkActions()
    {
        _selectedItems = [];
        _referencedIds.Clear();
    }

    private async Task OnStatusToggledAsync(ProductListItemDto product, bool activate)
    {
        if (activate)
        {
            await ApplyStatusChangeAsync([product.Id], true);
            return;
        }

        var confirmed = await ConfirmAsync(
            Strings.ManageProducts_DeactivateConfirmTitle,
            string.Format(Strings.ManageProducts_DeactivateConfirmBody, product.Name),
            Strings.ManageProducts_BulkDeactivate,
            Color.Primary);

        if (confirmed)
            await ApplyStatusChangeAsync([product.Id], false);
    }

    private async Task BulkActivateAsync() =>
        await ApplyStatusChangeAsync([.. _selectedItems.Select(p => p.Id)], true);

    private async Task BulkDeactivateAsync()
    {
        var ids = _selectedItems.Select(p => p.Id).ToList();
        if (ids.Count == 0)
            return;

        var confirmed = await ConfirmAsync(
            string.Format(Strings.ManageProducts_BulkDeactivateConfirmTitle, ids.Count),
            string.Format(Strings.ManageProducts_BulkDeactivateConfirmBody, ids.Count),
            Strings.ManageProducts_BulkDeactivate,
            Color.Primary);

        if (confirmed)
            await ApplyStatusChangeAsync(ids, false);
    }

    private async Task ApplyStatusChangeAsync(IReadOnlyList<Guid> ids, bool isActive)
    {
        if (ids.Count == 0)
            return;

        BeginRowMutation(ids, RowMutation.Status);

        await BusyState.RunAsync(BusyKeys.Products.ProductStatus, async () =>
        {
            try
            {
                var result = await Mediator.Send(new SetProductStatusCommand(ids, isActive));
                if (!result.IsSuccess)
                {
                    Snackbar.Add(Localizer[result.Error!], Severity.Error);
                    return;
                }

                var messageKey = isActive ? Strings.ManageProducts_ActivatedSuccess : Strings.ManageProducts_DeactivatedSuccess;
                Snackbar.Add(string.Format(messageKey, result.Value.ChangedCount), Severity.Success);

                if (result.Value.NotPublishable.Count > 0)
                {
                    Snackbar.Add(
                        string.Format(Strings.ManageProducts_ActivateSkipped, result.Value.NotPublishable.Count),
                        Severity.Warning);
                }

                _selectedItems.Clear();
                _referencedIds.Clear();
                await _products.LoadAsync();
            }
            finally
            {
                EndRowMutation();
            }
        });
    }

    private async Task DeleteSingleAsync(ProductListItemDto product)
    {
        var confirmed = await ConfirmAsync(
            Strings.ManageProducts_DeleteConfirmTitle,
            string.Format(Strings.ManageProducts_DeleteConfirmBody, product.Name),
            Strings.ManageProducts_BulkDelete,
            Color.Error);

        if (confirmed)
            await ApplyDeleteAsync([product.Id]);
    }

    private async Task BulkDeleteAsync()
    {
        var ids = _selectedItems.Select(p => p.Id).ToList();
        if (ids.Count == 0)
            return;

        var confirmed = await ConfirmAsync(
            string.Format(Strings.ManageProducts_BulkDeleteConfirmTitle, ids.Count),
            string.Format(Strings.ManageProducts_BulkDeleteConfirmBody, ids.Count),
            Strings.ManageProducts_BulkDelete,
            Color.Error);

        if (confirmed)
            await ApplyDeleteAsync(ids);
    }

    private async Task ApplyDeleteAsync(IReadOnlyList<Guid> ids)
    {
        BeginRowMutation(ids, RowMutation.Delete);

        await BusyState.RunAsync(BusyKeys.Products.DeleteProducts, async () =>
        {
            try
            {
                var result = await Mediator.Send(new DeleteProductsCommand(ids));
                if (!result.IsSuccess)
                {
                    Snackbar.Add(Localizer[result.Error!], Severity.Error);
                    return;
                }

                var outcome = result.Value;
                _referencedIds.Clear();

                if (ids.Count == 1)
                    HandleSingleDeleteOutcome(outcome);
                else
                    HandleBulkDeleteOutcome(ids.Count, outcome);

                await _products.LoadAsync();

                // AC-22: a deletion that empties the current page recovers to the new last page
                // (or the empty state when nothing remains) rather than showing a stranded page.
                if (_products.Page > _products.TotalPages)
                {
                    await _products.LastAsync();
                    await PushStateAsync(BuildState() with { Page = _products.Page }, replace: true);
                }
            }
            finally
            {
                EndRowMutation();
            }
        });
    }

    /// <summary>
    /// Handles a single-product deletion result, reporting success or identifying the product
    /// whose deletion was blocked because it is still referenced.
    /// </summary>
    private void HandleSingleDeleteOutcome(ProductDeletionOutcomeDto outcome)
    {
        if (outcome.DeletedCount == 1)
        {
            Snackbar.Add(Strings.ManageProducts_DeletedSuccess, Severity.Success);
            _selectedItems.Clear();
            return;
        }

        var blocked = outcome.Blocked[0];
        _referencedIds[blocked.Id] = blocked.ReferenceCount;
        Snackbar.Add(string.Format(Strings.Product_InUse, blocked.Name, blocked.ReferenceCount), Severity.Warning);
    }

    /// <summary>
    /// Handles a bulk-deletion result, reporting aggregate counts and keeping referenced products
    /// selected so the table can identify them with in-use indicators.
    /// </summary>
    private void HandleBulkDeleteOutcome(int requestedCount, ProductDeletionOutcomeDto outcome)
    {
        if (outcome.Blocked.Count == 0)
        {
            Snackbar.Add(string.Format(Strings.ManageProducts_BulkDeletedSuccess, outcome.DeletedCount), Severity.Success);
            _selectedItems.Clear();
            return;
        }

        if (outcome.DeletedCount == 0 && outcome.Blocked.Count == requestedCount)
        {
            Snackbar.Add(Strings.Product_BulkDeleteAllBlocked, Severity.Warning);
        }
        else
        {
            Snackbar.Add(
                string.Format(Strings.Product_BulkDeletePartial, outcome.DeletedCount, outcome.Blocked.Count),
                Severity.Warning);
        }

        // The referenced products are still present in _products.Items (only the reload below
        // would drop the deleted ones), so they can be looked up by id to rebuild the selection.
        foreach (var blocked in outcome.Blocked)
            _referencedIds[blocked.Id] = blocked.ReferenceCount;

        _selectedItems = _products.Items.Where(p => _referencedIds.ContainsKey(p.Id)).ToHashSet();
    }

    private async Task<bool> ConfirmAsync(string title, string body, string confirmLabel, Color confirmColor)
    {
        var parameters = new DialogParameters<ShopConfirmDialog>
        {
            { x => x.TitleText, title },
            { x => x.BodyText, body },
            { x => x.ConfirmLabel, confirmLabel },
            { x => x.ConfirmColor, confirmColor },
        };

        var dialog = await DialogService.ShowAsync<ShopConfirmDialog>(title, parameters);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    /// <summary>
    /// Renders a row's price range: a single amount when the lowest and highest price coincide
    /// (a zero-variant product, or every variant priced the same), the localized range format
    /// otherwise, and a placeholder when the row carries no price at all (plan §5 Decision 13).
    /// </summary>
    private static string FormatPrice(ProductListItemDto product)
    {
        if (product.MinPrice is not { } min || product.MaxPrice is not { } max)
            return Strings.NotAvailable;

        return min == max
            ? CurrencyFormatter.Format(min, product.Currency)
            : string.Format(
                Strings.ManageProducts_PriceRange,
                CurrencyFormatter.Format(min, product.Currency),
                CurrencyFormatter.Format(max, product.Currency));
    }

    private static string FormatVariantCount(int count) =>
        count == 1 ? Strings.ManageProducts_VariantCountOne : string.Format(Strings.ManageProducts_VariantCountMany, count);

    /// <summary>
    /// Formats a range-filter bound for the panel. Price is the list's only range group and is
    /// money, so it renders in the shop's currency.
    /// </summary>
    private static string FormatRange(string groupKey, decimal value) =>
        groupKey == ProductFilterKeys.Price
            ? CurrencyFormatter.Format(value)
            : value.ToString(CultureInfo.InvariantCulture);

    private static string StatusToValue(ProductStatusFilter status) => status switch
    {
        ProductStatusFilter.Active => StatusActiveValue,
        _ => StatusInactiveValue,
    };

    /// <summary>
    /// Maps a filter-panel option value back to a status filter. An unrecognised or absent value —
    /// including the null the panel emits when the user unchecks the selected option — clears the
    /// filter.
    /// </summary>
    private static ProductStatusFilter? ParseStatusValue(string? value) => value switch
    {
        StatusActiveValue => ProductStatusFilter.Active,
        StatusInactiveValue => ProductStatusFilter.Inactive,
        _ => null,
    };
}

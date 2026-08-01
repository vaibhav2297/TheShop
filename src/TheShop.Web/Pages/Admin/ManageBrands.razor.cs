using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.Commands.DeleteBrands;
using TheShop.Application.Features.Brands.Commands.SetBrandStatus;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Queries.GetBrandsPage;
using TheShop.Domain.Enums;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Common.Sorting;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// The manage-brands admin list: a paged, searchable, filterable, sortable table with row
/// selection and bulk Activate/Deactivate/Delete, plus links into the id-addressed edit form.
/// Filter/search/sort/page state is deep-linked through the URL query string
/// (<see cref="BrandQueryState"/>). Requires a signed-in user via
/// <c>Pages/Admin/_Imports.razor</c>, and is gated on <c>brands.view</c> within the page
/// itself.
/// </summary>
[Route(Routes.Admin.ManageBrands)]
[AuthorizePermission("brands.view")]
public partial class ManageBrands : QueryStatePageBase<BrandQueryState>
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

    private static readonly IReadOnlyList<FilterGroupDto> FilterGroups = [StatusFilterGroup];

    /// <summary>
    /// The sort orders offered in the list's picker, in display order. Declared once in
    /// <see cref="BrandSortCatalogue"/>, which also owns the matching URL slugs and the default —
    /// so an added order reaches the picker, the link, and the fallback together.
    /// </summary>
    private static readonly IReadOnlyList<(BrandSortOption Value, string LabelKey)> SortOptions =
        BrandSortCatalogue.Instance.Picker;

    /// <summary>
    /// The busy-state keys for mutations that act on rows already on screen. These mutations keep
    /// the list visible, disable controls that could start an overlapping mutation, and report
    /// progress on the affected rows rather than through the table's progress bar.
    /// </summary>
    private static readonly IReadOnlyList<string> MutationBusyKeys =
    [
        BusyKeys.Brands.BrandStatus,
        BusyKeys.Brands.DeleteBrands,
    ];

    /// <summary>Which row-scoped mutation is currently in flight, if any.</summary>
    private enum RowMutation
    {
        None,
        Status,
        Delete,
    }

    private Paginator<BrandListItemDto> _brands = default!;
    private string? _search;
    private BrandStatusFilter? _status;
    private BrandSortOption _sort = BrandSortCatalogue.Instance.Default;

    /// <summary>
    /// The currently selected rows. Selection is page-owned UI state rather than part of the
    /// deep-linked query and is cleared whenever query state is applied.
    /// </summary>
    private HashSet<BrandListItemDto> _selectedItems = [];

    /// <summary>
    /// The identifiers of rows whose deletion was blocked because the brands are in use. This is
    /// page-owned UI state and is cleared whenever query state is applied.
    /// </summary>
    private readonly HashSet<Guid> _blockedIds = [];

    /// <summary>The identifiers of rows on which the current mutation is operating.</summary>
    private readonly HashSet<Guid> _pendingIds = [];

    /// <summary>
    /// The current row mutation, used with <see cref="_pendingIds"/> to display progress on the
    /// control that initiated the operation.
    /// </summary>
    private RowMutation _pendingMutation = RowMutation.None;

    private IReadOnlyList<AppliedFilterDto> SelectedFilters =>
        _status is { } status ? [new AppliedFilterDto(StatusFilterKey, [StatusToValue(status)])] : [];

    private bool HasSelection => _selectedItems.Count > 0;

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
        Breadcrumbs.Set(BreadcrumbTrail.Admin().Current(Strings.ManageBrands_Heading));
        _brands = new Paginator<BrandListItemDto>(FetchBrandsAsync, PageSize);
    }

    /// <summary>
    /// Applies a manage-brands state — snapshot the criteria, clear the transient selection
    /// (RULE-12), then load the requested page. Runs on first render and on every URL change.
    /// </summary>
    protected override Task ApplyStateAsync(BrandQueryState state, CancellationToken ct)
    {
        _selectedItems.Clear();
        _blockedIds.Clear();
        _search = state.Search;
        _status = state.Status;
        _sort = state.Sort;

        return BusyState.RunAsync(BusyKeys.Brands.ManageList, () => _brands.GoToAsync(state.Page, ct));
    }

    private BrandQueryState BuildState() => new(_search, _status, _sort, _brands.Page);

    private async Task<PagedResult<BrandListItemDto>> FetchBrandsAsync(
        PaginationRequest request, CancellationToken ct)
    {
        var query = new GetBrandsPageQuery(_search, _status, _sort, request);
        var result = await Mediator.Send(query, ct);

        if (result.IsSuccess)
            return result.Value;

        Snackbar.Add(Localizer[result.Error!], Severity.Error);
        return PagedResult<BrandListItemDto>.Empty(request);
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

    private Task OnSortChangedAsync(BrandSortOption sort)
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
        return PushStateAsync(BuildState() with { Page = 1 });
    }

    private void OnSelectedItemsChanged(HashSet<BrandListItemDto> items) => _selectedItems = items;

    /// <summary>
    /// Dismisses the bulk-action bar. The bar's visibility is derived from the selection, so
    /// clearing the selection is what takes it down — along with the in-use indicators, which only
    /// ever annotate rows the user just tried to delete.
    /// </summary>
    private void OnCloseBulkActions()
    {
        _selectedItems = [];
        _blockedIds.Clear();
    }

    private async Task OnStatusToggledAsync(BrandListItemDto brand, bool activate)
    {
        if (activate)
        {
            await ApplyStatusChangeAsync([brand.Id], true);
            return;
        }

        var confirmed = await ConfirmAsync(
            Strings.ManageBrands_DeactivateConfirmTitle,
            string.Format(Strings.ManageBrands_DeactivateConfirmBody, brand.Name),
            Strings.ManageBrands_BulkDeactivate,
            Color.Primary);

        if (confirmed)
            await ApplyStatusChangeAsync([brand.Id], false);
    }

    private async Task BulkActivateAsync() =>
        await ApplyStatusChangeAsync([.. _selectedItems.Select(b => b.Id)], true);

    private async Task BulkDeactivateAsync()
    {
        var ids = _selectedItems.Select(b => b.Id).ToList();
        if (ids.Count == 0)
            return;

        var confirmed = await ConfirmAsync(
            string.Format(Strings.ManageBrands_BulkDeactivateConfirmTitle, ids.Count),
            string.Format(Strings.ManageBrands_BulkDeactivateConfirmBody, ids.Count),
            Strings.ManageBrands_BulkDeactivate,
            Color.Primary);

        if (confirmed)
            await ApplyStatusChangeAsync(ids, false);
    }

    private async Task ApplyStatusChangeAsync(IReadOnlyList<Guid> ids, bool isActive)
    {
        if (ids.Count == 0)
            return;

        BeginRowMutation(ids, RowMutation.Status);

        await BusyState.RunAsync(BusyKeys.Brands.BrandStatus, async () =>
        {
            try
            {
                var result = await Mediator.Send(new SetBrandStatusCommand(ids, isActive));
                if (!result.IsSuccess)
                {
                    Snackbar.Add(Localizer[result.Error!], Severity.Error);
                    return;
                }

                var messageKey = isActive ? Strings.ManageBrands_ActivatedSuccess : Strings.ManageBrands_DeactivatedSuccess;
                Snackbar.Add(string.Format(messageKey, result.Value.ChangedCount), Severity.Success);

                _selectedItems.Clear();
                _blockedIds.Clear();
                await _brands.LoadAsync();
            }
            finally
            {
                EndRowMutation();
            }
        });
    }

    private async Task DeleteSingleAsync(BrandListItemDto brand)
    {
        var confirmed = await ConfirmAsync(
            Strings.ManageBrands_DeleteConfirmTitle,
            string.Format(Strings.ManageBrands_DeleteConfirmBody, brand.Name),
            Strings.ManageBrands_BulkDelete,
            Color.Error);

        if (confirmed)
            await ApplyDeleteAsync([brand.Id]);
    }

    private async Task BulkDeleteAsync()
    {
        var ids = _selectedItems.Select(b => b.Id).ToList();
        if (ids.Count == 0)
            return;

        var confirmed = await ConfirmAsync(
            string.Format(Strings.ManageBrands_BulkDeleteConfirmTitle, ids.Count),
            string.Format(Strings.ManageBrands_BulkDeleteConfirmBody, ids.Count),
            Strings.ManageBrands_BulkDelete,
            Color.Error);

        if (confirmed)
            await ApplyDeleteAsync(ids);
    }

    private async Task ApplyDeleteAsync(IReadOnlyList<Guid> ids)
    {
        BeginRowMutation(ids, RowMutation.Delete);

        await BusyState.RunAsync(BusyKeys.Brands.DeleteBrands, async () =>
        {
            try
            {
                var result = await Mediator.Send(new DeleteBrandsCommand(ids));
                if (!result.IsSuccess)
                {
                    Snackbar.Add(Localizer[result.Error!], Severity.Error);
                    return;
                }

                var outcome = result.Value;
                _blockedIds.Clear();

                if (ids.Count == 1)
                    HandleSingleDeleteOutcome(outcome);
                else
                    HandleBulkDeleteOutcome(ids.Count, outcome);

                await _brands.LoadAsync();
            }
            finally
            {
                EndRowMutation();
            }
        });
    }

    /// <summary>
    /// Handles a single-brand deletion result, reporting success or identifying the brand whose
    /// deletion was blocked because it is in use.
    /// </summary>
    private void HandleSingleDeleteOutcome(BrandDeletionOutcomeDto outcome)
    {
        if (outcome.DeletedCount == 1)
        {
            Snackbar.Add(Strings.ManageBrands_DeletedSuccess, Severity.Success);
            _selectedItems.Clear();
            return;
        }

        var blocked = outcome.Blocked[0];
        _blockedIds.Add(blocked.Id);
        Snackbar.Add(string.Format(Strings.Brand_InUse, blocked.Name, blocked.ProductCount), Severity.Warning);
    }

    /// <summary>
    /// Handles a bulk-deletion result, reporting aggregate counts and keeping blocked brands
    /// selected so the table can identify them with in-use indicators.
    /// </summary>
    private void HandleBulkDeleteOutcome(int requestedCount, BrandDeletionOutcomeDto outcome)
    {
        if (outcome.Blocked.Count == 0)
        {
            Snackbar.Add(string.Format(Strings.ManageBrands_BulkDeletedSuccess, outcome.DeletedCount), Severity.Success);
            _selectedItems.Clear();
            return;
        }

        if (outcome.DeletedCount == 0 && outcome.Blocked.Count == requestedCount)
        {
            Snackbar.Add(Strings.Brand_BulkDeleteAllBlocked, Severity.Warning);
        }
        else
        {
            Snackbar.Add(
                string.Format(Strings.Brand_BulkDeletePartial, outcome.DeletedCount, outcome.Blocked.Count),
                Severity.Warning);
        }

        // The blocked brands are still present in _brands.Items (only the reload below would drop
        // the deleted ones), so they can be looked up by id to rebuild the table's selection.
        var blockedIds = outcome.Blocked.Select(b => b.Id).ToHashSet();
        foreach (var id in blockedIds)
            _blockedIds.Add(id);

        _selectedItems = _brands.Items.Where(b => blockedIds.Contains(b.Id)).ToHashSet();
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

    private static string StatusToValue(BrandStatusFilter status) => status switch
    {
        BrandStatusFilter.Active => StatusActiveValue,
        _ => StatusInactiveValue,
    };

    /// <summary>
    /// Maps a filter-panel option value back to a status filter. An unrecognised or absent value —
    /// including the null the panel emits when the user unchecks the selected option — clears the
    /// filter.
    /// </summary>
    private static BrandStatusFilter? ParseStatusValue(string? value) => value switch
    {
        StatusActiveValue => BrandStatusFilter.Active,
        StatusInactiveValue => BrandStatusFilter.Inactive,
        _ => null,
    };
}

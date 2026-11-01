using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Queries.GetFreshDropsProducts;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Admin;

/// <summary>Selected IDs and cards returned by the Fresh Drops picker.</summary>
public sealed record FreshDropsProductDialogResult(IReadOnlyList<Guid> ProductIds,
    IReadOnlyList<FreshDropsProductDto> Cards);

/// <summary>Dialog-local product search and unsaved selection.</summary>
public partial class FreshDropsProductDialog : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;

    [Parameter, EditorRequired] public IReadOnlyList<Guid> InitialSelectedIds { get; set; } = [];
    [Parameter, EditorRequired] public EventCallback<FreshDropsProductDialogResult?> OnCompleted { get; set; }

    private readonly CancellationTokenSource _lifetime = new();
    private List<Guid> _selectedIds = [];
    private List<FreshDropsProductDto> _results = [];
    private string _search = string.Empty;
    private string? _searchError;
    private bool _hasMoreResults;
    private bool _completed;

    protected override async Task OnInitializedAsync()
    {
        _selectedIds = [.. InitialSelectedIds];
        await SearchAsync();
    }

    private async Task SearchChangedAsync(string? value)
    {
        _search = value ?? string.Empty;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Homepage.SearchFreshDrops)) return;
        var term = _search;
        await BusyState.RunAsync(BusyKeys.Homepage.SearchFreshDrops, async () =>
        {
            var result = await Mediator.Send(new GetFreshDropsProductsQuery(Search: term, Limit: 24), _lifetime.Token);
            if (term != _search || _completed) return;
            if (result.IsFailure) { _searchError = result.Error; return; }
            _searchError = null;
            _results = [.. result.Value ?? []];
            _hasMoreResults = _results.Count == 24;
        });
        if (term != _search && !_completed) await SearchAsync();
    }

    private async Task LoadMoreAsync()
    {
        if (!_hasMoreResults || BusyState.IsBusy(BusyKeys.Homepage.SearchFreshDrops)) return;
        var term = _search;
        var offset = _results.Count;
        await BusyState.RunAsync(BusyKeys.Homepage.SearchFreshDrops, async () =>
        {
            var result = await Mediator.Send(new GetFreshDropsProductsQuery(Search: term, Limit: 24, Offset: offset), _lifetime.Token);
            if (term != _search || _completed) return;
            if (result.IsFailure) { _searchError = result.Error; return; }
            _searchError = null;
            var next = result.Value ?? [];
            _results.AddRange(next);
            _hasMoreResults = next.Count == 24;
        });
        if (term != _search && !_completed) await SearchAsync();
    }

    private void ToggleProduct(Guid id)
    {
        if (_selectedIds.Remove(id)) return;
        if (_selectedIds.Count < 3) _selectedIds.Add(id);
    }

    private Task ConfirmAsync() => CompleteAsync(new FreshDropsProductDialogResult(
        [.. _selectedIds], [.. _results.Where(card => _selectedIds.Contains(card.Id))]));

    private Task CancelAsync() => CompleteAsync(null);

    private Task CompleteAsync(FreshDropsProductDialogResult? result)
    {
        if (_completed) return Task.CompletedTask;
        _completed = true;
        return OnCompleted.InvokeAsync(result);
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}

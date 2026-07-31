using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace TheShop.Web.Common;

/// <summary>
/// Base component for pages whose filter / sort / pagination (or any) state is deep-linked through
/// the URL query string. The URL is the single source of truth: change handlers push a new
/// <typeparamref name="TState"/> via <see cref="PushStateAsync"/>, and every application of that
/// state — initial deep-link load, in-app change, and browser Back/Forward — flows through the one
/// <see cref="ApplyStateAsync"/> path. Derive a page from this, implement
/// <see cref="ApplyStateAsync"/>, and define a <typeparamref name="TState"/> record; the page owns
/// how it builds new states to push.
/// </summary>
/// <remarks>
/// Because the page owns the entire query string, <see cref="PushStateAsync"/> rebuilds the query
/// from scratch on each write — stale parameters (including cleared filters) are dropped rather
/// than lingering. A derived page does its synchronous one-time setup in
/// <see cref="OnPageInitialized"/> (<see cref="OnInitialized"/> is sealed, because silently losing
/// its <c>LocationChanged</c> subscription would strand the page on its first state), and any async
/// one-time work (e.g. loading a filter sidebar) in an <see cref="OnInitializedAsync"/> override
/// that calls <c>base.OnInitializedAsync()</c>, which performs the first state application.
/// </remarks>
/// <typeparam name="TState">The immutable state carried in the query string.</typeparam>
public abstract class QueryStatePageBase<TState> : ComponentBase, IDisposable
    where TState : IUrlQueryState<TState>
{
    [Inject] private NavigationManager Nav { get; set; } = default!;

    // The query portion of the last URL we applied. LocationChanged fires for our own pushes as
    // well as browser Back/Forward; comparing the raw query string (both sides are produced by the
    // same canonical builder) tells the two apart from a no-op change and keeps each change to one
    // ApplyStateAsync. Reference/list equality on the record can't be relied on here.
    private string? _appliedQuery;

    // Cancels the previous state application when a newer one starts. Rapid changes (fast filter
    // clicks) fire overlapping ApplyStateAsync calls whose fetches race; without this the slower
    // response could land last and win, showing results for a superseded selection. Each new
    // application cancels the prior one so only the latest state's fetch is adopted.
    private CancellationTokenSource? _cts;

    /// <inheritdoc/>
    /// <remarks>
    /// Sealed on purpose. This is where the page subscribes to <c>LocationChanged</c> — the only
    /// thing that turns a pushed URL back into an <see cref="ApplyStateAsync"/> call. An override
    /// that forgot to call <c>base.OnInitialized()</c> would drop that subscription and leave the
    /// page frozen on whatever state it loaded with, with no error to show why. Derived pages put
    /// their synchronous setup in <see cref="OnPageInitialized"/> instead.
    /// </remarks>
    protected sealed override void OnInitialized()
    {
        Nav.LocationChanged += OnLocationChanged;
        OnPageInitialized();
    }

    /// <summary>
    /// Synchronous one-time page setup — breadcrumbs, paginator construction, and anything else
    /// that must exist before the first state is applied. Runs immediately after the base wires up
    /// its URL subscription, in place of an <see cref="OnInitialized"/> override.
    /// </summary>
    protected virtual void OnPageInitialized()
    {
    }

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        _appliedQuery = CurrentQuery();
        await ApplyCoreAsync(ReadState());
    }

    /// <summary>
    /// Writes <paramref name="next"/> to the URL, replacing the whole query string, which raises
    /// <c>LocationChanged</c> and drives a single <see cref="ApplyStateAsync"/>.
    /// </summary>
    /// <param name="next">The state to encode into the URL.</param>
    /// <param name="replace">
    /// <c>true</c> to replace the current history entry (e.g. rapid, debounced changes);
    /// <c>false</c> (default) to push a new entry so Back reverses the change.
    /// </param>
    protected Task PushStateAsync(TState next, bool replace = false)
    {
        var path = Nav.ToAbsoluteUri(Nav.Uri).GetLeftPart(UriPartial.Path);
        var url = Nav.GetUriWithQueryParameters(path, next.ToQueryParameters());
        Nav.NavigateTo(url, forceLoad: false, replace: replace);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies a state to the page — set fields, seed pagination, and load data. Invoked once on
    /// first render and again for every subsequent URL change (in-app or Back/Forward).
    /// </summary>
    /// <param name="state">The state to apply.</param>
    /// <param name="ct">
    /// Cancelled when a newer state application supersedes this one. Flow it through every async
    /// load so a superseded fetch is abandoned rather than adopted after a faster later change.
    /// </param>
    protected abstract Task ApplyStateAsync(TState state, CancellationToken ct);

    // Wraps each ApplyStateAsync in a fresh cancellation scope, cancelling the previous one so the
    // latest state always wins even when an earlier fetch resolves later.
    private async Task ApplyCoreAsync(TState state)
    {
        var cts = new CancellationTokenSource();
        var prior = Interlocked.Exchange(ref _cts, cts);
        prior?.Cancel();
        prior?.Dispose();

        try
        {
            await ApplyStateAsync(state, cts.Token);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            // Superseded by a newer state application — drop this stale result.
        }
    }

    private TState ReadState() =>
        TState.FromQuery(QueryHelpers.ParseQuery(new Uri(Nav.Uri).Query));

    private string CurrentQuery() => new Uri(Nav.Uri).Query;

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        var query = CurrentQuery();
        if (query == _appliedQuery)
            return;

        _appliedQuery = query;
        await InvokeAsync(async () =>
        {
            await ApplyCoreAsync(ReadState());
            StateHasChanged();
        });
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        Nav.LocationChanged -= OnLocationChanged;
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

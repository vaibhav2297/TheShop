using TheShop.Application.Common.Models;

namespace TheShop.Web.Common;

/// <summary>
/// A page-scoped, stateful pagination controller for a paged list of <typeparamref name="T"/>.
/// Owns the current page number and the loaded page's data, and exposes flexible navigation
/// (<see cref="NextAsync"/>, <see cref="PreviousAsync"/>, <see cref="GoToAsync"/>, …) so a
/// component can bind to it directly instead of hand-tracking page state.
/// </summary>
/// <remarks>
/// Data is loaded through a caller-supplied fetch delegate, so this type stays free of
/// MediatR, <see cref="BusyState"/>, and error handling — the owning component supplies those
/// in the delegate (e.g. dispatch a query inside <c>BusyState.RunAsync(...)</c>). Not a DI
/// service: construct one per component that needs paging.
/// </remarks>
/// <typeparam name="T">The item type of a single page.</typeparam>
/// <remarks>
/// Creates a paginator that loads pages via <paramref name="fetch"/>.
/// </remarks>
/// <param name="fetch">
/// Loads the page described by a <see cref="PaginationRequest"/>. Should never throw for
/// expected failures — return an empty page (e.g. <see cref="PagedResult{T}.Empty"/>) instead.
/// </param>
/// <param name="pageSize">The initial page size; clamped to at least 1.</param>
public sealed class Paginator<T>(Func<PaginationRequest, CancellationToken, Task<PagedResult<T>>> fetch, int pageSize = 12)
{
    private readonly Func<PaginationRequest, CancellationToken, Task<PagedResult<T>>> _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));

    private PagedResult<T> _current = PagedResult<T>.Empty(new PaginationRequest(PageSize: Math.Max(pageSize, 1)));

    /// <summary>
    /// <c>true</c> once a page has been loaded at least once.
    /// </summary>
    public bool HasLoaded { get; private set; }

    /// <summary>
    /// The current 1-based page number.
    /// </summary>
    public int Page => _current.Page;

    /// <summary>
    /// The number of items requested per page.
    /// </summary>
    public int PageSize => _current.PageSize;

    /// <summary>
    /// The total number of matching items across all pages.
    /// </summary>
    public int TotalCount => _current.TotalCount;

    /// <summary>
    /// The items on the current page.
    /// </summary>
    public IReadOnlyList<T> Items => _current.Items;

    /// <inheritdoc cref="PagedResult{T}.TotalPages"/>
    public int TotalPages => _current.TotalPages;

    /// <inheritdoc cref="PagedResult{T}.HasPrevious"/>
    public bool HasPrevious => _current.HasPrevious;

    /// <inheritdoc cref="PagedResult{T}.HasNext"/>
    public bool HasNext => _current.HasNext;

    /// <summary>
    /// Loads (or reloads) the current page — initial load and refresh alike.
    /// </summary>
    public Task LoadAsync(CancellationToken ct = default) => GoToAsync(Page, ct);

    /// <summary>
    /// Navigates back to the first page. Use when filters or sort change.
    /// </summary>
    public Task ResetAsync(CancellationToken ct = default) => GoToAsync(1, ct);

    /// <summary>
    /// Navigates to the last page (or the first page when nothing has loaded yet).
    /// </summary>
    public Task LastAsync(CancellationToken ct = default) => GoToAsync(Math.Max(TotalPages, 1), ct);

    /// <summary>
    /// Navigates to the next page when one exists; otherwise does nothing.
    /// </summary>
    public Task NextAsync(CancellationToken ct = default) =>
        HasNext ? GoToAsync(Page + 1, ct) : Task.CompletedTask;

    /// <summary>
    /// Navigates to the previous page when one exists; otherwise does nothing.
    /// </summary>
    public Task PreviousAsync(CancellationToken ct = default) =>
        HasPrevious ? GoToAsync(Page - 1, ct) : Task.CompletedTask;

    /// <summary>
    /// Loads <paramref name="page"/> (clamped to at least 1) and adopts the returned
    /// <see cref="PagedResult{T}"/> as the current state.
    /// </summary>
    public async Task GoToAsync(int page, CancellationToken ct = default)
    {
        var result = await _fetch(new PaginationRequest(Math.Max(page, 1), PageSize), ct);

        // A fetch superseded mid-flight must not adopt its (now stale) result — guard here in case
        // the fetch delegate itself doesn't honour cancellation.
        ct.ThrowIfCancellationRequested();

        _current = result;
        HasLoaded = true;
    }

    /// <summary>
    /// Changes the page size (clamped to at least 1) and reloads from the first page.
    /// </summary>
    public Task SetPageSizeAsync(int pageSize, CancellationToken ct = default)
    {
        _current = _current with { PageSize = Math.Max(pageSize, 1) };
        return ResetAsync(ct);
    }
}

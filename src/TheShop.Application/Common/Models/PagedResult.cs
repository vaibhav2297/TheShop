namespace TheShop.Application.Common.Models;

/// <summary>
/// A single page of <typeparamref name="T"/> items plus enough metadata to render
/// pagination controls. Reusable across features — not specific to the product catalogue.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>
    /// The total number of pages, given <see cref="TotalCount"/> and <see cref="PageSize"/>.
    /// </summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>
    /// <c>true</c> when a page before the current one exists.
    /// </summary>
    public bool HasPrevious => Page > 1;

    /// <summary>
    /// <c>true</c> when a page after the current one exists.
    /// </summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>
    /// Projects each item through <paramref name="map"/>, preserving <see cref="Page"/>,
    /// <see cref="PageSize"/>, and <see cref="TotalCount"/>. Lets a handler turn a page of
    /// entities into a page of DTOs without re-stating the pagination metadata.
    /// </summary>
    public PagedResult<TOut> MapItems<TOut>(Func<T, TOut> map) =>
        new([.. Items.Select(map)], Page, PageSize, TotalCount);

    /// <summary>
    /// An empty page (no items, zero total) at the position and size of
    /// <paramref name="pagination"/>. Convenient for short-circuit results.
    /// </summary>
    public static PagedResult<T> Empty(PaginationRequest pagination) =>
        new([], pagination.Page, pagination.PageSize, 0);
}

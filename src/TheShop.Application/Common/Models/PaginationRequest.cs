namespace TheShop.Application.Common.Models;

/// <summary>
/// Requested page position and size for any paged query. Reusable across features —
/// not specific to the product catalogue.
/// </summary>
public sealed record PaginationRequest(int Page = 1, int PageSize = 12)
{
    /// <summary>
    /// The number of items to skip to reach the start of the current page.
    /// </summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Returns a copy with <see cref="Page"/> clamped to at least 1 and
    /// <see cref="PageSize"/> clamped into <c>1..maxPageSize</c>.
    /// </summary>
    public PaginationRequest Normalized(int maxPageSize) =>
        this with
        {
            Page = Math.Max(Page, 1),
            PageSize = Math.Clamp(PageSize, 1, maxPageSize),
        };

    /// <summary>
    /// Returns the zero-based, inclusive row range <c>(From, To)</c> for the current page,
    /// suitable for range-based data sources (e.g. a Postgrest <c>Range</c> header).
    /// </summary>
    public (int From, int To) ToInclusiveRange() => (Skip, Skip + PageSize - 1);
}

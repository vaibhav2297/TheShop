using System.Globalization;
using Microsoft.Extensions.Primitives;
using TheShop.Domain.Enums;
using TheShop.Web.Common;
using TheShop.Web.Common.Sorting;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// The deep-linkable state of the manage-brands admin list: the search term, the status filter,
/// the sort order, and the current page. Round-trips through the URL query string via
/// <see cref="IUrlQueryState{TSelf}"/> so a filtered/sorted view is shareable and survives refresh
/// and Back/Forward. Defaults are omitted from the URL to keep links clean (RULE-10).
/// </summary>
/// <param name="Search">The case-insensitive name search term, or <c>null</c> for none.</param>
/// <param name="Status">The status filter, or <c>null</c> for none — brands of every status.</param>
/// <param name="Sort">The sort order.</param>
/// <param name="Page">The 1-based page number.</param>
public sealed record BrandQueryState(
    string? Search,
    BrandStatusFilter? Status,
    BrandSortOption Sort,
    int Page) : IUrlQueryState<BrandQueryState>
{
    private const string SearchKey = "search";
    private const string StatusKey = "status";
    private const string SortKey = "sort";
    private const string PageKey = "page";

    private const string ActiveStatusValue = "active";
    private const string InactiveStatusValue = "inactive";

    /// <summary>The empty manage-brands view: no search, no status filter, name A→Z, page 1.</summary>
    public static BrandQueryState Default { get; } =
        new(null, null, BrandSortCatalogue.Instance.Default, 1);

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> ToQueryParameters()
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(Search))
            parameters[SearchKey] = Search;

        if (Status is { } status)
            parameters[StatusKey] = StatusToValue(status);

        if (Sort != BrandSortCatalogue.Instance.Default)
            parameters[SortKey] = BrandSortCatalogue.Instance.ToSlug(Sort);

        if (Page > 1)
            parameters[PageKey] = Page;

        return parameters;
    }

    /// <inheritdoc/>
    public static BrandQueryState FromQuery(IReadOnlyDictionary<string, StringValues> query) =>
        new(
            GetString(query, SearchKey),
            ParseStatus(GetString(query, StatusKey)),
            BrandSortCatalogue.Instance.FromSlug(GetString(query, SortKey)),
            ParsePage(query));

    private static string StatusToValue(BrandStatusFilter status) => status switch
    {
        BrandStatusFilter.Active => ActiveStatusValue,
        _ => InactiveStatusValue,
    };

    /// <summary>
    /// Reads the status filter from the URL. An absent or unrecognised value yields no filter,
    /// so a hand-edited link degrades to the unfiltered list rather than failing.
    /// </summary>
    private static BrandStatusFilter? ParseStatus(string? value) => value switch
    {
        ActiveStatusValue => BrandStatusFilter.Active,
        InactiveStatusValue => BrandStatusFilter.Inactive,
        _ => null,
    };

    private static string? GetString(IReadOnlyDictionary<string, StringValues> query, string key) =>
        query.TryGetValue(key, out var value) ? value.ToString() : null;

    private static int ParsePage(IReadOnlyDictionary<string, StringValues> query) =>
        query.TryGetValue(PageKey, out var value)
        && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var page)
        && page > 1
            ? page
            : 1;
}

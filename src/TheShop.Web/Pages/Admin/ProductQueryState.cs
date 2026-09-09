using System.Globalization;
using Microsoft.Extensions.Primitives;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Features.Products;
using TheShop.Domain.Enums;
using TheShop.Web.Common;
using TheShop.Web.Common.Sorting;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// The deep-linkable state of the manage-products admin list: the search term, the status
/// filter, the selected brand/category multi-select filters, the price bounds, the sort order,
/// and the current page. Round-trips through the URL query string via
/// <see cref="IUrlQueryState{TSelf}"/> so a filtered/sorted view is shareable and survives
/// refresh and Back/Forward. Defaults are omitted from the URL to keep links clean.
/// </summary>
/// <param name="Search">The case-insensitive name search term, or <c>null</c> for none.</param>
/// <param name="Status">The status filter, or <c>null</c> for none — products of every status.</param>
/// <param name="Filters">The applied multi-select filters (brand / category).</param>
/// <param name="PriceMin">The lower price bound, or <c>null</c> for no lower bound.</param>
/// <param name="PriceMax">The upper price bound, or <c>null</c> for no upper bound.</param>
/// <param name="Sort">The sort order.</param>
/// <param name="Page">The 1-based page number.</param>
public sealed record ProductQueryState(
    string? Search,
    ProductStatusFilter? Status,
    IReadOnlyList<AppliedFilterDto> Filters,
    decimal? PriceMin,
    decimal? PriceMax,
    AdminProductSortOption Sort,
    int Page) : IUrlQueryState<ProductQueryState>
{
    private const string SearchKey = "search";
    private const string StatusKey = "status";
    private const string PriceMinKey = "price_min";
    private const string PriceMaxKey = "price_max";
    private const string SortKey = "sort";
    private const string PageKey = "page";

    private const string ActiveStatusValue = "active";
    private const string InactiveStatusValue = "inactive";

    /// <summary>The empty manage-products view: no search/filters/bounds, name A→Z, page 1.</summary>
    public static ProductQueryState Default { get; } =
        new(null, null, [], null, null, AdminProductSortCatalogue.Instance.Default, 1);

    /// <summary>
    /// Returns this state with a single multi-select filter option toggled on or off and the page
    /// reset to 1. Merging one atomic toggle against the caller's current state is what lets rapid
    /// successive toggles compound instead of racing — see <c>CatalogueQueryState.ToggleFilter</c>.
    /// </summary>
    public ProductQueryState ToggleFilter(string groupKey, string value, bool isSelected)
    {
        var existing = Filters.FirstOrDefault(f => f.Key == groupKey);
        var values = existing?.Values.ToList() ?? [];

        if (isSelected)
        {
            if (!values.Contains(value))
                values.Add(value);
        }
        else
        {
            values.Remove(value);
        }

        var filters = Filters.Where(f => f.Key != groupKey).ToList();
        if (values.Count > 0)
            filters.Add(new AppliedFilterDto(groupKey, values));

        return this with { Filters = filters, Page = 1 };
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> ToQueryParameters()
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(Search))
            parameters[SearchKey] = Search;

        if (Status is { } status)
            parameters[StatusKey] = StatusToValue(status);

        foreach (var filter in Filters)
        {
            if (filter.Values.Count > 0)
                parameters[filter.Key] = filter.Values.ToArray();
        }

        if (PriceMin is { } min)
            parameters[PriceMinKey] = min.ToString(CultureInfo.InvariantCulture);

        if (PriceMax is { } max)
            parameters[PriceMaxKey] = max.ToString(CultureInfo.InvariantCulture);

        if (Sort != AdminProductSortCatalogue.Instance.Default)
            parameters[SortKey] = AdminProductSortCatalogue.Instance.ToSlug(Sort);

        if (Page > 1)
            parameters[PageKey] = Page;

        return parameters;
    }

    /// <inheritdoc/>
    public static ProductQueryState FromQuery(IReadOnlyDictionary<string, StringValues> query)
    {
        var filters = new List<AppliedFilterDto>();
        foreach (var key in ProductFilterKeys.SelectableKeys)
        {
            if (!query.TryGetValue(key, out var raw))
                continue;

            var values = raw
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToList();

            if (values.Count > 0)
                filters.Add(new AppliedFilterDto(key, values));
        }

        return new ProductQueryState(
            GetString(query, SearchKey),
            ParseStatus(GetString(query, StatusKey)),
            filters,
            ParseDecimal(query, PriceMinKey),
            ParseDecimal(query, PriceMaxKey),
            AdminProductSortCatalogue.Instance.FromSlug(GetString(query, SortKey)),
            ParsePage(query));
    }

    private static string StatusToValue(ProductStatusFilter status) => status switch
    {
        ProductStatusFilter.Active => ActiveStatusValue,
        _ => InactiveStatusValue,
    };

    /// <summary>
    /// Reads the status filter from the URL. An absent or unrecognised value yields no filter, so
    /// a hand-edited link degrades to the unfiltered list rather than failing.
    /// </summary>
    private static ProductStatusFilter? ParseStatus(string? value) => value switch
    {
        ActiveStatusValue => ProductStatusFilter.Active,
        InactiveStatusValue => ProductStatusFilter.Inactive,
        _ => null,
    };

    private static string? GetString(IReadOnlyDictionary<string, StringValues> query, string key) =>
        query.TryGetValue(key, out var value) ? value.ToString() : null;

    private static decimal? ParseDecimal(IReadOnlyDictionary<string, StringValues> query, string key) =>
        query.TryGetValue(key, out var value)
        && decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static int ParsePage(IReadOnlyDictionary<string, StringValues> query) =>
        query.TryGetValue(PageKey, out var value)
        && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var page)
        && page > 1
            ? page
            : 1;
}

using System.Globalization;
using Microsoft.Extensions.Primitives;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Features.Products;
using TheShop.Domain.Enums;
using TheShop.Web.Common;
using TheShop.Web.Common.Sorting;

namespace TheShop.Web.Pages.Products;

/// <summary>
/// The deep-linkable state of the product catalogue: the selected multi-select filters, the price
/// bounds, the sort order, and the current page. Round-trips through the URL query string via
/// <see cref="IUrlQueryState{TSelf}"/> so a filtered/sorted view is shareable and survives refresh
/// and Back/Forward. Defaults are omitted from the URL to keep links clean.
/// </summary>
/// <param name="Filters">The applied multi-select filters (category / brand / flavour / nicotine).</param>
/// <param name="PriceMin">The lower price bound, or <c>null</c> for no lower bound.</param>
/// <param name="PriceMax">The upper price bound, or <c>null</c> for no upper bound.</param>
/// <param name="Sort">The sort order.</param>
/// <param name="Page">The 1-based page number.</param>
public sealed record CatalogueQueryState(
    IReadOnlyList<AppliedFilterDto> Filters,
    decimal? PriceMin,
    decimal? PriceMax,
    ProductSortOption Sort,
    int Page) : IUrlQueryState<CatalogueQueryState>
{
    private const string PriceMinKey = "price_min";
    private const string PriceMaxKey = "price_max";
    private const string SortKey = "sort";
    private const string PageKey = "page";

    /// <summary>The empty catalogue view: no filters, no price bounds, newest-first, page 1.</summary>
    public static CatalogueQueryState Default { get; } =
        new([], null, null, ProductSortCatalogue.Instance.Default, 1);

    /// <summary>
    /// Returns this state with a single filter option toggled on or off and the page reset to 1.
    /// Merging one atomic toggle against the caller's current state — rather than having the panel
    /// recompute the whole selection from a parameter that only refreshes after the URL round-trip —
    /// is what lets rapid successive toggles compound instead of racing: each toggle builds on the
    /// state the previous one produced, so none is silently dropped.
    /// </summary>
    /// <param name="groupKey">The filter group the option belongs to.</param>
    /// <param name="value">The option value being toggled.</param>
    /// <param name="isSelected">Whether the option is now selected.</param>
    public CatalogueQueryState ToggleFilter(string groupKey, string value, bool isSelected)
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

        foreach (var filter in Filters)
        {
            if (filter.Values.Count > 0)
                parameters[filter.Key] = filter.Values.ToArray();
        }

        if (PriceMin is { } min)
            parameters[PriceMinKey] = min.ToString(CultureInfo.InvariantCulture);

        if (PriceMax is { } max)
            parameters[PriceMaxKey] = max.ToString(CultureInfo.InvariantCulture);

        if (Sort != ProductSortCatalogue.Instance.Default)
            parameters[SortKey] = ProductSortCatalogue.Instance.ToSlug(Sort);

        if (Page > 1)
            parameters[PageKey] = Page;

        return parameters;
    }

    /// <inheritdoc/>
    public static CatalogueQueryState FromQuery(IReadOnlyDictionary<string, StringValues> query)
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

        return new CatalogueQueryState(
            filters,
            ParseDecimal(query, PriceMinKey),
            ParseDecimal(query, PriceMaxKey),
            ProductSortCatalogue.Instance.FromSlug(GetString(query, SortKey)),
            ParsePage(query));
    }

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

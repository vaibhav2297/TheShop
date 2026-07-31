using System.Globalization;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Features.Products;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Filtering;

/// <summary>
/// One entry in the product catalogue's filter-definition registry: the multi-select filter
/// <paramref name="Key"/> (matches <see cref="ProductFilterKeys"/>), its resource
/// <paramref name="LabelKey"/>, the <c>products</c> column its predicate is built against, and
/// <paramref name="ProjectOptions"/> — how to shape this group's options out of the single
/// <see cref="CatalogueFiltersRecord"/> the <c>get_catalogue_filters()</c> RPC returns.
/// </summary>
internal sealed record ProductFilterDefinition(
    string Key,
    string LabelKey,
    string Column,
    Func<CatalogueFiltersRecord, IReadOnlyList<FilterOptionDto>> ProjectOptions);

/// <summary>
/// The filter-definition registry for the product catalogue's multi-select filter groups
/// (category, brand, flavour, nicotine strength). Adding a new filter dimension is one new entry
/// here; <see cref="Repositories.SupabaseProductRepository"/> reads this list both to project the
/// <c>GetFilterGroupsAsync</c> response from the RPC payload and to resolve applied-filter
/// predicates in <c>GetPageAsync</c>. The <see cref="FilterKind.Range"/> price group is not part
/// of this registry — it is read from the RPC's <c>price_min</c>/<c>price_max</c> fields directly
/// (see <see cref="ProductFilterKeys.SelectableKeys"/>).
/// </summary>
internal static class ProductFilterDefinitions
{
    private const string CategoryLabelKey = "Filter_Category";
    private const string BrandLabelKey = "Filter_Brand";
    private const string FlavourLabelKey = "Filter_Flavour";
    private const string NicotineLabelKey = "Filter_NicotineStrength";

    /// <summary>
    /// Resource key for the Price filter group's label. Price is not part of
    /// <see cref="All"/> because it is a <see cref="FilterKind.Range"/> group, not a
    /// multi-select one, but shares this registry's label-key naming convention.
    /// </summary>
    public const string PriceLabelKey = "Filter_Price";

    public static readonly IReadOnlyList<ProductFilterDefinition> All =
    [
        new(ProductFilterKeys.Category, CategoryLabelKey, "category_id",
            facets => facets.Categories
                .Select(c => new FilterOptionDto(c.Id.ToString(), c.Name, null))
                .ToList()),
        new(ProductFilterKeys.Brand, BrandLabelKey, "brand_id",
            facets => facets.Brands
                .Select(b => new FilterOptionDto(b.Id.ToString(), b.Name, null))
                .ToList()),
        new(ProductFilterKeys.Flavour, FlavourLabelKey, "flavour",
            facets => facets.Flavours
                .Select(f => new FilterOptionDto(f, f, null))
                .ToList()),
        new(ProductFilterKeys.Nicotine, NicotineLabelKey, "nicotine_strength_mg",
            facets => facets.NicotineStrengths
                .Select(mg => new FilterOptionDto(
                    mg.ToString(CultureInfo.InvariantCulture), $"{mg} mg", null))
                .ToList()),
    ];

    public static ProductFilterDefinition? FindByKey(string key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
}

using System.Globalization;
using static Supabase.Postgrest.Constants;
using Supabase.Postgrest.Interfaces;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Filtering;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Paging;
using TheShop.Infrastructure.Persistence.Records;
using TheShop.Domain.Enums;

namespace TheShop.Infrastructure.Persistence.Repositories;

/// <summary>
/// Supabase-backed implementation of <see cref="IProductRepository"/>. Reads the
/// <c>products</c> table (embedding <c>categories</c>/<c>brands</c> via Postgrest resource
/// embedding) and maps rows to the domain <see cref="Product"/> aggregate. RLS restricts every
/// query to published products; the repository additionally filters on <c>is_published</c>
/// explicitly so its behaviour does not depend on which role executes the query.
/// </summary>
public sealed class SupabaseProductRepository(Supabase.Client client) : IProductRepository
{
    private const string IsPublishedColumn = "is_published";
    // The storefront filters and sorts on the price the customer actually pays — the sale
    // price when discounted, else the original — so both use the generated effective_price
    // column (COALESCE(sale_price, original_price); migration 0006) rather than original_price.
    private const string EffectivePriceColumn = "effective_price";
    private const string NameColumn = "name";
    private const string CreatedAtColumn = "created_at";
    private const string IdColumn = "id";
    private const string CatalogueFiltersRpc = "get_catalogue_filters";

    // A product's image_path is an object key inside this bucket; GetPublicUrl turns it into
    // the stable, cacheable public URL the storefront renders.
    private const string ProductImagesBucket = "product-images";

    // Supabase.Postgrest 4.0.3's Filter<TCriterion> only pattern-matches string/int/float/
    // IDictionary/IList/IntRange/FullTextSearchConfig criteria — a raw bool or decimal falls
    // through to its default case and throws PostgrestException. Passing the invariant string
    // form instead lands on the supported `string` branch and produces the same `eq.true` /
    // `eq.<number>` wire format.
    private const string PublishedValue = "true";

    // get_catalogue_filters() takes no arguments; Rpc still requires a params object, so send an
    // empty one (serialized to `{}`) rather than null.
    private static readonly object EmptyRpcArgs = new();

    /// <inheritdoc/>
    public async Task<PagedResult<Product>> GetPageAsync(
        ProductCatalogueCriteria criteria, CancellationToken ct)
    {
        var recordPage = await PostgrestPaginationExtensions.GetPagedAsync(
            () => ApplyFilters(client.From<ProductRecord>(), criteria),
            query => ApplySort(query, criteria.Sort),
            criteria.Pagination,
            ct);

        return recordPage.MapItems(record => record.ToDomain(ResolveImagePublicUrl));
    }

    // Resolves a Storage object key to its public URL. GetPublicUrl is pure string composition
    // (no network call), so it is safe to call per row while mapping.
    private string ResolveImagePublicUrl(string imagePath) =>
        client.Storage.From(ProductImagesBucket).GetPublicUrl(imagePath);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FilterGroupDto>> GetFilterGroupsAsync(CancellationToken ct)
    {
        // One server-side aggregation (get_catalogue_filters) returns every filter group — the
        // category/brand lookups, the distinct flavour/nicotine facets, and the price range — in
        // a single round-trip, so the client never downloads the whole catalogue to compute them.
        var facets = await client.Rpc<CatalogueFiltersRecord>(CatalogueFiltersRpc, EmptyRpcArgs)
            ?? new CatalogueFiltersRecord();

        var groups = ProductFilterDefinitions.All
            .Select(definition => new FilterGroupDto(
                definition.Key, definition.LabelKey, FilterKind.MultiSelect, definition.ProjectOptions(facets), null))
            .ToList();

        groups.Add(new FilterGroupDto(
            ProductFilterKeys.Price, ProductFilterDefinitions.PriceLabelKey, FilterKind.Range, [],
            new RangeFilterDto(facets.PriceMin, facets.PriceMax)));

        return groups;
    }

    private static IPostgrestTable<ProductRecord> ApplyFilters(
        IPostgrestTable<ProductRecord> query, ProductCatalogueCriteria criteria)
    {
        query.Filter(IsPublishedColumn, Operator.Equals, PublishedValue);

        foreach (var applied in criteria.SelectedFilters)
        {
            if (applied.Values.Count == 0)
                continue;

            var definition = ProductFilterDefinitions.FindByKey(applied.Key);
            if (definition is null)
                continue;

            query.Filter(definition.Column, Operator.In, applied.Values.ToList());
        }

        if (criteria.PriceMin is decimal min)
            query.Filter(EffectivePriceColumn, Operator.GreaterThanOrEqual, min.ToString(CultureInfo.InvariantCulture));

        if (criteria.PriceMax is decimal max)
            query.Filter(EffectivePriceColumn, Operator.LessThanOrEqual, max.ToString(CultureInfo.InvariantCulture));

        return query;
    }

    private static void ApplySort(IPostgrestTable<ProductRecord> query, ProductSortOption sort)
    {
        switch (sort)
        {
            case ProductSortOption.PriceLowToHigh:
                query.Order(EffectivePriceColumn, Ordering.Ascending);
                break;
            case ProductSortOption.PriceHighToLow:
                query.Order(EffectivePriceColumn, Ordering.Descending);
                break;
            case ProductSortOption.NameAToZ:
                query.Order(NameColumn, Ordering.Ascending);
                break;
            case ProductSortOption.NameZToA:
                query.Order(NameColumn, Ordering.Descending);
                break;
            case ProductSortOption.NewestFirst:
            default:
                query.Order(CreatedAtColumn, Ordering.Descending);
                break;
        }

        // Every sort column above is non-unique, and Postgres leaves the relative order of tied rows
        // undefined — so two products sharing a price or a creation timestamp could be ordered one
        // way while page 1 is fetched and the other way for page 2, showing one of them twice and
        // hiding the other entirely. Breaking the tie on the primary key makes the total order
        // deterministic across requests.
        query.Order(IdColumn, Ordering.Ascending);
    }

}

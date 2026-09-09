using System.Globalization;
using static Supabase.Postgrest.Constants;
using Supabase.Postgrest.Interfaces;
using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
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
/// embedding) and maps rows to the domain <see cref="Product"/> aggregate for the customer
/// catalogue. The admin CRUD side loads/writes the full aggregate — gallery, option types,
/// variants — via separate per-table reads and the <c>save_product</c> RPC (plan Decision 1),
/// since Postgrest resource embedding does not extend to writing a multi-table aggregate
/// atomically. RLS restricts every customer-facing query to published products; the repository
/// additionally filters on <c>is_published</c> explicitly so its behaviour does not depend on
/// which role executes the query.
/// </summary>
public sealed class SupabaseProductRepository(Supabase.Client client, IFileStorage fileStorage) : IProductRepository
{
    private const string IsPublishedColumn = "is_published";
    // The storefront filters and sorts on the price the customer actually pays, so both use the
    // generated display_price column (COALESCE(sale_price, original_price, min_variant_price);
    // migration 0027) rather than original_price. It falls back to the lowest variant price because
    // a product with variants has no price of its own — filtering on effective_price alone dropped
    // every such product out of the price range and sorted it as NULL.
    private const string DisplayPriceColumn = "display_price";
    private const string NameColumn = "name";
    private const string CreatedAtColumn = "created_at";
    private const string IdColumn = "id";
    private const string ProductIdColumn = "product_id";
    private const string OptionTypeIdColumn = "option_type_id";
    private const string VariantIdColumn = "variant_id";
    private const string SkuNormalizedColumn = "sku_normalized";
    private const string CatalogueFiltersRpc = "get_catalogue_filters";
    private const string SaveProductRpc = "save_product";
    private const string AdminProductsPageRpc = "admin_products_page";
    private const string AdminProductFiltersRpc = "get_admin_product_filters";
    private const string DeleteProductsRpc = "delete_products";

    // Matches the DB constraint names from migrations 0022/0023/0024 — the race-condition
    // backstop behind the FindConflictsAsync pre-check.
    private const string SkuRegistryPrimaryKeyConstraint = "product_sku_registry_pkey";
    private const string NormalizedNameUniqueConstraint = "ux_products_name_normalized";
    private const string ConcurrencyErrorMarker = "concurrent modification";

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
            query.Filter(DisplayPriceColumn, Operator.GreaterThanOrEqual, min.ToString(CultureInfo.InvariantCulture));

        if (criteria.PriceMax is decimal max)
            query.Filter(DisplayPriceColumn, Operator.LessThanOrEqual, max.ToString(CultureInfo.InvariantCulture));

        return query;
    }

    private static void ApplySort(IPostgrestTable<ProductRecord> query, ProductSortOption sort)
    {
        switch (sort)
        {
            case ProductSortOption.PriceLowToHigh:
                query.Order(DisplayPriceColumn, Ordering.Ascending);
                break;
            case ProductSortOption.PriceHighToLow:
                query.Order(DisplayPriceColumn, Ordering.Descending);
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

    /// <inheritdoc/>
    public async Task<ProductConflicts> FindConflictsAsync(
        string name, IReadOnlyCollection<string> skus, Guid? excludeProductId, CancellationToken ct)
    {
        var nameQuery = client.From<ProductRecord>();
        nameQuery.Filter(NameColumn, Operator.ILike, name.Trim());
        if (excludeProductId is { } excludeId)
            nameQuery.Filter(IdColumn, Operator.NotEqual, excludeId.ToString());
        var nameResponse = await nameQuery.Get(ct);
        var nameTaken = nameResponse.Models.Count > 0;

        var skuList = skus.Where(s => s.Length > 0).Distinct().ToList();
        var skusTaken = new List<string>();
        if (skuList.Count > 0)
        {
            var registryQuery = client.From<ProductSkuRegistryRecord>();
            registryQuery.Filter(SkuNormalizedColumn, Operator.In, skuList);
            if (excludeProductId is { } excludeRegistryId)
                registryQuery.Filter(ProductIdColumn, Operator.NotEqual, excludeRegistryId.ToString());
            var registryResponse = await registryQuery.Get(ct);
            skusTaken = [.. registryResponse.Models.Select(r => r.SkuNormalized)];
        }

        return new ProductConflicts(nameTaken, skusTaken);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ProductListItemDto>> GetAdminPageAsync(AdminProductCriteria criteria, CancellationToken ct)
    {
        var args = new
        {
            p_search = criteria.Search,
            p_status = ResolveStatusArg(criteria.Status),
            p_brand_ids = criteria.BrandIds,
            p_category_ids = criteria.CategoryIds,
            p_price_min = criteria.PriceMin,
            p_price_max = criteria.PriceMax,
            p_sort = ResolveSortArg(criteria.Sort),
            p_limit = criteria.Pagination.PageSize,
            p_offset = criteria.Pagination.Skip,
        };

        var rows = await client.Rpc<List<AdminProductRowRecord>>(AdminProductsPageRpc, args) ?? [];
        if (rows.Count == 0)
            return PagedResult<ProductListItemDto>.Empty(criteria.Pagination);

        var totalCount = (int)rows[0].TotalCount;
        var items = rows.Select(row => row.ToDto(fileStorage)).ToList();
        return new PagedResult<ProductListItemDto>(items, criteria.Pagination.Page, criteria.Pagination.PageSize, totalCount);
    }

    /// <inheritdoc/>
    public async Task<AdminProductFiltersDto> GetAdminFiltersAsync(CancellationToken ct)
    {
        var record = await client.Rpc<AdminProductFiltersRecord>(AdminProductFiltersRpc, EmptyRpcArgs)
            ?? new AdminProductFiltersRecord();

        return record.ToDto();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Product>> GetManyWithVariantsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];

        var idStrings = ids.Select(id => id.ToString()).ToList();

        var productQuery = client.From<ProductRecord>();
        productQuery.Filter(IdColumn, Operator.In, idStrings);
        var productResponse = await productQuery.Get(ct);

        var variantQuery = client.From<ProductVariantRecord>();
        variantQuery.Filter(ProductIdColumn, Operator.In, idStrings);
        var variantResponse = await variantQuery.Get(ct);

        return [.. productResponse.Models.Select(record =>
        {
            var variants = variantResponse.Models
                .Where(v => v.ProductId == record.Id)
                .OrderBy(v => v.Position)
                .Select(v => v.ToDomain((IReadOnlySet<Guid>)new HashSet<Guid>()))
                .ToList();

            return record.ToDomain(ResolveImagePublicUrl, variants: variants);
        })];
    }

    /// <inheritdoc/>
    public async Task<int> SetPublishedAsync(IReadOnlyList<Guid> ids, bool isPublished, CancellationToken ct)
    {
        if (ids.Count == 0)
            return 0;

        var response = await client.From<ProductRecord>()
            .Filter(IdColumn, Operator.In, ids.Select(id => id.ToString()).ToList())
            .Filter(IsPublishedColumn, Operator.Equals, isPublished ? "false" : "true")
            .Set(x => x.IsPublished, isPublished)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Update(cancellationToken: ct);

        return response.Models.Count;
    }

    /// <inheritdoc/>
    public async Task<(ProductDeletionOutcomeDto Outcome, IReadOnlyList<string> DeletedImageKeys)> DeleteManyAsync(
        IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var args = new { product_ids = ids };
        var rows = await client.Rpc<List<DeleteProductsResultRecord>>(DeleteProductsRpc, args) ?? [];

        var deleted = rows.Where(r => r.Deleted).ToList();
        var blocked = rows
            .Where(r => !r.Deleted)
            .Select(r => new ReferencedProductDto(r.Id, r.Name, (int)r.ReferenceCount))
            .ToList();

        var deletedImageKeys = deleted.SelectMany(r => r.ImageKeys).ToList();

        return (new ProductDeletionOutcomeDto(deleted.Count, blocked), deletedImageKeys);
    }

    private static string? ResolveStatusArg(ProductStatusFilter? status) =>
        status switch
        {
            ProductStatusFilter.Active => "active",
            ProductStatusFilter.Inactive => "inactive",
            _ => null,
        };

    private static string ResolveSortArg(AdminProductSortOption sort) =>
        sort switch
        {
            AdminProductSortOption.NameZToA => "name-desc",
            AdminProductSortOption.NewestFirst => "newest",
            AdminProductSortOption.OldestFirst => "oldest",
            AdminProductSortOption.LowestVariantPriceAsc => "price-asc",
            AdminProductSortOption.LowestVariantPriceDesc => "price-desc",
            _ => "name-asc",
        };

    /// <inheritdoc/>
    public async Task<(Product Product, string RowVersion)?> GetForEditAsync(Guid id, CancellationToken ct)
    {
        var response = await client.From<ProductRecord>().Where(x => x.Id == id).Single(ct);
        if (response is null)
            return null;

        var imageRecords = await client.From<ProductImageRecord>().Where(x => x.ProductId == id).Get(ct);
        var optionTypeRecords = await client.From<ProductOptionTypeRecord>().Where(x => x.ProductId == id).Get(ct);
        var typeIds = optionTypeRecords.Models.Select(t => t.Id).ToList();
        var optionValueRecords = typeIds.Count > 0 ? await GetOptionValuesAsync(typeIds, ct) : [];
        var variantRecords = await client.From<ProductVariantRecord>().Where(x => x.ProductId == id).Get(ct);
        var variantIds = variantRecords.Models.Select(v => v.Id).ToList();
        var variantOptionValueRecords = variantIds.Count > 0 ? await GetVariantOptionValuesAsync(variantIds, ct) : [];

        var images = imageRecords.Models.OrderBy(i => i.Position).Select(i => i.ToDomain()).ToList();
        var optionTypes = optionTypeRecords.Models
            .OrderBy(t => t.Position)
            .Select(t => t.ToDomain([.. optionValueRecords.Where(v => v.OptionTypeId == t.Id).OrderBy(v => v.Position)]))
            .ToList();
        var variants = variantRecords.Models
            .OrderBy(v => v.Position)
            .Select(v => v.ToDomain(
                (IReadOnlySet<Guid>)new HashSet<Guid>(
                    variantOptionValueRecords.Where(l => l.VariantId == v.Id).Select(l => l.OptionValueId))))
            .ToList();

        var product = response.ToDomain(ResolveImagePublicUrl, images, optionTypes, variants);
        return (product, response.UpdatedAt.ToString("O"));
    }

    /// <inheritdoc/>
    public async Task<Result<string>> AddAsync(Product product, CancellationToken ct)
    {
        var payload = BuildSavePayload(product, mode: "create", expectedUpdatedAt: null);

        try
        {
            var rows = await client.Rpc<List<SaveProductResultRecord>>(SaveProductRpc, new { payload }) ?? [];
            var row = rows.FirstOrDefault();
            return row is not null
                ? Result.Ok(row.UpdatedAt.ToString("O"))
                : Result.Fail<string>(ProductErrorKeys.CreateFailed);
        }
        catch (Exception ex) when (IsSkuUniqueViolation(ex))
        {
            return Result.Fail<string>(ProductErrorKeys.SkuAlreadyExists);
        }
        catch (Exception ex) when (IsNameUniqueViolation(ex))
        {
            return Result.Fail<string>(ProductErrorKeys.NameAlreadyExists);
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string>> UpdateAsync(Product product, string expectedRowVersion, CancellationToken ct)
    {
        var payload = BuildSavePayload(product, mode: "update", expectedUpdatedAt: expectedRowVersion);

        try
        {
            var rows = await client.Rpc<List<SaveProductResultRecord>>(SaveProductRpc, new { payload }) ?? [];
            var row = rows.FirstOrDefault();
            return row is not null
                ? Result.Ok(row.UpdatedAt.ToString("O"))
                : Result.Fail<string>(ProductErrorKeys.UpdateFailed);
        }
        catch (Exception ex) when (IsConcurrencyViolation(ex))
        {
            return Result.Fail<string>(ProductErrorKeys.ModifiedElsewhere);
        }
        catch (Exception ex) when (IsSkuUniqueViolation(ex))
        {
            return Result.Fail<string>(ProductErrorKeys.SkuAlreadyExists);
        }
        catch (Exception ex) when (IsNameUniqueViolation(ex))
        {
            return Result.Fail<string>(ProductErrorKeys.NameAlreadyExists);
        }
    }

    private async Task<List<ProductOptionValueRecord>> GetOptionValuesAsync(
        IReadOnlyList<Guid> typeIds, CancellationToken ct)
    {
        var query = client.From<ProductOptionValueRecord>();
        query.Filter(OptionTypeIdColumn, Operator.In, typeIds.Select(id => id.ToString()).ToList());
        var response = await query.Get(ct);
        return response.Models;
    }

    private async Task<List<ProductVariantOptionValueRecord>> GetVariantOptionValuesAsync(
        IReadOnlyList<Guid> variantIds, CancellationToken ct)
    {
        var query = client.From<ProductVariantOptionValueRecord>();
        query.Filter(VariantIdColumn, Operator.In, variantIds.Select(id => id.ToString()).ToList());
        var response = await query.Get(ct);
        return response.Models;
    }

    private static object BuildSavePayload(Product product, string mode, string? expectedUpdatedAt) => new
    {
        mode,
        product = new
        {
            id = product.Id,
            expected_updated_at = expectedUpdatedAt,
            name = product.Name,
            description = product.Description,
            sku = product.Sku.Value,
            category_id = product.Category.Id,
            brand_id = product.Brand.Id,
            original_price = product.Pricing?.OriginalPrice.Amount,
            sale_price = product.Pricing?.SalePrice?.Amount,
            is_published = product.IsPublished,
        },
        images = product.Images.Select(i => new
        {
            id = i.Id,
            object_key = i.ObjectKey,
            position = i.Position,
            is_primary = i.IsPrimary,
        }),
        option_types = product.OptionTypes.Select(t => new
        {
            id = t.Id,
            name = t.Name,
            position = t.Position,
            values = t.Values.Select(v => new { id = v.Id, value = v.Value, position = v.Position }),
        }),
        variants = product.Variants.Select(v => new
        {
            id = v.Id,
            sku = v.Sku.Value,
            original_price = v.Pricing?.OriginalPrice.Amount,
            sale_price = v.Pricing?.SalePrice?.Amount,
            is_available = v.IsAvailable,
            image_id = v.PinnedImageId,
            position = v.Position,
            option_value_ids = v.OptionValueIds,
        }),
    };

    private string ResolveImagePublicUrl(string objectKey) => fileStorage.GetPublicUrl(StorageArea.ProductImages, objectKey);

    private static bool IsSkuUniqueViolation(Exception ex) =>
        ex.Message.Contains(SkuRegistryPrimaryKeyConstraint, StringComparison.OrdinalIgnoreCase);

    private static bool IsNameUniqueViolation(Exception ex) =>
        ex.Message.Contains(NormalizedNameUniqueConstraint, StringComparison.OrdinalIgnoreCase);

    private static bool IsConcurrencyViolation(Exception ex) =>
        ex.Message.Contains(ConcurrencyErrorMarker, StringComparison.OrdinalIgnoreCase);
}

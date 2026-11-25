using static Supabase.Postgrest.Constants;
using Supabase.Postgrest.Interfaces;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Brands;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.Enums;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Paging;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Repositories;

/// <summary>
/// Supabase-backed implementation of <see cref="IBrandRepository"/>. The manage-brands paged list
/// and per-brand product counts read <c>brands</c> and the <c>brand_product_counts</c>/
/// <c>delete_brands</c> <c>SECURITY DEFINER</c> RPCs directly — no client-visible RLS policy can
/// answer either question (plan §5 Decisions 2/3).
/// </summary>
public sealed class SupabaseBrandRepository(Supabase.Client client, IFileStorage fileStorage) : IBrandRepository
{
    // Matches the DB unique index name from migration 0012 (ux_brands_normalized_name),
    // the race-condition backstop behind the ExistsByNormalizedNameAsync pre-check.
    private const string NormalizedNameUniqueConstraint = "ux_brands_normalized_name";

    private const string IdColumn = "id";
    private const string NameColumn = "name";
    private const string IsActiveColumn = "is_active";
    private const string CreatedAtColumn = "created_at";

    private const string BrandProductCountsRpc = "brand_product_counts";
    private const string DeleteBrandsRpc = "delete_brands";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BrandLookupDto>> GetActiveLookupAsync(CancellationToken ct)
    {
        var query = client.From<BrandRecord>();
        query.Filter(IsActiveColumn, Operator.Equals, "true");
        query.Order(NameColumn, Ordering.Ascending);
        var response = await query.Get(ct);
        return [.. response.Models.Select(r => new BrandLookupDto(r.Id, r.Name))];
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsByNormalizedNameAsync(string name, Guid? excludeBrandId, CancellationToken ct)
    {
        var trimmed = name.Trim();

        var query = client.From<BrandRecord>();
        query.Filter(NameColumn, Operator.ILike, trimmed);

        if (excludeBrandId is { } id)
            query.Filter(IdColumn, Operator.NotEqual, id.ToString());

        var response = await query.Get(ct);
        return response.Models.Count > 0;
    }

    /// <inheritdoc/>
    public async Task<Result> AddAsync(Brand brand, CancellationToken ct)
    {
        var record = brand.ToRecord();

        try
        {
            await client.From<BrandRecord>().Insert(record, cancellationToken: ct);
            return Result.Ok();
        }
        catch (Exception ex) when (IsNormalizedNameUniqueViolation(ex))
        {
            return Result.Fail(BrandErrorKeys.AlreadyExists);
        }
    }

    /// <inheritdoc/>
    public async Task<PagedResult<BrandListItemDto>> GetPageAsync(
        string? search,
        BrandStatusFilter? status,
        BrandSortOption sort,
        PaginationRequest pagination,
        CancellationToken ct)
    {
        var recordPage = await PostgrestPaginationExtensions.GetPagedAsync(
            () => ApplyFilters(client.From<BrandRecord>(), search, status),
            query => ApplySort(query, sort),
            pagination,
            ct);

        var counts = await GetProductCountsAsync(
            recordPage.Items.Select(r => r.Id).ToList(), ct);

        return recordPage.MapItems(record => new BrandListItemDto(
            record.Id,
            record.Name,
            record.Description,
            ResolveLogoPublicUrl(record.LogoPath, record.Name),
            record.IsActive,
            counts.TryGetValue(record.Id, out var count) ? count : 0));
    }

    /// <inheritdoc/>
    public async Task<Brand?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var response = await client.From<BrandRecord>().Where(x => x.Id == id).Single(ct);
        return response?.ToDomain();
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(Brand brand, CancellationToken ct)
    {
        try
        {
            await client.From<BrandRecord>()
                .Where(x => x.Id == brand.Id)
                .Set(x => x.Name, brand.Name)
                // false warning
                .Set(x => x.Description!, brand.Description)
                .Set(x => x.LogoPath!, brand.LogoPath)
                .Set(x => x.IsActive, brand.IsActive)
                .Update(cancellationToken: ct);

            return Result.Ok();
        }
        catch (Exception ex) when (IsNormalizedNameUniqueViolation(ex))
        {
            return Result.Fail(BrandErrorKeys.AlreadyExists);
        }
    }

    /// <inheritdoc/>
    public async Task<(BrandDeletionOutcomeDto Outcome, IReadOnlyList<string> DeletedLogoPaths)> DeleteManyAsync(
        IReadOnlyList<Guid> brandIds, CancellationToken ct)
    {
        var args = new { brand_ids = brandIds };
        var rows = await client.Rpc<List<DeleteBrandsResultRecord>>(DeleteBrandsRpc, args) ?? [];

        var deleted = rows.Where(r => r.Deleted).ToList();
        var blocked = rows
            .Where(r => !r.Deleted)
            .Select(r => new BlockedBrandDto(r.Id, r.Name, (int)r.ProductCount))
            .ToList();

        var deletedLogoPaths = deleted
            .Where(r => !string.IsNullOrEmpty(r.LogoPath))
            .Select(r => r.LogoPath!)
            .ToList();

        return (new BrandDeletionOutcomeDto(deleted.Count, blocked), deletedLogoPaths);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, int>> GetProductCountsAsync(
        IReadOnlyList<Guid> brandIds, CancellationToken ct)
    {
        if (brandIds.Count == 0)
            return new Dictionary<Guid, int>();

        var args = new { brand_ids = brandIds };
        var rows = await client.Rpc<List<BrandProductCountRecord>>(BrandProductCountsRpc, args) ?? [];

        return rows.ToDictionary(r => r.BrandId, r => (int)r.ProductCount);
    }

    private string ResolveLogoPublicUrl(string? logoPath, string name) =>
        logoPath is { } path ? fileStorage.GetPublicUrl(StorageArea.BrandLogos, path) : PlaceholderImage.For(name);

    /// <summary>
    /// Narrows the query by the supplied criteria. Each criterion is applied only when supplied —
    /// a null <paramref name="search"/> or <paramref name="status"/> adds no predicate at all.
    /// </summary>
    private static IPostgrestTable<BrandRecord> ApplyFilters(
        IPostgrestTable<BrandRecord> query, string? search, BrandStatusFilter? status)
    {
        if (!string.IsNullOrWhiteSpace(search))
            query.Filter(NameColumn, Operator.ILike, $"%{search.Trim()}%");

        if (status is { } value)
            query.Filter(IsActiveColumn, Operator.Equals, value is BrandStatusFilter.Active ? "true" : "false");

        return query;
    }

    private static void ApplySort(IPostgrestTable<BrandRecord> query, BrandSortOption sort)
    {
        switch (sort)
        {
            case BrandSortOption.NameZToA:
                query.Order(NameColumn, Ordering.Descending);
                break;
            case BrandSortOption.NewestFirst:
                query.Order(CreatedAtColumn, Ordering.Descending);
                break;
            case BrandSortOption.NameAToZ:
            default:
                query.Order(NameColumn, Ordering.Ascending);
                break;
        }

        // Every sort column above is non-unique, and Postgres leaves the relative order of tied rows
        // undefined — so two brands sharing a name or a creation timestamp could be ordered one way
        // while page 1 is fetched and the other way for page 2, showing one of them twice and hiding
        // the other entirely. Breaking the tie on the primary key makes the total order deterministic
        // across requests.
        query.Order(IdColumn, Ordering.Ascending);
    }

    private static bool IsNormalizedNameUniqueViolation(Exception ex) =>
        ex.Message.Contains(NormalizedNameUniqueConstraint, StringComparison.OrdinalIgnoreCase);
}

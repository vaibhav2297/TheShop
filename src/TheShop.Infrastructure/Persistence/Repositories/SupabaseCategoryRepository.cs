using static Supabase.Postgrest.Constants;
using Supabase.Postgrest.Interfaces;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Categories;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.Enums;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Paging;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Repositories;

/// <summary>
/// Supabase-backed implementation of <see cref="ICategoryRepository"/>. The manage-categories
/// paged list and per-category product counts read <c>categories</c> and the
/// <c>category_product_counts</c>/<c>delete_categories</c> <c>SECURITY DEFINER</c> RPCs directly —
/// no client-visible RLS policy can answer either question (plan §5 Decisions 2/3).
/// </summary>
public sealed class SupabaseCategoryRepository(Supabase.Client client, IFileStorage fileStorage) : ICategoryRepository
{
    // Matches the DB unique index name from migration 0020 (ux_categories_name_normalized),
    // the race-condition backstop behind the ExistsByNormalizedNameAsync pre-check.
    private const string NormalizedNameUniqueConstraint = "ux_categories_name_normalized";

    private const string IdColumn = "id";
    private const string NameColumn = "name";
    private const string IsActiveColumn = "is_active";
    private const string CreatedAtColumn = "created_at";

    private const string CategoryProductCountsRpc = "category_product_counts";
    private const string DeleteCategoriesRpc = "delete_categories";

    /// <inheritdoc/>
    public async Task<bool> ExistsByNormalizedNameAsync(string name, Guid? excludeCategoryId, CancellationToken ct)
    {
        var trimmed = name.Trim();

        var query = client.From<CategoryRecord>();
        query.Filter(NameColumn, Operator.ILike, trimmed);

        if (excludeCategoryId is { } id)
            query.Filter(IdColumn, Operator.NotEqual, id.ToString());

        var response = await query.Get(ct);
        return response.Models.Count > 0;
    }

    /// <inheritdoc/>
    public async Task<Result> AddAsync(Category category, CancellationToken ct)
    {
        var record = category.ToRecord();

        try
        {
            await client.From<CategoryRecord>().Insert(record, cancellationToken: ct);
            return Result.Ok();
        }
        catch (Exception ex) when (IsNormalizedNameUniqueViolation(ex))
        {
            return Result.Fail(CategoryErrorKeys.AlreadyExists);
        }
    }

    /// <inheritdoc/>
    public async Task<PagedResult<CategoryListItemDto>> GetPageAsync(
        string? search,
        CategoryStatusFilter? status,
        CategorySortOption sort,
        PaginationRequest pagination,
        CancellationToken ct)
    {
        var recordPage = await PostgrestPaginationExtensions.GetPagedAsync(
            () => ApplyFilters(client.From<CategoryRecord>(), search, status),
            query => ApplySort(query, sort),
            pagination,
            ct);

        var counts = await GetProductCountsAsync(
            recordPage.Items.Select(r => r.Id).ToList(), ct);

        return recordPage.MapItems(record => new CategoryListItemDto(
            record.Id,
            record.Name,
            record.Description,
            ResolveImagePublicUrl(record.ImagePath, record.Name),
            record.IsActive,
            counts.TryGetValue(record.Id, out var count) ? count : 0));
    }

    /// <inheritdoc/>
    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var response = await client.From<CategoryRecord>().Where(x => x.Id == id).Single(ct);
        return response?.ToDomain();
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(Category category, CancellationToken ct)
    {
        try
        {
            await client.From<CategoryRecord>()
                .Where(x => x.Id == category.Id)
                .Set(x => x.Name, category.Name)
                // false warning
                .Set(x => x.Description!, category.Description)
                .Set(x => x.ImagePath!, category.ImagePath)
                .Set(x => x.IsActive, category.IsActive)
                .Update(cancellationToken: ct);

            return Result.Ok();
        }
        catch (Exception ex) when (IsNormalizedNameUniqueViolation(ex))
        {
            return Result.Fail(CategoryErrorKeys.AlreadyExists);
        }
    }

    /// <inheritdoc/>
    public async Task<(CategoryDeletionOutcomeDto Outcome, IReadOnlyList<string> DeletedImagePaths)> DeleteManyAsync(
        IReadOnlyList<Guid> categoryIds, CancellationToken ct)
    {
        var args = new { category_ids = categoryIds };
        var rows = await client.Rpc<List<DeleteCategoriesResultRecord>>(DeleteCategoriesRpc, args) ?? [];

        var deleted = rows.Where(r => r.Deleted).ToList();
        var blocked = rows
            .Where(r => !r.Deleted)
            .Select(r => new BlockedCategoryDto(r.Id, r.Name, (int)r.ProductCount))
            .ToList();

        var deletedImagePaths = deleted
            .Where(r => !string.IsNullOrEmpty(r.ImagePath))
            .Select(r => r.ImagePath!)
            .ToList();

        return (new CategoryDeletionOutcomeDto(deleted.Count, blocked), deletedImagePaths);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, int>> GetProductCountsAsync(
        IReadOnlyList<Guid> categoryIds, CancellationToken ct)
    {
        if (categoryIds.Count == 0)
            return new Dictionary<Guid, int>();

        var args = new { category_ids = categoryIds };
        var rows = await client.Rpc<List<CategoryProductCountRecord>>(CategoryProductCountsRpc, args) ?? [];

        return rows.ToDictionary(r => r.CategoryId, r => (int)r.ProductCount);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoryLookupDto>> GetActiveLookupAsync(CancellationToken ct)
    {
        var query = client.From<CategoryRecord>();
        query.Filter(IsActiveColumn, Operator.Equals, "true");
        query.Order(NameColumn, Ordering.Ascending);
        var response = await query.Get(ct);
        return [.. response.Models.Select(r => new CategoryLookupDto(r.Id, r.Name))];
    }

    private string ResolveImagePublicUrl(string? imagePath, string name) =>
        imagePath is { } path ? fileStorage.GetPublicUrl(StorageArea.CategoryImages, path) : PlaceholderImage.For(name);

    /// <summary>
    /// Narrows the query by the supplied criteria. Each criterion is applied only when supplied —
    /// a null <paramref name="search"/> or <paramref name="status"/> adds no predicate at all.
    /// </summary>
    private static IPostgrestTable<CategoryRecord> ApplyFilters(
        IPostgrestTable<CategoryRecord> query, string? search, CategoryStatusFilter? status)
    {
        if (!string.IsNullOrWhiteSpace(search))
            query.Filter(NameColumn, Operator.ILike, $"%{search.Trim()}%");

        if (status is { } value)
            query.Filter(IsActiveColumn, Operator.Equals, value is CategoryStatusFilter.Active ? "true" : "false");

        return query;
    }

    private static void ApplySort(IPostgrestTable<CategoryRecord> query, CategorySortOption sort)
    {
        switch (sort)
        {
            case CategorySortOption.NameZToA:
                query.Order(NameColumn, Ordering.Descending);
                break;
            case CategorySortOption.NewestFirst:
                query.Order(CreatedAtColumn, Ordering.Descending);
                break;
            case CategorySortOption.OldestFirst:
                query.Order(CreatedAtColumn, Ordering.Ascending);
                break;
            case CategorySortOption.NameAToZ:
            default:
                query.Order(NameColumn, Ordering.Ascending);
                break;
        }

        // Every sort column above is non-unique, and Postgres leaves the relative order of tied rows
        // undefined — so two categories sharing a name or a creation timestamp could be ordered one
        // way while page 1 is fetched and the other way for page 2, showing one of them twice and
        // hiding the other entirely. Breaking the tie on the primary key makes the total order
        // deterministic across requests.
        query.Order(IdColumn, Ordering.Ascending);
    }

    private static bool IsNormalizedNameUniqueViolation(Exception ex) =>
        ex.Message.Contains(NormalizedNameUniqueConstraint, StringComparison.OrdinalIgnoreCase);
}

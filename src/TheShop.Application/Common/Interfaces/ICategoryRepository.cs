using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.Enums;

namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Category"/>. Implementations live in the
/// Infrastructure layer.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// Returns <c>true</c> when a category already exists whose name matches
    /// <paramref name="name"/> case- and whitespace-insensitively (RULE-2), excluding
    /// <paramref name="excludeCategoryId"/> when supplied so a category can keep its own name on
    /// a no-op save (AC-11).
    /// </summary>
    Task<bool> ExistsByNormalizedNameAsync(string name, Guid? excludeCategoryId, CancellationToken ct);

    /// <summary>
    /// Persists a newly created category. Fails with a resource-key <see cref="Result"/> when a
    /// concurrent insert has since taken the name; throws for unexpected technical failures.
    /// </summary>
    Task<Result> AddAsync(Category category, CancellationToken ct);

    /// <summary>
    /// Returns a filtered, sorted, paged slice of categories for the manage-categories admin
    /// list, each carrying its authoritative product count. A <see langword="null"/>
    /// <paramref name="search"/> or <paramref name="status"/> applies no narrowing on that
    /// criterion.
    /// </summary>
    Task<PagedResult<CategoryListItemDto>> GetPageAsync(
        string? search,
        CategoryStatusFilter? status,
        CategorySortOption sort,
        PaginationRequest pagination,
        CancellationToken ct);

    /// <summary>
    /// Returns the category matching <paramref name="id"/>, or <c>null</c> when it no longer
    /// exists.
    /// </summary>
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Persists changes to an existing category. Fails with a resource-key <see cref="Result"/>
    /// when a concurrent update has since taken the name; throws for unexpected technical
    /// failures.
    /// </summary>
    Task<Result> UpdateAsync(Category category, CancellationToken ct);

    /// <summary>
    /// Deletes every category in <paramref name="categoryIds"/> that has no referencing product,
    /// atomically (RULE-15 partial success). Alongside the outcome, returns the image storage
    /// keys of the categories that were actually deleted, so the caller can dispose them
    /// (RULE-11) — the row is already gone by the time this returns, so that disposal is
    /// best-effort.
    /// </summary>
    Task<(CategoryDeletionOutcomeDto Outcome, IReadOnlyList<string> DeletedImagePaths)> DeleteManyAsync(
        IReadOnlyList<Guid> categoryIds, CancellationToken ct);

    /// <summary>
    /// Returns the authoritative product count — published or not (RULE-6) — for each of
    /// <paramref name="categoryIds"/>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetProductCountsAsync(IReadOnlyList<Guid> categoryIds, CancellationToken ct);

    /// <summary>
    /// Returns every Active category as a minimal <c>{ id, name }</c> lookup, name-ordered, for
    /// picker controls (AC-23). Unpaginated — category counts are small reference data.
    /// </summary>
    Task<IReadOnlyList<CategoryLookupDto>> GetActiveLookupAsync(CancellationToken ct);
}

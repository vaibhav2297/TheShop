using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Domain.Entities;
using TheShop.Domain.Enums;

namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Brand"/>. Implementations live in the
/// Infrastructure layer.
/// </summary>
public interface IBrandRepository
{
    /// <summary>
    /// Returns <c>true</c> when a brand already exists whose name matches
    /// <paramref name="name"/> case- and whitespace-insensitively (RULE-2), excluding
    /// <paramref name="excludeBrandId"/> when supplied so a brand can keep its own name on a
    /// no-op save (AC-9).
    /// </summary>
    Task<bool> ExistsByNormalizedNameAsync(string name, Guid? excludeBrandId, CancellationToken ct);

    /// <summary>
    /// Persists a newly created brand. Fails with a resource-key <see cref="Result"/> when a
    /// concurrent insert has since taken the name; throws for unexpected technical failures.
    /// </summary>
    Task<Result> AddAsync(Brand brand, CancellationToken ct);

    /// <summary>
    /// Returns a filtered, sorted, paged slice of brands for the manage-brands admin list, each
    /// carrying its authoritative product count. A <see langword="null"/> <paramref name="search"/>
    /// or <paramref name="status"/> applies no narrowing on that criterion.
    /// </summary>
    Task<PagedResult<BrandListItemDto>> GetPageAsync(
        string? search,
        BrandStatusFilter? status,
        BrandSortOption sort,
        PaginationRequest pagination,
        CancellationToken ct);

    /// <summary>
    /// Returns the brand matching <paramref name="id"/>, or <c>null</c> when it no longer exists.
    /// </summary>
    Task<Brand?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Persists changes to an existing brand. Fails with a resource-key <see cref="Result"/> when
    /// a concurrent update has since taken the name; throws for unexpected technical failures.
    /// </summary>
    Task<Result> UpdateAsync(Brand brand, CancellationToken ct);

    /// <summary>
    /// Deletes every brand in <paramref name="brandIds"/> that has no referencing product,
    /// atomically (RULE-13 partial success). Alongside the outcome, returns the logo storage keys
    /// of the brands that were actually deleted, so the caller can dispose them (RULE-11) — the
    /// row is already gone by the time this returns, so that disposal is best-effort.
    /// </summary>
    Task<(BrandDeletionOutcomeDto Outcome, IReadOnlyList<string> DeletedLogoPaths)> DeleteManyAsync(
        IReadOnlyList<Guid> brandIds, CancellationToken ct);

    /// <summary>
    /// Returns the authoritative product count — published or not (RULE-6) — for each of
    /// <paramref name="brandIds"/>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetProductCountsAsync(IReadOnlyList<Guid> brandIds, CancellationToken ct);

    /// <summary>
    /// Returns every Active brand as a minimal <c>{ id, name }</c> lookup, name-ordered, for
    /// picker controls (AC-23). Unpaginated — brand counts are small reference data.
    /// </summary>
    Task<IReadOnlyList<BrandLookupDto>> GetActiveLookupAsync(CancellationToken ct);
}

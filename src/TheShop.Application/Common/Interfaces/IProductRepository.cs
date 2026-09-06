using TheShop.Application.Common.Filtering;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;

namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Product"/> — both the customer catalogue's read side and
/// the admin CRUD side (list, edit-load, create, update, uniqueness checks). Implementations
/// live in the Infrastructure layer.
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// Returns a filtered, sorted, paged slice of published products matching
    /// <paramref name="criteria"/>, carrying the total number of matches (pre-pagination).
    /// </summary>
    Task<PagedResult<Product>> GetPageAsync(
        ProductCatalogueCriteria criteria, CancellationToken ct);

    /// <summary>
    /// Returns the backend-driven filter groups (category, brand, price range, and the generic
    /// option types published products carry) computed from the published product catalogue.
    /// </summary>
    Task<IReadOnlyList<FilterGroupDto>> GetFilterGroupsAsync(CancellationToken ct);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="name"/> (ignoring case and surrounding
    /// whitespace) or any of <paramref name="skus"/> (each already normalized) already belongs
    /// to another product or variant, excluding <paramref name="excludeProductId"/> so a product
    /// can keep its own name and SKUs on a no-op save (RULE-2, RULE-8).
    /// </summary>
    Task<ProductConflicts> FindConflictsAsync(
        string name, IReadOnlyCollection<string> skus, Guid? excludeProductId, CancellationToken ct);

    /// <summary>
    /// Returns a page of the admin product list, published and unpublished alike, newest first,
    /// with no search/filter/sort (AC-1, AC-2).
    /// </summary>
    Task<PagedResult<ProductListItemDto>> GetAdminPageAsync(PaginationRequest pagination, CancellationToken ct);

    /// <summary>
    /// Returns the full aggregate — gallery, option types, and variants included — for the admin
    /// edit form, alongside its current <c>RowVersion</c> concurrency token, or <c>null</c> when
    /// it no longer exists (AC-34).
    /// </summary>
    Task<(Product Product, string RowVersion)?> GetForEditAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Persists a newly created product — details, gallery, option types, and variants — in one
    /// transaction, returning its assigned <c>RowVersion</c> on success. Fails with a
    /// resource-key <see cref="Result{T}"/> on a concurrent uniqueness conflict; throws for
    /// unexpected technical failures.
    /// </summary>
    Task<Result<string>> AddAsync(Product product, CancellationToken ct);

    /// <summary>
    /// Persists changes to an existing product in one transaction, guarded by
    /// <paramref name="expectedRowVersion"/>: a concurrent save since the form was loaded fails
    /// with <see cref="Features.Products.ProductErrorKeys.ModifiedElsewhere"/>. Returns the new
    /// <c>RowVersion</c> on success; throws for unexpected technical failures.
    /// </summary>
    Task<Result<string>> UpdateAsync(Product product, string expectedRowVersion, CancellationToken ct);
}

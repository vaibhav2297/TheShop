using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.Entities;

namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for the product catalogue's read side. Implementations live in the
/// Infrastructure layer.
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
    /// Returns the backend-driven filter groups (category, brand, flavour, nicotine strength,
    /// price range) computed from the published product catalogue.
    /// </summary>
    Task<IReadOnlyList<FilterGroupDto>> GetFilterGroupsAsync(CancellationToken ct);
}

using TheShop.Application.Common.Models;
using TheShop.Domain.Enums;

namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One immutable set of listing criteria for the admin manage-products page — search, filters,
/// sort, and pagination travel together from <c>GetAdminProductsPageQuery</c> to the repository
/// (plan §3). Every field except <see cref="Sort"/> and <see cref="Pagination"/> is optional: a
/// <see langword="null"/> value applies no narrowing on that criterion.
/// </summary>
public sealed record AdminProductCriteria(
    string? Search,
    ProductStatusFilter? Status,
    IReadOnlyList<Guid>? BrandIds,
    IReadOnlyList<Guid>? CategoryIds,
    decimal? PriceMin,
    decimal? PriceMax,
    AdminProductSortOption Sort,
    PaginationRequest Pagination);

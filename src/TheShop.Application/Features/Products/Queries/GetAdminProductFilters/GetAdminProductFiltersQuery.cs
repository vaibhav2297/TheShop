using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Application.Features.Products.Queries.GetAdminProductFilters;

/// <summary>
/// Requests the filter options for the admin manage-products page: the brands and categories
/// that own a product, plus the variant-aware price bounds (plan §5 Decision 7).
/// </summary>
[RequiresPermission("products.view")]
public sealed record GetAdminProductFiltersQuery : IRequest<Result<AdminProductFiltersDto>>;

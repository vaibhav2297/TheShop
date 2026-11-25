using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;

namespace TheShop.Application.Features.Categories.Queries.GetActiveCategories;

/// <summary>
/// Requests every Active category as a picker lookup (AC-23). Undecorated — category names are
/// public reference data (<c>categories_public_read</c>), so no permission gate applies.
/// </summary>
public sealed record GetActiveCategoriesQuery : IRequest<Result<IReadOnlyList<CategoryLookupDto>>>;

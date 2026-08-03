using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;

namespace TheShop.Application.Features.Categories.Queries.GetCategoryById;

/// <summary>
/// Requests a single category by id, for the edit form. Requires only <c>categories.view</c> —
/// the edit page's <c>categories.edit</c> gate is what denies a view-only admin's direct link
/// (plan §5 Decision 9).
/// </summary>
[RequiresPermission("categories.view")]
public sealed record GetCategoryByIdQuery(Guid Id) : IRequest<Result<CategoryDto>>;

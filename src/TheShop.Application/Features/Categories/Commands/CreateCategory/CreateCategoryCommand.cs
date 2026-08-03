using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;

namespace TheShop.Application.Features.Categories.Commands.CreateCategory;

/// <summary>
/// Creates a new category. Only the name is required; description and image are optional.
/// <see cref="IsActive"/> is the caller's explicit choice — the add page seeds it <c>true</c>
/// (plan §5 Decision 11) but the entity treats it as a plain parameter, not a default.
/// </summary>
[RequiresPermission("categories.create")]
public sealed record CreateCategoryCommand(
    string Name,
    string? Description,
    CategoryImageUpload? Image,
    bool IsActive) : IRequest<Result<CategoryDto>>;

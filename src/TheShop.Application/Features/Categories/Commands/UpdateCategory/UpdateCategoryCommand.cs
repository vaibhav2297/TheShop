using MediatR;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;

namespace TheShop.Application.Features.Categories.Commands.UpdateCategory;

/// <summary>
/// Updates an existing category's name, description, status, and image. <see cref="RemoveImage"/>
/// takes precedence when both it and <see cref="NewImage"/> are supplied.
/// </summary>
[RequiresPermission("categories.edit")]
public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    CategoryImageUpload? NewImage,
    bool RemoveImage) : IRequest<Result<CategoryDto>>;

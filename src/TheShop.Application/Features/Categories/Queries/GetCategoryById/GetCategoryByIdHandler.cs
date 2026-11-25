using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Mappers;

namespace TheShop.Application.Features.Categories.Queries.GetCategoryById;

/// <summary>
/// Handles <see cref="GetCategoryByIdQuery"/>.
/// </summary>
public sealed class GetCategoryByIdHandler(ICategoryRepository categories, IFileStorage fileStorage)
    : IRequestHandler<GetCategoryByIdQuery, Result<CategoryDto>>
{
    /// <inheritdoc/>
    public async Task<Result<CategoryDto>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.Id, cancellationToken);
        if (category is null)
            return Result.Fail<CategoryDto>(CategoryErrorKeys.NotFound);

        return Result.Ok(CategoryDtoMapper.ToDto(category, fileStorage));
    }
}

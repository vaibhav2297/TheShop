using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;

namespace TheShop.Application.Features.Categories.Queries.GetCategoriesPage;

/// <summary>
/// Handles <see cref="GetCategoriesPageQuery"/>. Normalizes pagination to the fixed page size and
/// delegates the filtered/sorted/paged read — including per-category product counts — to
/// <see cref="ICategoryRepository"/>.
/// </summary>
public sealed class GetCategoriesPageHandler(ICategoryRepository categories)
    : IRequestHandler<GetCategoriesPageQuery, Result<PagedResult<CategoryListItemDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<PagedResult<CategoryListItemDto>>> Handle(
        GetCategoriesPageQuery request,
        CancellationToken cancellationToken)
    {
        var pagination = request.Pagination.Normalized(GetCategoriesPageQueryValidator.PageSize);

        var page = await categories.GetPageAsync(
            request.Search, request.Status, request.Sort, pagination, cancellationToken);

        return Result.Ok(page);
    }
}

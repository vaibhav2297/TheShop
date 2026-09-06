using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;

namespace TheShop.Application.Features.Categories.Queries.GetActiveCategories;

/// <summary>
/// Handles <see cref="GetActiveCategoriesQuery"/> by delegating to
/// <see cref="ICategoryRepository.GetActiveLookupAsync"/>.
/// </summary>
public sealed class GetActiveCategoriesHandler(ICategoryRepository categories)
    : IRequestHandler<GetActiveCategoriesQuery, Result<IReadOnlyList<CategoryLookupDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<CategoryLookupDto>>> Handle(
        GetActiveCategoriesQuery request, CancellationToken cancellationToken) =>
        Result.Ok(await categories.GetActiveLookupAsync(cancellationToken));
}

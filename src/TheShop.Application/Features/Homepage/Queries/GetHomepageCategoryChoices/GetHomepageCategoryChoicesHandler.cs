using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Mappers;

namespace TheShop.Application.Features.Homepage.Queries.GetHomepageCategoryChoices;

/// <summary>Resolves active category image URLs for the picker.</summary>
public sealed class GetHomepageCategoryChoicesHandler(ICategoryRepository categories, IFileStorage files)
    : IRequestHandler<GetHomepageCategoryChoicesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(GetHomepageCategoryChoicesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var choices = await categories.GetActiveForHomepageAsync(cancellationToken);
            return Result.Ok<IReadOnlyList<CategoryDto>>([.. choices.Select(x => CategoryDtoMapper.ToDto(x, files))]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result.Fail<IReadOnlyList<CategoryDto>>(HomepageCategoryErrorKeys.ChoicesLoadFailed); }
    }
}

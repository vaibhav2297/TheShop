using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Mappers;

namespace TheShop.Application.Features.Homepage.Queries.GetHomepageBrandChoices;

/// <summary>Resolves active brand logos for the picker.</summary>
public sealed class GetHomepageBrandChoicesHandler(IBrandRepository brands, IFileStorage files)
    : IRequestHandler<GetHomepageBrandChoicesQuery, Result<IReadOnlyList<BrandDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<BrandDto>>> Handle(GetHomepageBrandChoicesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var choices = await brands.GetActiveForHomepageAsync(cancellationToken);
            return Result.Ok<IReadOnlyList<BrandDto>>([.. choices.Select(x => BrandDtoMapper.ToDto(x, files))]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result.Fail<IReadOnlyList<BrandDto>>(HomepageBrandErrorKeys.ChoicesLoadFailed); }
    }
}

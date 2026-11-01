using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Queries.GetFreshDropsForAdmin;

/// <summary>Distinguishes absence from failed or denied reads.</summary>
public sealed class GetFreshDropsForAdminHandler(IFreshDropsRepository repository)
    : IRequestHandler<GetFreshDropsForAdminQuery, Result<FreshDropsDto?>>
{
    /// <inheritdoc/>
    public async Task<Result<FreshDropsDto?>> Handle(GetFreshDropsForAdminQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var drops = await repository.GetAsync(cancellationToken);
            return Result.Ok<FreshDropsDto?>(drops is null ? null : new(drops.Title, drops.ButtonLabel,
                drops.ButtonLink, drops.SelectionMode, [.. drops.ManualProductIds], drops.IsVisible));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail<FreshDropsDto?>(ex.MessageKey); }
        catch { return Result.Fail<FreshDropsDto?>(FreshDropsErrorKeys.LoadFailed); }
    }
}

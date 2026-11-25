using MediatR;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Commands.CreateFreshDrops;

/// <summary>Creates the singleton after pipeline authorization and validation.</summary>
public sealed class CreateFreshDropsHandler(IFreshDropsRepository repository)
    : IRequestHandler<CreateFreshDropsCommand, Result>
{
    /// <inheritdoc/>
    public async Task<Result> Handle(CreateFreshDropsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var drops = FreshDrops.Create(request.Title, request.ButtonLabel, request.ButtonLink,
                request.SelectionMode, request.ManualProductIds, request.IsVisible);
            return await repository.SaveAsync(drops, true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail(ex.MessageKey); }
        catch { return Result.Fail(FreshDropsErrorKeys.SaveFailed); }
    }
}

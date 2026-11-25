using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Roles;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Commands.CreateFeatured;

/// <summary>Creates only an absent singleton after resolving saved image references.</summary>
public sealed class CreateFeaturedHandler(
    IFeaturedRepository repository, IFileStorage storage, ICurrentUserService currentUser)
    : IRequestHandler<CreateFeaturedCommand, Result>
{
    /// <inheritdoc/>
    public async Task<Result> Handle(CreateFeaturedCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } ownerId) return Result.Fail(RbacErrorKeys.AccessDenied);
        FeaturedSection? current;
        try { current = await repository.GetAsync(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail(ex.MessageKey); }
        catch { return Result.Fail(FeaturedErrorKeys.LoadFailed); }
        if (current is not null) return Result.Fail(FeaturedErrorKeys.AlreadyExists);
        return await FeaturedWrite.SaveAsync(repository, storage, ownerId, current, request.Title,
            request.Tile1, request.Tile2, request.IsVisible, cancellationToken);
    }
}

using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Roles;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Commands.CreateHeroBanner;

/// <summary>Creates only when the singleton is absent, retaining the draft on expected failures.</summary>
public sealed class CreateHeroBannerHandler(
    IHeroBannerRepository repository, IFileStorage storage, ICurrentUserService currentUser)
    : IRequestHandler<CreateHeroBannerCommand, Result>
{
    /// <inheritdoc/>
    public async Task<Result> Handle(CreateHeroBannerCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } ownerId)
            return Result.Fail(RbacErrorKeys.AccessDenied);
        TheShop.Domain.Entities.HeroBanner? current;
        try { current = await repository.GetForWriteAsync(true, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail(ex.MessageKey); }
        catch { return Result.Fail(HeroBannerErrorKeys.LoadFailed); }
        if (current is not null) return Result.Fail(HeroBannerErrorKeys.AlreadyExists);
        return await HeroBannerWrite.SaveAsync(repository, storage, ownerId, current, request.Heading,
            request.Description, request.ButtonLabel, request.ButtonLink, request.IsVisible,
            request.DesktopImage, request.MobileImage,
            false, cancellationToken);
    }
}

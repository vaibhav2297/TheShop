using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Queries.GetHeroBannerForAdmin;

/// <summary>Loads saved content and signs private previews without changing stored keys.</summary>
public sealed class GetHeroBannerForAdminHandler(IHeroBannerRepository repository, IFileStorage storage)
    : IRequestHandler<GetHeroBannerForAdminQuery, Result<HeroBannerDto?>>
{
    /// <inheritdoc/>
    public async Task<Result<HeroBannerDto?>> Handle(GetHeroBannerForAdminQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var banner = await repository.GetAsync(cancellationToken);
            if (banner is null) return Result.Ok<HeroBannerDto?>(null);
            var desktop = new HeroBannerImageDto(banner.DesktopImageKey,
                await storage.CreateSignedUrlAsync(StorageArea.HeroBannerImages, banner.DesktopImageKey, 900, cancellationToken));
            var mobile = banner.MobileImageKey is { } key
                ? new HeroBannerImageDto(key, await storage.CreateSignedUrlAsync(StorageArea.HeroBannerImages, key, 900, cancellationToken)) : null;
            return Result.Ok<HeroBannerDto?>(new(banner.Heading, banner.Description, banner.ButtonLabel,
                banner.ButtonLink, banner.IsVisible, desktop, mobile));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail<HeroBannerDto?>(ex.MessageKey); }
        catch { return Result.Fail<HeroBannerDto?>(HeroBannerErrorKeys.LoadFailed); }
    }
}

using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Homepage.Commands;

/// <summary>Coordinates immutable uploads and one configuration commit without deleting committed artwork.</summary>
internal static class HeroBannerWrite
{
    internal static async Task<Result> SaveAsync(IHeroBannerRepository repository, IFileStorage storage,
        Guid ownerId, HeroBanner? current, string heading, string? description, string? buttonLabel, string? buttonLink,
        bool isVisible, ImageUpload? desktop, ImageUpload? mobile, bool removeMobile,
        CancellationToken cancellationToken)
    {
        List<string> uploaded = [];
        string? desktopKey = current?.DesktopImageKey;
        string? mobileKey = removeMobile ? null : current?.MobileImageKey;
        try
        {
            if (desktop is not null)
            {
                desktopKey = await UploadAsync(storage, ownerId, desktop, cancellationToken);
                uploaded.Add(desktopKey);
            }
            if (mobile is not null)
            {
                mobileKey = await UploadAsync(storage, ownerId, mobile, cancellationToken);
                uploaded.Add(mobileKey);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            await CompensateAsync(storage, uploaded, cancellationToken);
            return Result.Fail(HeroBannerErrorKeys.UploadFailed);
        }

        HeroBanner banner;
        try
        {
            if (current is null)
                banner = HeroBanner.Create(heading, description, buttonLabel, buttonLink, desktopKey!, mobileKey, isVisible);
            else
            {
                banner = current;
                banner.ChangeContent(heading, description, buttonLabel, buttonLink);
                banner.ChangeImages(desktopKey!, mobileKey);
                banner.SetVisibility(isVisible);
            }
        }
        catch (DomainException ex)
        {
            await CompensateAsync(storage, uploaded, cancellationToken);
            return Result.Fail(ex.MessageKey);
        }

        Result result;
        try
        {
            result = current is null
                ? await repository.AddAsync(banner, cancellationToken)
                : await repository.UpdateAsync(banner, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return Result.Fail(HeroBannerErrorKeys.SaveUnconfirmed);
        }
        if (result.IsFailure && result.Error != HeroBannerErrorKeys.SaveUnconfirmed)
            await CompensateAsync(storage, uploaded, cancellationToken);
        return result;
    }

    private static async Task<string> UploadAsync(
        IFileStorage storage, Guid ownerId, ImageUpload image, CancellationToken ct)
    {
        var extension = image.ContentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("Unsupported image type after validation.")
        };
        using var content = new MemoryStream(image.Content);
        return await storage.UploadAsync(StorageArea.HeroBannerImages, ownerId, content,
            $"image{extension}", image.ContentType, ct);
    }

    private static async Task CompensateAsync(IFileStorage storage, List<string> uploaded, CancellationToken ct)
    {
        foreach (var key in uploaded)
        {
            try { await storage.DeleteAsync(StorageArea.HeroBannerImages, key, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { }
        }
    }
}

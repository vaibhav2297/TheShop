using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Commands;

internal static class FeaturedWrite
{
    internal static async Task<Result> SaveAsync(IFeaturedRepository repository, IFileStorage storage,
        Guid ownerId, FeaturedSection? current, string title, FeaturedTileInput tile1,
        FeaturedTileInput tile2, bool isVisible, CancellationToken cancellationToken)
    {
        string? key1 = tile1.RemoveImage ? null : current?.Tile1.ImageKey;
        string? key2 = tile2.RemoveImage ? null : current?.Tile2.ImageKey;
        try
        {
            if (tile1.NewImage is { } image1) key1 = await UploadAsync(storage, ownerId, image1, cancellationToken);
            if (tile2.NewImage is { } image2) key2 = await UploadAsync(storage, ownerId, image2, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result.Fail(FeaturedErrorKeys.UploadFailed); }

        FeaturedSection section;
        try
        {
            section = FeaturedSection.Create(title,
                FeaturedTile.Create(tile1.Label, tile1.Title, tile1.ButtonLabel, tile1.ButtonLink, key1!),
                FeaturedTile.Create(tile2.Label, tile2.Title, tile2.ButtonLabel, tile2.ButtonLink, key2!), isVisible);
        }
        catch (DomainException ex) { return Result.Fail(ex.MessageKey); }
        try
        {
            return current is null
                ? await repository.AddAsync(section, cancellationToken)
                : await repository.UpdateAsync(section, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Result.Fail(FeaturedErrorKeys.SaveUnconfirmed); }
    }

    private static async Task<string> UploadAsync(IFileStorage storage, Guid ownerId,
        ImageUpload image, CancellationToken cancellationToken)
    {
        var extension = image.ContentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("Unsupported image type after validation.")
        };
        using var content = new MemoryStream(image.Content);
        return await storage.UploadAsync(StorageArea.FeaturedImages, ownerId, content,
            $"image{extension}", image.ContentType, cancellationToken);
    }
}

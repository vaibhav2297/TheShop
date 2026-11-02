using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Homepage.Queries.GetFeaturedForAdmin;

/// <summary>Loads fixed tile content with fresh signed private previews.</summary>
public sealed class GetFeaturedForAdminHandler(IFeaturedRepository repository, IFileStorage storage)
    : IRequestHandler<GetFeaturedForAdminQuery, Result<FeaturedDto?>>
{
    /// <inheritdoc/>
    public async Task<Result<FeaturedDto?>> Handle(GetFeaturedForAdminQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var section = await repository.GetAsync(cancellationToken);
            if (section is null) return Result.Ok<FeaturedDto?>(null);
            return Result.Ok<FeaturedDto?>(new(section.Title,
                await TileAsync(section.Tile1, cancellationToken),
                await TileAsync(section.Tile2, cancellationToken), section.IsVisible));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (DomainException ex) { return Result.Fail<FeaturedDto?>(ex.MessageKey); }
        catch { return Result.Fail<FeaturedDto?>(FeaturedErrorKeys.LoadFailed); }
    }

    private async Task<FeaturedTileDto> TileAsync(FeaturedTile tile, CancellationToken cancellationToken)
        => new(tile.Label, tile.Title, tile.ButtonLabel, tile.ButtonLink,
            new(tile.ImageKey, await storage.CreateSignedUrlAsync(
                StorageArea.FeaturedImages, tile.ImageKey, 900, cancellationToken)));
}

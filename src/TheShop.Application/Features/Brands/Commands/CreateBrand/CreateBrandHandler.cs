using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Mappers;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Brands.Commands.CreateBrand;

/// <summary>
/// Handles <see cref="CreateBrandCommand"/>. Guards name uniqueness, invokes
/// <see cref="Brand.Create"/> for the integrity invariants, optionally uploads the logo, then
/// persists the brand. Best-effort deletes an uploaded logo if persistence fails afterwards so
/// no partial brand — an orphaned image with no row — appears in the store.
/// </summary>
public sealed class CreateBrandHandler(IBrandRepository brands, IFileStorage fileStorage)
    : IRequestHandler<CreateBrandCommand, Result<BrandDto>>
{
    /// <inheritdoc/>
    public async Task<Result<BrandDto>> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
    {
        if (await brands.ExistsByNormalizedNameAsync(request.Name, cancellationToken))
            return Result.Fail<BrandDto>(BrandErrorKeys.AlreadyExists);

        Brand brand;
        try
        {
            brand = Brand.Create(request.Name, request.Description, request.IsActive);
        }
        catch (DomainException ex)
        {
            return Result.Fail<BrandDto>(ex.MessageKey);
        }

        string? logoPath = null;
        try
        {
            if (request.Logo is { } logo)
            {
                using var content = new MemoryStream(logo.Content);
                logoPath = await fileStorage.UploadAsync(
                    StorageArea.BrandLogos, brand.Id, content, logo.FileName, logo.ContentType, cancellationToken);
                brand.AttachLogo(logoPath);
            }

            var addResult = await brands.AddAsync(brand, cancellationToken);
            if (addResult.IsFailure)
            {
                await TryDeleteOrphanedLogoAsync(logoPath, cancellationToken);
                return Result.Fail<BrandDto>(addResult.Error!);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await TryDeleteOrphanedLogoAsync(logoPath, cancellationToken);
            return Result.Fail<BrandDto>(BrandErrorKeys.CreateFailed);
        }

        return Result.Ok(BrandDtoMapper.ToDto(brand, fileStorage));
    }

    private async Task TryDeleteOrphanedLogoAsync(string? logoPath, CancellationToken cancellationToken)
    {
        if (logoPath is null)
            return;

        try
        {
            await fileStorage.DeleteAsync(StorageArea.BrandLogos, logoPath, cancellationToken);
        }
        catch
        {
            // Best-effort compensation (Decision 5): an orphaned storage object is acceptable;
            // failing this request a second time over cleanup is not.
        }
    }
}

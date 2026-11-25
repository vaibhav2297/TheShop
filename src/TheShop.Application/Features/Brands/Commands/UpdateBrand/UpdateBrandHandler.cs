using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Application.Features.Brands.Mappers;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Brands.Commands.UpdateBrand;

/// <summary>
/// Handles <see cref="UpdateBrandCommand"/>. Guards name uniqueness (excluding the brand's own
/// current name, AC-9), invokes the <c>Brand</c> mutation methods for the integrity invariants,
/// applies the logo change, then persists. Best-effort disposes the outgoing logo object after a
/// successful update (RULE-11) — mirroring <c>CreateBrandHandler</c>'s compensation stance: an
/// orphaned object never fails the request.
/// </summary>
public sealed class UpdateBrandHandler(IBrandRepository brands, IFileStorage fileStorage)
    : IRequestHandler<UpdateBrandCommand, Result<BrandDto>>
{
    /// <inheritdoc/>
    public async Task<Result<BrandDto>> Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
    {
        var brand = await brands.GetByIdAsync(request.Id, cancellationToken);
        if (brand is null)
            return Result.Fail<BrandDto>(BrandErrorKeys.NotFound);

        if (await brands.ExistsByNormalizedNameAsync(request.Name, request.Id, cancellationToken))
            return Result.Fail<BrandDto>(BrandErrorKeys.AlreadyExists);

        try
        {
            brand.Rename(request.Name);
            brand.ChangeDescription(request.Description);

            if (request.IsActive)
                brand.Activate();
            else
                brand.Deactivate();
        }
        catch (DomainException ex)
        {
            return Result.Fail<BrandDto>(ex.MessageKey);
        }

        var previousLogoPath = brand.LogoPath;
        string? uploadedLogoPath = null;

        try
        {
            if (request.RemoveLogo)
            {
                brand.RemoveLogo();
            }
            else if (request.NewLogo is { } logo)
            {
                using var content = new MemoryStream(logo.Content);
                uploadedLogoPath = await fileStorage.UploadAsync(
                    StorageArea.BrandLogos, brand.Id, content, logo.FileName, logo.ContentType, cancellationToken);
                brand.AttachLogo(uploadedLogoPath);
            }

            var updateResult = await brands.UpdateAsync(brand, cancellationToken);
            if (updateResult.IsFailure)
            {
                await TryDeleteOrphanedLogoAsync(uploadedLogoPath, cancellationToken);
                return Result.Fail<BrandDto>(updateResult.Error!);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await TryDeleteOrphanedLogoAsync(uploadedLogoPath, cancellationToken);
            return Result.Fail<BrandDto>(BrandErrorKeys.UpdateFailed);
        }

        if ((request.RemoveLogo || uploadedLogoPath is not null) && previousLogoPath is not null)
            await TryDeleteOrphanedLogoAsync(previousLogoPath, cancellationToken);

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
            // Best-effort compensation: an orphaned storage object is acceptable; failing this
            // request a second time over cleanup is not.
        }
    }
}

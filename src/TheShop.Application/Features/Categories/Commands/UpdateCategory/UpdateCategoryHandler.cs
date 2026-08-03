using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Mappers;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Categories.Commands.UpdateCategory;

/// <summary>
/// Handles <see cref="UpdateCategoryCommand"/>. Guards name uniqueness (excluding the category's
/// own current name, AC-11), invokes the <c>Category</c> mutation methods for the integrity
/// invariants — no slug is recomputed, since nothing derived from the name exists (FR-11, AC-24) —
/// applies the image change, then persists. Best-effort disposes the outgoing image object after a
/// successful update (RULE-11) — mirroring <c>CreateCategoryHandler</c>'s compensation stance: an
/// orphaned object never fails the request.
/// </summary>
public sealed class UpdateCategoryHandler(ICategoryRepository categories, IFileStorage fileStorage)
    : IRequestHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    /// <inheritdoc/>
    public async Task<Result<CategoryDto>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.Id, cancellationToken);
        if (category is null)
            return Result.Fail<CategoryDto>(CategoryErrorKeys.NotFound);

        if (await categories.ExistsByNormalizedNameAsync(request.Name, request.Id, cancellationToken))
            return Result.Fail<CategoryDto>(CategoryErrorKeys.AlreadyExists);

        try
        {
            category.Rename(request.Name);
            category.ChangeDescription(request.Description);

            if (request.IsActive)
                category.Activate();
            else
                category.Deactivate();
        }
        catch (DomainException ex)
        {
            return Result.Fail<CategoryDto>(ex.MessageKey);
        }

        var previousImagePath = category.ImagePath;
        string? uploadedImagePath = null;

        try
        {
            if (request.RemoveImage)
            {
                category.RemoveImage();
            }
            else if (request.NewImage is { } image)
            {
                using var content = new MemoryStream(image.Content);
                uploadedImagePath = await fileStorage.UploadAsync(
                    StorageArea.CategoryImages, category.Id, content, image.FileName, image.ContentType, cancellationToken);
                category.AttachImage(uploadedImagePath);
            }

            var updateResult = await categories.UpdateAsync(category, cancellationToken);
            if (updateResult.IsFailure)
            {
                await TryDeleteOrphanedImageAsync(uploadedImagePath, cancellationToken);
                return Result.Fail<CategoryDto>(updateResult.Error!);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await TryDeleteOrphanedImageAsync(uploadedImagePath, cancellationToken);
            return Result.Fail<CategoryDto>(CategoryErrorKeys.UpdateFailed);
        }

        if ((request.RemoveImage || uploadedImagePath is not null) && previousImagePath is not null)
            await TryDeleteOrphanedImageAsync(previousImagePath, cancellationToken);

        return Result.Ok(CategoryDtoMapper.ToDto(category, fileStorage));
    }

    private async Task TryDeleteOrphanedImageAsync(string? imagePath, CancellationToken cancellationToken)
    {
        if (imagePath is null)
            return;

        try
        {
            await fileStorage.DeleteAsync(StorageArea.CategoryImages, imagePath, cancellationToken);
        }
        catch
        {
            // Best-effort compensation: an orphaned storage object is acceptable; failing this
            // request a second time over cleanup is not.
        }
    }
}

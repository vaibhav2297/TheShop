using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Application.Features.Categories.Mappers;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;

namespace TheShop.Application.Features.Categories.Commands.CreateCategory;

/// <summary>
/// Handles <see cref="CreateCategoryCommand"/>. Guards name uniqueness, invokes
/// <see cref="Category.Create"/> for the integrity invariants, optionally uploads the image, then
/// persists the category. Best-effort deletes an uploaded image if persistence fails afterwards
/// so no partial category — an orphaned image with no row — appears in the store.
/// </summary>
public sealed class CreateCategoryHandler(ICategoryRepository categories, IFileStorage fileStorage)
    : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    /// <inheritdoc/>
    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (await categories.ExistsByNormalizedNameAsync(request.Name, excludeCategoryId: null, cancellationToken))
            return Result.Fail<CategoryDto>(CategoryErrorKeys.AlreadyExists);

        Category category;
        try
        {
            category = Category.Create(request.Name, request.Description, request.IsActive);
        }
        catch (DomainException ex)
        {
            return Result.Fail<CategoryDto>(ex.MessageKey);
        }

        string? imagePath = null;
        try
        {
            if (request.Image is { } image)
            {
                using var content = new MemoryStream(image.Content);
                imagePath = await fileStorage.UploadAsync(
                    StorageArea.CategoryImages, category.Id, content, image.FileName, image.ContentType, cancellationToken);
                category.AttachImage(imagePath);
            }

            var addResult = await categories.AddAsync(category, cancellationToken);
            if (addResult.IsFailure)
            {
                await TryDeleteOrphanedImageAsync(imagePath, cancellationToken);
                return Result.Fail<CategoryDto>(addResult.Error!);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await TryDeleteOrphanedImageAsync(imagePath, cancellationToken);
            return Result.Fail<CategoryDto>(CategoryErrorKeys.CreateFailed);
        }

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
            // Best-effort compensation (Decision 5): an orphaned storage object is acceptable;
            // failing this request a second time over cleanup is not.
        }
    }
}

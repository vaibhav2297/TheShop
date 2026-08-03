using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Categories.DTOs;
using TheShop.Domain.Entities;

namespace TheShop.Application.Features.Categories.Mappers;

/// <summary>
/// Hand-written <see cref="Category"/> → <see cref="CategoryDto"/> mapping. A dedicated static
/// mapper rather than an AutoMapper profile — the projection is small and one-directional.
/// </summary>
public static class CategoryDtoMapper
{
    public static CategoryDto ToDto(Category category, IFileStorage fileStorage) =>
        new(
            category.Id,
            category.Name,
            category.Description,
            category.ImagePath is { } imagePath ? fileStorage.GetPublicUrl(StorageArea.CategoryImages, imagePath) : null,
            category.IsActive);
}

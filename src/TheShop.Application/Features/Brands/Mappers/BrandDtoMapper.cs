using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Domain.Entities;

namespace TheShop.Application.Features.Brands.Mappers;

/// <summary>
/// Hand-written <see cref="Brand"/> → <see cref="BrandDto"/> mapping. A dedicated static mapper
/// rather than an AutoMapper profile — the projection is small and one-directional.
/// </summary>
public static class BrandDtoMapper
{
    public static BrandDto ToDto(Brand brand, IFileStorage fileStorage) =>
        new(
            brand.Id,
            brand.Name,
            brand.Slug,
            brand.Description,
            brand.LogoPath is { } logoPath ? fileStorage.GetPublicUrl(StorageArea.BrandLogos, logoPath) : null,
            brand.IsActive);
}

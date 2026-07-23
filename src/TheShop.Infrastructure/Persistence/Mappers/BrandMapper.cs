using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps between <see cref="BrandRecord"/> rows and the domain <see cref="Brand"/> aggregate.
/// </summary>
internal static class BrandMapper
{
    public static Brand ToDomain(this BrandRecord record) =>
        Brand.Rehydrate(record.Id, record.Name, record.Slug, record.Description, record.LogoPath, record.IsActive);

    public static BrandRecord ToRecord(this Brand brand) => new()
    {
        Id = brand.Id,
        Name = brand.Name,
        Slug = brand.Slug,
        Description = brand.Description,
        LogoPath = brand.LogoPath,
        IsActive = brand.IsActive,
        CreatedAt = DateTime.UtcNow,
    };
}

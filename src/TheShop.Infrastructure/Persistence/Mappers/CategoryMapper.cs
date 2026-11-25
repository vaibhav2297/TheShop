using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps between <see cref="CategoryRecord"/> rows and the domain <see cref="Category"/> aggregate.
/// </summary>
internal static class CategoryMapper
{
    public static Category ToDomain(this CategoryRecord record) =>
        Category.Rehydrate(record.Id, record.Name, record.Description, record.ImagePath, record.IsActive);

    public static CategoryRecord ToRecord(this Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        ImagePath = category.ImagePath,
        IsActive = category.IsActive,
        CreatedAt = DateTime.UtcNow,
    };
}

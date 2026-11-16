using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>Restores validated homepage category settings.</summary>
internal static class HomepageCategoryMapper
{
    internal static HomepageCategorySection ToDomain(this HomepageCategoryRecord record) =>
        HomepageCategorySection.Create(record.Title, record.ButtonLabel, record.ButtonLink,
            record.CategoryIds, record.IsVisible);
}

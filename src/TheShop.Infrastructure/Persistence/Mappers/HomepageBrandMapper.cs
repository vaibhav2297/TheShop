using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>Restores validated homepage brand settings.</summary>
internal static class HomepageBrandMapper
{
    internal static HomepageBrandSection ToDomain(this HomepageBrandRecord record) =>
        HomepageBrandSection.Create(record.Title, record.ButtonLabel, record.ButtonLink,
            record.BrandIds, record.IsVisible);
}

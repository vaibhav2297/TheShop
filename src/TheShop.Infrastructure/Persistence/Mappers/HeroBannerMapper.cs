using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>Restores the domain configuration from its private persistence shape.</summary>
internal static class HeroBannerMapper
{
    internal static HeroBanner ToDomain(this HeroBannerRecord record) =>
        HeroBanner.Rehydrate(record.Heading, record.Description, record.ButtonLabel, record.ButtonLink,
            record.DesktopImageKey, record.MobileImageKey, record.IsVisible);
}


using TheShop.Domain.Entities;
using TheShop.Domain.Enums;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>Restores domain settings from the authorized row.</summary>
internal static class FreshDropsMapper
{
    internal static FreshDrops ToDomain(this FreshDropsRecord record) =>
        FreshDrops.Create(record.Title, record.ButtonLabel, record.ButtonLink,
            record.SelectionMode switch
            {
                "newest" => FreshDropsSelectionMode.Newest,
                "manual" => FreshDropsSelectionMode.Manual,
                _ => throw new InvalidOperationException("Unknown Fresh Drops selection mode.")
            }, record.ManualProductIds, record.IsVisible);
}
